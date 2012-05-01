using System;
using System.IO;
#if DEBUG
using System.Diagnostics;
#endif

namespace GimpsharpFsh
{
    static class QfsComp
    {

        public static byte[] Decomp(Stream input)
        {
            byte[] bytes = new byte[input.Length];
            input.ProperRead(bytes, bytes.Length);

            return Decomp(bytes);
        }

        /// <summary>
        /// Decompresses an QFS Compressed File
        /// </summary>
        /// <param name="compressedData">The byte array to decompress</param>
        /// <returns>A MemoryStream containing the decompressed data</returns>
        public unsafe static byte[] Decomp(byte[] compressedData)
        {
            if (compressedData == null)
                throw new ArgumentNullException("compressedData");

            int length = compressedData.Length;

            int outIndex = 0;
            int outLength = 0;

            int startOffset = 0;

            if (compressedData[0] != 16 || compressedData[1] != 0xfb)
            {
                startOffset = 4;
                if (compressedData[4] != 16 && compressedData[5] != 0xfb)
                {
                    throw new NotSupportedException(GimpsharpFsh.Properties.Resources.UnsupportedCompressionFormat);
                }
            }

            byte hi = compressedData[startOffset + 2];
            byte mid = compressedData[startOffset + 3];
            byte lo = compressedData[startOffset + 4];

            outLength = ((hi << 16) | (mid << 8)) | lo;

            byte[] unCompressedData = new byte[outLength];

            int index = startOffset + 5;
            if ((compressedData[startOffset] & 1) > 0)
            {
                index = 8;
            }

            byte ccbyte0 = 0; // control char 0
            byte ccbyte1 = 0; // control char 1
            byte ccbyte2 = 0; // control char 2
            byte ccbyte3 = 0; // control char 3

            int plainCount = 0;
            int copyCount = 0;
            int copyOffset = 0;

            int srcIndex = 0;
            fixed (byte* compressed = compressedData, uncompressed = unCompressedData)
            {
                byte* compData = compressed + index;
                byte* unCompData = uncompressed;

                while (index < length && outIndex < outLength) // code adapted from http://simswiki.info/wiki.php?title=DBPF_Compression
                {
                    ccbyte0 = *compData++;
                    index++;

                    if (ccbyte0 >= 0xFC)
                    {
                        plainCount = (ccbyte0 & 3);

                        if ((index + plainCount) > length)
                        {
                            plainCount = (int)(length - index);
                        }


                        copyCount = 0;
                        copyOffset = 0;
                    }
                    else if (ccbyte0 >= 0xE0)
                    {
                        plainCount = (ccbyte0 - 0xDF) << 2;

                        copyCount = 0;
                        copyOffset = 0;
                    }
                    else if (ccbyte0 >= 0xC0)
                    {
                        ccbyte1 = *compData++;
                        ccbyte2 = *compData++;
                        ccbyte3 = *compData++;

                        index += 3;

                        plainCount = (ccbyte0 & 3);

                        copyCount = (((ccbyte0 >> 2) & 0x03) * 256) + ccbyte3 + 5;
                        copyOffset = (((ccbyte0 & 16) << 12) + (256 * ccbyte1)) + ccbyte2 + 1;
                    }
                    else if (ccbyte0 >= 0x80)
                    {
                        ccbyte1 = *compData++;
                        ccbyte2 = *compData++;
                        index += 2;

                        plainCount = (ccbyte1 >> 6) & 0x03;

                        copyCount = (ccbyte0 & 0x3F) + 4;
                        copyOffset = ((ccbyte1 & 0x3F) * 256) + ccbyte2 + 1;
                    }
                    else
                    {
#if DEBUG
                    if ((index + 1L) >= compressedData.Length)
                    {
                        Debugger.Break();

                        /*using (FileStream fs = new FileStream(@"C:\Dev_projects\sc4\readfshdat\bin\Debug\dump.qfs",FileMode.Create, FileAccess.Write))
                        {
                            byte[] buf = ((MemoryStream)input).ToArray();
                            fs.Write(buf, ccbyte0, buf.Length);
                        }*/


                        Debug.WriteLine(ccbyte0.ToString("X1"));
                    }
#endif

                        ccbyte1 = *compData++;
                        index++;

                        plainCount = (ccbyte0 & 3);

                        copyCount = ((ccbyte0 & 0x1c) >> 2) + 3;
                        copyOffset = ((ccbyte0 >> 5) << 8) + ccbyte1 + 1;
                    }

                    byte* pDst = unCompData + outIndex;
                    Copy(ref compData, ref pDst, plainCount);
     
                    index += plainCount;
                    outIndex += plainCount;

                    srcIndex = outIndex - copyOffset;

                    byte* src = unCompData + srcIndex;
                    byte* dst = unCompData + outIndex;
                    Copy(ref src, ref dst, copyCount);

                    srcIndex += copyCount;
                    outIndex += copyCount;

                }
            }

            return unCompressedData;
        }

