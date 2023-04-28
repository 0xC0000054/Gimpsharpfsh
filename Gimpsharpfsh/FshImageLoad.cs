using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using FSHLib;
using System.Runtime.InteropServices;
using GimpsharpFsh.Properties;

namespace GimpsharpFsh
{
    internal sealed class FshImageLoad : IDisposable
    {
        public FshImageLoad()
        {
            disposed = false;
            bitmaps = new List<GimpBitmapItem>();
            header = new FSHHeader();
            dirs = null;
            entries = null;
        }

        public FshImageLoad(Stream input) : this()
        {
            this.Load(input);
        }

        private List<GimpBitmapItem> bitmaps;
        private FSHHeader header;
        private FSHDirEntry[] dirs;
        private FSHEntryHeader[] entries;

        public List<GimpBitmapItem> Bitmaps
        {
            get
            {
                return bitmaps;
            }
        }

        public FSHHeader Header
        {
            get
            {
                return header;
            }
        }


        public FSHDirEntry[] Directories
        {
            get
            {
                return dirs;
            }
        }

        public FSHEntryHeader[] EntryHeaders
        {
            get
            {
                return entries;
            }
        }

        private static int GetBmpDataSize(int width, int height, FshFileFormat code)
        {
            int size = 0;
            switch (code)
            {
                case FshFileFormat.SixteenBit:
                case FshFileFormat.SixteenBit4x4:
                case FshFileFormat.SixteenBitAlpha:
                    size = (width * height) * 2;
                    break;
                case FshFileFormat.ThirtyTwoBit:
                    size = (width * height) * 4;
                    break;
                case FshFileFormat.TwentyFourBit:
                    size = (width * height) * 3;
                    break;
                case FshFileFormat.DXT1:
                    size = (width * height) / 2;
                    break;
                case FshFileFormat.DXT3:
                    size = (width * height);
                    break;
            }

            return size;
        }

        private MemoryStream Decomp(Stream input)
        {
            byte[] bytes = QfsComp.Decomp(input);

            return new MemoryStream(bytes);
        }

