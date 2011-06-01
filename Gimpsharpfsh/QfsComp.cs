using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace GimpsharpFsh
{
    static class QfsComp
    {
        /// <summary>
        /// Decompresses an QFS Compressed File
        /// </summary>
        /// <param name="input">The input stream to decompress</param>
        /// <param name="offset">The offset to start at</param>
        /// <param name="length">The length of the compressed data block</param>
        /// <returns>A byte array containing the decompressed data</returns>
        public static byte[] Decomp(Stream input, int offset, int length)
        {
            if (input == null)
                throw new ArgumentNullException("input", "input is null.");

            input.Seek((long)offset, SeekOrigin.Begin);

            int complen = (int)(input.Position + length);

            int outidx = 0;
            int outlen = 0;

            byte[] packbuf = new byte[2];
            input.Read(packbuf, 0, 2);
            if (packbuf[0] != 16 && packbuf[1] != 0xfb)
            {
                input.Position = 4L;
                input.Read(packbuf, 0, 2);

                if (packbuf[0] != 16 && packbuf[1] != 0xfb)
                {
                    throw new NotSupportedException("Unsupported compression format");
                }
            }

            outlen = ((input.ReadByte2() << 16) + (input.ReadByte2() << 8) + input.ReadByte2());
            //Debug.WriteLine(outlen.ToString());

            byte[] uncompdata = new byte[outlen];

            byte ccbyte0 = 0; // control char 0
            byte ccbyte1 = 0; // control char 1
            byte ccbyte2 = 0; // control char 2
            byte ccbyte3 = 0; // control char 3

            int plaincnt = 0;
            int copycnt = 0;
            int copyofs = 0;

            int srcidx = 0;

            while (input.Position < complen)
            {
                ccbyte0 = input.ReadByte2();  // return the next byte or throws an EndOfStreamException
                if (ccbyte0 == 0xfc)
                {
                    input.Position -= 1L; // go back one byte
                    break;
                }
                if ((ccbyte0 & 0x80) == 0)
                {
                    ccbyte1 = input.ReadByte2();

                    plaincnt = (ccbyte0 & 3);
                    copycnt = ((ccbyte0 & 0x1c) >> 2) + 3;
                    copyofs = ((ccbyte0 >> 5) << 8) + ccbyte1 + 1;
                }
                else if ((ccbyte0 & 0x40) == 0)
                {
                    ccbyte1 = input.ReadByte2();
                    ccbyte2 = input.ReadByte2();

                    plaincnt = (ccbyte1 >> 6) & 0x03;
                    copycnt = (ccbyte0 & 0x3F) + 4;
                    copyofs = ((ccbyte1 & 0x3F) * 256) + ccbyte2 + 1;
                }
                else if ((ccbyte0 & 0x20) == 0)
                {
                    ccbyte1 = input.ReadByte2();
                    ccbyte2 = input.ReadByte2();
                    ccbyte3 = input.ReadByte2();

                    plaincnt = (ccbyte0 & 3);
                    copycnt = (((ccbyte0 >> 2) & 0x03) * 256) + ccbyte3 + 5;
                    copyofs = (((ccbyte0 & 16) << 12) + (256 * ccbyte1)) + ccbyte2 + 1;
                }
                else
                {
                    plaincnt = ((ccbyte0 & 0x1F) * 4) + 4;
                    copycnt = 0;
                    copyofs = 0;
                }

                for (int i = 0; i < plaincnt; i++)
                {
                    if (input.Position < input.Length)
                    {
                        uncompdata[outidx++] = input.ReadByte2();
                    }
                }

                srcidx = outidx - copyofs;

                for (int i = 0; i < copycnt; i++)
                {
                    uncompdata[outidx++] = uncompdata[srcidx++];
                }
            }

            return uncompdata;
        }

        const int QfsMaxIterCount = 50;
        const int QfsMaxBlockSize = 1028;
        const int CompMaxLen = 131072; // FshTool's WINDOWLEN
        const int CompMask = CompMaxLen - 1;  // Fshtool's WINDOWMASK
        /// <summary>
        /// Compresses the input Stream with QFS compression
        /// </summary>
        /// <param name="input">The input stream data to compress</param>
        /// <returns>The compressed data or null if the compession fails</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        public static byte[] Comp(Stream input)
        {
            if (input == null)
                throw new ArgumentNullException("input", "input is null.");

            int inlen = (int)input.Length;
            byte[] inbuf = new byte[(inlen + 1028)]; // 1028 byte safety buffer
            input.Read(inbuf, 0, (int)input.Length);

            int[] similar_rev = new int[CompMaxLen];
            int[,] last_rev = new int[256, 256];
            int bestlen = 0;
            int bestoffs = 0;
            int len = 0;
            int offs = 0;
            int lastwrot = 0;

            for (int i = 0; i < CompMaxLen; i++)
            {
                similar_rev[i] = -1;
            }

            for (int i = 0; i < 256; i++)
            {
                for (int j = 0; j < 256; j++)
                {
                    last_rev[i, j] = -1;
                }
            }

            byte[] outbuf = new byte[(inlen + 4)];
            Array.Copy(BitConverter.GetBytes(outbuf.Length), 0, outbuf, 0, 4);
            outbuf[4] = 0x10;
            outbuf[5] = 0xfb;
            outbuf[6] = (byte)(inlen >> 16);
            outbuf[7] = (byte)((inlen >> 8) & 0xff);
            outbuf[8] = (byte)(inlen & 0xff);
            int outidx = 9;
            int index = 0;
            lastwrot = 0;
            for (index = 0; index < inlen; index++)
            {
                int temp = last_rev[inbuf[index], inbuf[index + 1]];
                offs = similar_rev[index & CompMask] = temp;
                last_rev[inbuf[index], inbuf[index + 1]] = index;
                if (index < lastwrot)
                {
                    continue;
                }
                else
                {
                    bestlen = 0;
                    int itercnt = 0;
                    while (((offs >= 0) && ((index - offs) < CompMaxLen)) && (itercnt++ < QfsMaxIterCount))
                    {
                        len = 2;
                        while ((inbuf[index + len] == inbuf[offs + len]) && (len < QfsMaxBlockSize))
                        {
                            len++;
                        }
                        if (len > bestlen)
                        {
                            bestlen = len;
                            bestoffs = index - offs;
                        }
                        offs = similar_rev[offs & CompMask];
                    }
                    if (bestlen > (inlen - index))
                    {
                        bestlen = index - inlen;
                    }
                    if (bestlen <= 2)
                    {
                        bestlen = 0;
                    }
                    if ((bestlen == 3) && (bestoffs > 1024))
                    {
                        bestlen = 0;
                    }
                    if ((bestlen == 4) && (bestoffs > 16384))
                    {
                        bestlen = 0;
                    }
                    if (bestlen > 0)
                    {
                        while ((index - lastwrot) >= 4)
                        {
                            len = ((index - lastwrot) / 4) - 1;
                            if (len > 0x1b)
                            {
                                len = 0x1b;
                            }
                            outbuf[outidx++] = (byte)(0xe0 + len);
                            len = (4 * len) + 4;
                            Array.Copy(inbuf, lastwrot, outbuf, outidx, len);
                            lastwrot += len;
                            outidx += len;
                        }
                        len = index - lastwrot;
                        if ((bestlen <= 10) && (bestoffs <= 1024))
                        {
                            outbuf[outidx++] = (byte)(((((bestoffs - 1) >> 8) << 5) + ((bestlen - 3) << 2)) + len);
                            outbuf[outidx++] = (byte)((bestoffs - 1) & 0xff);
                            while (len-- > 0)
                            {
                                outbuf[outidx++] = inbuf[lastwrot++];
                            }
                            lastwrot += bestlen;
                        }
                        else if ((bestlen <= 67) && (bestoffs <= 16384))
                        {
                            outbuf[outidx++] = (byte)(0x80 + (bestlen - 4));
                            outbuf[outidx++] = (byte)((len << 6) + ((bestoffs - 1) >> 8));
                            outbuf[outidx++] = (byte)((bestoffs - 1) & 0xff);
                            while (len-- > 0)
                            {
                                outbuf[outidx++] = inbuf[lastwrot++];
                            }
                            lastwrot += bestlen;
                        }
                        else if ((bestlen <= QfsMaxBlockSize) && (bestoffs < CompMaxLen)) // 0x20
                        {
                            bestoffs--;
                            outbuf[outidx++] = (byte)(((0xc0 + ((bestoffs >> 0x10) << 4)) + (((bestlen - 5) >> 8) << 2)) + len);
                            outbuf[outidx++] = (byte)((bestoffs >> 8) & 0xff);
                            outbuf[outidx++] = (byte)(bestoffs & 0xff);
                            outbuf[outidx++] = (byte)((bestlen - 5) & 0xff);
                            while (len-- > 0)
                            {
                                outbuf[outidx++] = inbuf[lastwrot++];
                            }
                            lastwrot += bestlen;
                        }
                    }
                }
            }
            index = inlen;
            // write the end data
            while ((index - lastwrot) >= 4)
            {
                len = ((index - lastwrot) / 4) - 1;
                if (len > 0x1b)
                {
                    len = 0x1b;
                }
                outbuf[outidx++] = (byte)(0xe0 + len);
                len = (4 * len) + 4;

                if ((outidx + len) >= outbuf.Length)
                    return null;

                Array.Copy(inbuf, lastwrot, outbuf, outidx, len);
                lastwrot += len;
                outidx += len;
            }
            len = index - lastwrot;

            if ((outidx + len) >= outbuf.Length) // add in the remaining data length to check for available space
            {
                return null; // data did not compress so return null
            }

            outbuf[outidx++] = (byte)(0xfc + len);


            while (len-- > 0)
            {
                if (outidx >= outbuf.Length)
                    return null;

                outbuf[outidx++] = inbuf[lastwrot++];
            }
            byte[] tempsize = new byte[outidx]; // trim the outbuf array to it's actual length
            Array.Copy(outbuf, 0, tempsize, 0, outidx);
            outbuf = tempsize;

            return outbuf;
        }
    }
}