        private static unsafe void Copy(ref byte* src, ref byte* dst, int length)
        {
            while (length-- > 0)
            {
                *dst++ = *src++;
            }
        }

        const int QfsMaxIterCount = 50;
        const int QfsMaxBlockSize = 1028;
        const int CompMaxLen = 131072; // FshTool's WINDOWLEN
        const int CompMask = CompMaxLen - 1;  // Fshtool's WINDOWMASK
        /// <summary>
        /// Compresses the input byte array with QFS compression
        /// </summary>
        /// <param name="input">The input byte array to compress</param>
        /// <returns>The compressed data or null if the compession fails</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1814:PreferJaggedArraysOverMultidimensional", MessageId = "Body"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        public static byte[] Comp(byte[] input, bool incLen)
        {
            if (input == null)
                throw new ArgumentNullException("input", "input byte array is null.");

            int inlen = (int)input.Length;
            byte[] inbuf = new byte[(inlen + 1028)]; // 1028 byte safety buffer
            Buffer.BlockCopy(input, 0, inbuf, 0, input.Length);

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

            byte[] outbuf = new byte[inlen + 2048];
            outbuf[0] = 0x10;
            outbuf[1] = 0xfb;
            outbuf[2] = (byte)(inlen >> 16);
            outbuf[3] = (byte)((inlen >> 8) & 0xff);
            outbuf[4] = (byte)(inlen & 0xff);
            int outidx = 5;
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
                            if ((outidx + len) >= outbuf.Length)
                                return null;// data did not compress so return null

                            Buffer.BlockCopy(inbuf, lastwrot, outbuf, outidx, len);
                            lastwrot += len;
                            outidx += len;
                        }
                        len = index - lastwrot;
                        if ((bestlen <= 10) && (bestoffs <= 1024))
                        {
                            outbuf[outidx++] = (byte)(((((bestoffs - 1) >> 8) << 5) + ((bestlen - 3) << 2)) + len);
                            outbuf[outidx++] = (byte)((bestoffs - 1) & 0xff);

                            if ((outidx + len) >= outbuf.Length)
                                return null;// data did not compress so return null

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

                            if ((outidx + len) >= outbuf.Length)
                                return null;// data did not compress so return null

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

                            if ((outidx + len) >= outbuf.Length)
                                return null;

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
                    return null;// data did not compress so return null

                Buffer.BlockCopy(inbuf, lastwrot, outbuf, outidx, len);
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

            if (incLen)
            {
                byte[] temp = new byte[outidx + 4]; // trim the outbuf array to it's actual length
                
                Array.Copy(BitConverter.GetBytes(outidx), temp, 4); // write the compressed length before the actual data
                Buffer.BlockCopy(outbuf, 0, temp, 4, outidx); 
                outbuf = temp;    
            }
            else
            {
                byte[] temp = new byte[outidx]; // trim the outbuf array to it's actual length
                Buffer.BlockCopy(outbuf, 0, temp, 0, outidx);
                outbuf = temp;    
            }
           

            return outbuf;
        }
    }
}