        public unsafe void Load(Stream input)
        {
            if (input.Length <= 4)
            {
                throw new FormatException(Resources.InvalidFshFile);
            }

            MemoryStream ms = null;
            byte[] compSig = new byte[2];

            if ((compSig[0] & 0xfe) == 0x10 && compSig[1] == 0xfb)
            {
                ms = this.Decomp(input);
            }
            else
            {
                input.Position = 4L;

                input.Read(compSig, 0, 2);

                if ((compSig[0] & 0xfe) == 0x10 && compSig[1] == 0xfb)
                {
                    ms = this.Decomp(input);
                }
                else
                {
                    input.Position = 0L;
                    byte[] bytes = new byte[input.Length];
                    input.ProperRead(bytes, bytes.Length);

                    ms = new MemoryStream(bytes);
                }
            }

            try
            {
                header = new FSHHeader(){ SHPI = new byte[4], dirID = new byte[4] };
                ms.Read(header.SHPI, 0, 4);

                if (Encoding.ASCII.GetString(header.SHPI) != "SHPI")
                {
                    throw new FormatException(Resources.InvalidFshHeader);
                }

                header.size = ms.ReadInt32();
                header.numBmps = ms.ReadInt32();
                ms.Read(header.dirID, 0, 4);

                int nBmps = header.numBmps;

                this.dirs = new FSHDirEntry[nBmps];
                for (int i = 0; i < nBmps; i++)
                {
                    dirs[i] = new FSHDirEntry() { name = new byte[4] };
                    ms.Read(dirs[i].name, 0, 4);
                    dirs[i].offset = ms.ReadInt32();
                }

                for (int i = 0; i < nBmps; i++)
                {
                    ms.Seek((long)dirs[i].offset, SeekOrigin.Begin);
                    int code = (ms.ReadInt32() & 0x7f);

                    if (code == 0x7b || code == 0x7e || code == 0x78 || code == 0x6d)
                    {
                        throw new FormatException(Resources.UnsupportedFshFormat);
                    }
                }
                int size = header.size;


                this.entries = new FSHEntryHeader[nBmps];
                this.bitmaps = new List<GimpBitmapItem>(nBmps);
                for (int i = 0; i < nBmps; i++)
                {
                    FSHDirEntry dir = dirs[i];
                    for (int j = 0; j < nBmps; j++)
                    {
                        if ((dirs[j].offset < size) && (dirs[j].offset > dir.offset)) size = dirs[j].offset;
                    }

                    ms.Seek((long)dir.offset, SeekOrigin.Begin);
                    FSHEntryHeader entry = new FSHEntryHeader();
                    entry = new FSHEntryHeader() { misc = new ushort[4] };
                    entry.code = ms.ReadInt32();
                    entry.width = ms.ReadUInt16();
                    entry.height = ms.ReadUInt16();
                    for (int m = 0; m < 4; m++)
                    {
                        entry.misc[m] = ms.ReadUInt16();
                    }

                    int code = (entry.code & 0x7f);

                    if ((entry.code & 0x80) > 0)
                    {
                        throw new FormatException(Resources.CompressedEntriesNotSupported);
                    }

                    bool isbmp = ((code == 0x60) || (code == 0x61) || (code == 0x7d) || (code == 0x7f));

                    if (isbmp)
                    {
                        FSHEntryHeader aux = entry;
                        int nAttach = 0;
                        int auxofs = dir.offset;
                        while ((aux.code >> 8) > 0)
                        {
                            auxofs += (aux.code >> 8);

                            if ((auxofs + 16) >= size)
                            {
                                break;
                            }
                            nAttach++;
                        }



                        int numScales = (entry.misc[3] >> 12) & 0x0f;
                        if (((entry.width % 1) << numScales) > 0 || ((entry.height % 1) << numScales) > 0)
                        {
                            numScales = 0;
                        }

                        if (numScales > 0)
                        {
                            int bpp = 0;
                            int mbpLen = 0;
                            int mbpPadLen = 0;
                            int bmpw = 0;
                            switch (code)
                            {
                                case 0x7b:
                                case 0x61:
                                    bpp = 2;
                                    break;
                                case 0x7d:
                                    bpp = 8;
                                    break;
                                case 0x7f:
                                    bpp = 6;
                                    break;
                                case 0x60:
                                    bpp = 1;
                                    break;
                            }
                            for (int n = 0; n <= numScales; n++)
                            {
                                bmpw = (entry.width >> n);
                                int bmph = (entry.height >> n);
                                if (code == 0x60)
                                {
                                    bmpw += (4 - bmpw) & 3;
                                    bmph += (4 - bmph) & 3;
                                }
                                mbpLen += (bmpw * bmph) * bpp / 2;
                                mbpPadLen += (bmpw * bmph) * bpp / 2;

                                if (code != 0x60)
                                {
                                    mbpLen += ((16 - mbpLen) & 15); // padding
                                    if (n == numScales)
                                    {
                                        mbpPadLen += ((16 - mbpPadLen) & 15);
                                    }
                                }
                            }
                            if (((entry.code >> 8) != mbpLen + 16) && ((entry.code >> 8) != 0) ||
                                ((entry.code >> 8) == 0) && ((mbpLen + dir.offset + 16) != size))
                            {
                                if (((entry.code >> 8) != mbpPadLen + 16) && ((entry.code >> 8) != 0) ||
                                ((entry.code >> 8) == 0) && ((mbpPadLen + dir.offset + 16) != size))
                                {
                                    numScales = 0;
                                }
                            }

                            if (numScales > 0)
                            {
                                throw new FormatException(Resources.MultiscaleBitmapsNotSupported);
                            }
                        }

                        int width = (int)entry.width;
                        int height = (int)entry.height;
                        FshFileFormat format = (FshFileFormat)code;


                        GimpBitmapItem item = new GimpBitmapItem(width, height, format);
                        int dataSize = GetBmpDataSize(width, height, format);
                        int destSize = width * height * 4;
                        int destStride = width * 4;

                        long bmppos = (long)(dir.offset + 16);

                        byte[] buf = new byte[dataSize];
                        ms.Seek(bmppos, SeekOrigin.Begin);
                        ms.ProperRead(buf, buf.Length);

                        byte[] dest = null;

                        if (code == 0x7d || code == 0x7f)
                        {
                            dest = new byte[dataSize];
                        }
                        int srcStride = 0;

                        switch (code)
                        {
                            case 0x7d: // 32-bit ARGB

                                srcStride = width * 4;

                                fixed (byte* sPtr = buf, dPtr = dest)
                                {
                                    for (int y = 0; y < height; y++)
                                    {
                                        byte* src = sPtr + (y * srcStride);
                                        byte* dst = dPtr + (y * destStride);

                                        for (int x = 0; x < width; x++)
                                        {
                                            dst[0] = src[2]; // red
                                            dst[1] = src[1]; // green
                                            dst[2] = src[0]; // blue
                                            dst[3] = src[3]; // alpha

                                            src += 4;
                                            dst += 4;
                                        }
                                    }
                                }

                                item.ImageData = dest;
                                break;
                            case 0x7f:
                                srcStride = width * 3;

                                fixed (byte* sPtr = buf, dPtr = dest)
                                {
                                    for (int y = 0; y < height; y++)
                                    {
                                        byte* src = sPtr + (y * srcStride);
                                        byte* dst = dPtr + (y * destStride);

                                        for (int x = 0; x < width; x++)
                                        {
                                            dst[0] = src[2]; // red
                                            dst[1] = src[1]; // green
                                            dst[2] = src[0]; // blue
                                            dst[3] = 255; // alpha

                                            src += 3;
                                            dst += 4;
                                        }
                                    }
                                }

                                item.ImageData = dest;
                                break;
                            case 0x60:
                            case 0x61:

                                item.ImageData = DXTComp.UnpackDXT(buf, width, height, (code == 0x60));
                                break;
                        }

                        entries[i] = entry;
                        bitmaps.Add(item);
                    }

                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (ms != null)
                {
                    ms.Dispose();
                    ms = null;
                }
            }
        }

        private bool disposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    disposed = true;
                }
            }
        }
    }
}
