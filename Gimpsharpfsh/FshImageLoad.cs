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
            bitmaps = new List<BitmapItem>();
            header = new FSHHeader();
            dirs = null;
            entries = null;
        }

        public FshImageLoad(Stream input) : this()
        {
            this.Load(input);
        }

        private List<BitmapItem> bitmaps;
        private FSHHeader header;
        private FSHDirEntry[] dirs;
        private FSHEntryHeader[] entries;

        public List<BitmapItem> Bitmaps
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



        private MemoryStream Decomp(Stream input)
        {
            byte[] bytes = QfsComp.Decomp(input, 0, (int)input.Length);

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

            if (compSig[0] == 16 && compSig[1] == 0xfb)
            {
                ms = this.Decomp(input);
            }
            else
            {
                input.Position = 4L;

                input.Read(compSig, 0, 2);

                if (compSig[0] == 16 && compSig[1] == 0xfb)
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
                this.bitmaps = new List<BitmapItem>(nBmps);
                for (int i = 0; i < nBmps; i++)
                { 
                    FSHDirEntry dir = dirs[i];
                    for (int j = 0; j < nBmps; j++)
                    {
                        if ((dirs[j].offset < size) && (dirs[j].offset > dir.offset)) size = dirs[j].offset;
                    }

                    ms.Seek((long)dir.offset, SeekOrigin.Begin);
                    FSHEntryHeader entry = new FSHEntryHeader();
                    entry = new FSHEntryHeader() { misc = new short[4] };
                    entry.code = ms.ReadInt32();
                    entry.width = ms.ReadInt16();
                    entry.height = ms.ReadInt16();
                    for (int m = 0; m < 4; m++)
                    {
                        entry.misc[m] = ms.ReadInt16();
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

                    BitmapItem item = null;
                    int width = (int)entry.width;
                    int height = (int)entry.height;
                    long bmppos = (long)(dir.offset + 16);
                    if (code == 0x60) // DXT1
                    {
                        ms.Seek(bmppos, SeekOrigin.Begin);

                        int blockCount = (((int)entry.width + 3) / 4) * (((int)entry.height + 3) / 4);
                        int blockSize = 8;

                        int ds = (blockCount * blockSize);
                        byte[] buf = new byte[ds];
                      
                        ms.ProperRead(buf, buf.Length);

                        byte[] data = Squish.DecompressImage(buf, width, height, (int)Squish.SquishFlags.kDxt1);
                        item = BuildDxtBitmap(data, width, height, FSHBmpType.DXT1);
                    }
                    else if (code == 0x61) // DXT3
                    {
                        ms.Seek(bmppos, SeekOrigin.Begin);

                        int blockCount = (((int)entry.width + 3) / 4) * (((int)entry.height + 3) / 4);
                        int blockSize = 16;

                        int ds = (blockCount * blockSize);



                        byte[] buf = new byte[ds];
                        ms.ProperRead(buf, buf.Length);

                        byte[] data = Squish.DecompressImage(buf, width,height, (int)Squish.SquishFlags.kDxt3);
                        item = BuildDxtBitmap(data, width, height, FSHBmpType.DXT3);
                    }
                    else if (code == 0x7d) // 32-bit RGBA
                    {
                        ms.Seek(bmppos, SeekOrigin.Begin);

                        byte[] data = new byte[(width * height) * 4];

                        ms.ProperRead(data, data.Length);


                        item = new BitmapItem()
                        {
                            Bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb),
                            Alpha = new Bitmap(width, height, PixelFormat.Format24bppRgb),
                            BmpType = FSHBmpType.ThirtyTwoBit
                        }; 


                        unsafe
                        {

                            // alpha bitmap
                            BitmapData ald = item.Alpha.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);


                            try
                            {
                                for (int y = 0; y < height; y++)
                                {
                                    byte* p = (byte*)(void*)ald.Scan0 + (y * ald.Stride);
                                    for (int x = 0; x < width; x++)
                                    {
                                        int offset = (y * width * 4) + (x * 4);
                                        //Debug.WriteLine(string.Format("y = {0}, x = {1}, offset value = {2}", y.ToString(), x.ToString(), decompdata[offset].ToString())); 
                                        p[0] = p[1] = p[2] = data[offset + 3];
                                        p += 3;
                                    }

                                }
                            }
                            finally
                            {
                                item.Alpha.UnlockBits(ald);
                            }
                            
                            

                            // color bitmap
                            BitmapData d = item.Bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);



                            try
                            {
                                for (int y = 0; y < height; y++)
                                {
                                    for (int x = 0; x < width; x++)
                                    {
                                        byte* p = (byte*)(void*)d.Scan0 + (y * d.Stride) + (x * 4);
                                        int offset = (y * width * 4) + (x * 4);

                                        p[2] = (byte)data[offset]; // red 
                                        p[1] = (byte)data[offset + 1]; // green
                                        p[0] = (byte)data[offset + 2]; // blue
                                    }
                                }
                            }
                            finally
                            {
                                item.Bitmap.UnlockBits(d);
                            }
                            
                        }
                    }
                    else if (code == 0x7f) // 24-bit RGB
                    {
                        ms.Seek(bmppos, SeekOrigin.Begin);

                        byte[] data = new byte[(width * height) * 3];

                        ms.ProperRead(data, data.Length);


                        item = new BitmapItem()
                        {
                            Bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb),
                            Alpha = new Bitmap(width, height, PixelFormat.Format24bppRgb),
                            BmpType = FSHBmpType.TwentyFourBit
                        };


                        unsafe
                        {

                            // alpha bitmap
                            BitmapData ald = item.Alpha.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);


                            try
                            {
                                for (int y = 0; y < height; y++)
                                {
                                    byte* p = (byte*)(void*)ald.Scan0 + (y * ald.Stride);
                                    for (int x = 0; x < width; x++)
                                    {
                                        //Debug.WriteLine(string.Format("y = {0}, x = {1}, offset value = {2}", y.ToString(), x.ToString(), decompdata[offset].ToString())); 
                                        p[0] = p[1] = p[2] = 255;
                                        p += 3;
                                    }

                                }
                            }
                            finally
                            {
                                item.Alpha.UnlockBits(ald);
                            }



                            // color bitmap
                            BitmapData d = item.Bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);



                            try
                            {
                                for (int y = 0; y < height; y++)
                                {
                                    byte* p = (byte*)(void*)d.Scan0 + (y * d.Stride);


                                    for (int x = 0; x < width; x++)
                                    {
                                        int offset = (y * width * 4) + (x * 4);

                                        p[2] = (byte)data[offset]; // red 
                                        p[1] = (byte)data[offset + 1]; // green
                                        p[0] = (byte)data[offset + 2]; // blue
                                        p += 3;
                                    }
                                }
                            }
                            finally
                            {
                                item.Bitmap.UnlockBits(d);
                            }

                        }

                    }

                    if (isbmp)
                    {
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

        /// <summary>
        /// Build the alpha and color bitmaps from the uncompressed DXT image data.
        /// </summary>
        /// <param name="data">The image data.</param>
        /// <param name="bmp">The output color bitmap.</param>
        /// <param name="alpha">The output alpha bitmap.</param>
        private unsafe BitmapItem BuildDxtBitmap(byte[] data, int width, int height, FSHBmpType format)
        {

            BitmapItem item = new BitmapItem() { Bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb), 
                Alpha = new Bitmap(width, height, PixelFormat.Format24bppRgb), BmpType = format };


            // alpha bitmap
            BitmapData ald = item.Alpha.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            unsafe
            {

                for (int y = 0; y < height; y++)
                {
                    byte* p = (byte*)(void*)ald.Scan0 + (y * ald.Stride);
                    for (int x = 0; x < width; x++)
                    {
                        int offset = (y * width * 4) + (x * 4);
                        //Debug.WriteLine(string.Format("y = {0}, x = {1}, offset value = {2}", y.ToString(), x.ToString(), decompdata[offset].ToString())); 
                        p[0] = p[1] = p[2] = data[offset + 3];
                        p += 3;
                    }

                }
            }
            item.Alpha.UnlockBits(ald);

            // color bitmap
            BitmapData d = item.Bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            unsafe
            {


                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        byte* p = (byte*)(void*)d.Scan0 + (y * d.Stride) + (x * 4);
                        int offset = (y * width * 4) + (x * 4);

                        p[2] = (byte)data[offset]; // red 
                        p[1] = (byte)data[offset + 1]; // green
                        p[0] = (byte)data[offset + 2]; // blue
                        p[3] = 255;
                    }
                }
            }

            item.Bitmap.UnlockBits(d);

            return item;
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
                    foreach (var item in bitmaps)
                    {
                        item.Alpha.Dispose();
                        item.Bitmap.Dispose();
                    }
                    disposed = true;
                }
            }
        }
    }
}
