/*
*  This file is part of Gimpsharpfsh, a file format plug-in for GIMP
*  that loads and saves FSH images.
*
*  Copyright (C) 2009, 2010, 2011, 2012, 2023 Nicholas Hayes
*
*  This program is free software: you can redistribute it and/or modify
*  it under the terms of the GNU General Public License as published by
*  the Free Software Foundation, either version 3 of the License, or
*  (at your option) any later version.
*
*  This program is distributed in the hope that it will be useful,
*  but WITHOUT ANY WARRANTY; without even the implied warranty of
*  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*  GNU General Public License for more details.
*
*  You should have received a copy of the GNU General Public License
*  along with this program.  If not, see <http://www.gnu.org/licenses/>.
*
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace GimpsharpFsh
{
    internal class Fshwrite
    {
        public Fshwrite()
        {
            bmplist = new List<Bitmap>();
            alphalist = new List<Bitmap>();
            dirnames = new List<byte[]>();
            codelist = new List<int>();
        }

        private Bitmap BlendDXTBmp(Bitmap colorbmp, Bitmap bmpalpha)
        {
            Bitmap image = null;
            if (colorbmp != null && bmpalpha != null)
            {
                image = new Bitmap(colorbmp.Width, colorbmp.Height, PixelFormat.Format32bppArgb);
            }
            if (colorbmp.Size != bmpalpha.Size)
            {
                throw new ArgumentException("The bitmap and alpha must be equal size");
            }
            BitmapData colordata = colorbmp.LockBits(new Rectangle(0, 0, colorbmp.Width, colorbmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData alphadata = bmpalpha.LockBits(new Rectangle(0, 0, bmpalpha.Width, bmpalpha.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData bdata = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            IntPtr scan0 = bdata.Scan0;
            unsafe
            {
                byte* clrdata = (byte*)(void*)colordata.Scan0;
                byte* aldata = (byte*)(void*)alphadata.Scan0;
                byte* destdata = (byte*)(void*)scan0;
                int offset = bdata.Stride - image.Width * 4;
                int clroffset = colordata.Stride - image.Width * 4;
                int aloffset = alphadata.Stride - image.Width * 4;
                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        destdata[3] = aldata[0];
                        destdata[0] = clrdata[0];
                        destdata[1] = clrdata[1];
                        destdata[2] = clrdata[2];


                        destdata += 4;
                        clrdata += 4;
                        aldata += 4;
                    }
                    destdata += offset;
                    clrdata += clroffset;
                    aldata += aloffset;
                }

            }
            colorbmp.UnlockBits(colordata);
            bmpalpha.UnlockBits(alphadata);
            image.UnlockBits(bdata);
            return image;
        }

        private List<Bitmap> bmplist = null;
        private List<Bitmap> alphalist = null;
        private List<byte[]> dirnames = null;
        private List<int> codelist = null;
        private int GetBmpDataSize(Bitmap bmp, int code)
        {
            int ret = -1;
            switch (code)
            {
                case 0x60:
                    ret = (bmp.Width * bmp.Height / 2); //Dxt1
                    break;
                case 0x61:
                    ret = (bmp.Width * bmp.Height); //Dxt3
                    break;
            }
            return ret;
        }
        public List<Bitmap> alpha
        {
            get 
            {
                return alphalist;
            }
            set
            {
                alphalist = value;
            }
        }
        public List<Bitmap> bmp
        {
            get
            {
                return bmplist;
            }
            set
            {
                bmplist = value;
            }
        }
        public List<byte[]> dir
        {
            get
            {
                return dirnames;
            }
            set
            {
                dirnames = value;
            }
        }
        public List<int> code
        {
            get
            {
                return codelist;
            }
            set
            {
                codelist = value;
            }
        }
        /// <summary>
        /// The function that writes the fsh
        /// </summary>
        /// <param name="output">The output file to write to</param>
        public unsafe void WriteFsh(Stream output)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                if (bmplist != null && bmplist.Count > 0 && alphalist != null && dirnames != null && codelist != null)
                {
                    //write header
                    ms.Write(Encoding.ASCII.GetBytes("SHPI"), 0, 4); // write SHPI id
                    ms.Write(BitConverter.GetBytes(0), 0, 4); // placeholder for the length
                    ms.Write(BitConverter.GetBytes(bmplist.Count), 0, 4); // write the number of bitmaps in the list

                    ms.Write(Encoding.ASCII.GetBytes("G264"), 0, 4); // 

                    int fshlen = 16 + (8 * bmplist.Count); // fsh length
                    for (int c = 0; c < bmplist.Count; c++)
                    {
                        //write directory
                       // Debug.WriteLine("bmp = " + c.ToString() + " offset = " + fshlen.ToString());
                        ms.Write(dir[c], 0, 4); // directory id
                        ms.Write(BitConverter.GetBytes(fshlen), 0, 4); // Write the Entry offset 

                        fshlen += 16; // skip the entry header length
                        int bmplen = GetBmpDataSize(bmplist[c], codelist[c]);
                        fshlen += bmplen; // skip the bitmap length
                    }
                    for (int b = 0; b < bmplist.Count; b++)
                    {
                        Bitmap bmp = bmplist[b];
                        Bitmap alpha = alphalist[b];
                        int code = codelist[b];
                        // write entry header
                        ms.Write(BitConverter.GetBytes(code), 0, 4); // write the Entry bitmap code
                        ms.Write(BitConverter.GetBytes((short)bmp.Width), 0, 2); // write width
                        ms.Write(BitConverter.GetBytes((short)bmp.Height), 0, 2); //write height
                        for (int m = 0; m < 4; m++)
                        {
                            ms.Write(BitConverter.GetBytes((short)0), 0, 2);// write misc data
                        }

                        if (code == 0x60) //DXT1
                        {
                            Bitmap temp = BlendDXTBmp(bmp, alpha);
                            byte[] data = new byte[temp.Width * temp.Height * 4];
                            int flags = (int)Squish.SquishFlags.kDxt1;
                            flags |= (int)Squish.SquishFlags.kColourIterativeClusterFit;
                            data = Squish.CompressImage(temp, flags);
                            ms.Write(data, 0, data.Length);
                        }
                        else if (code == 0x61) // DXT3
                        {
                            Bitmap temp = BlendDXTBmp(bmp, alpha);
                            byte[] data = new byte[temp.Width * temp.Height * 4];
                            int flags = (int)Squish.SquishFlags.kDxt3;
                            flags |= (int)Squish.SquishFlags.kColourIterativeClusterFit;
                            data = Squish.CompressImage(temp, flags);
                            ms.Write(data, 0, data.Length);
                        }

                    }

                    ms.Position = 4L;
                    ms.Write(BitConverter.GetBytes((int)ms.Length), 0, 4); // write the files length
                    ms.WriteTo(output); // write the memory stream to the file
                }
            }
        }

    }
}
