﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using FSHLib;
using Gimp;
using GimpsharpFsh.Properties;
using Gtk;

namespace GimpsharpFsh
{
    class Fsh : FilePlugin
    {
       static void Main(string[] args)
        {
          new Fsh(args);
        }
       
        public Fsh(string[] args) : base(args, "fsh")
        {
        }
        protected override IEnumerable<Procedure> ListProcedures()
        {
            yield return
            FileLoadProcedure("file_Fsh_load",
              _("loads images of the Fsh file format"),
              _("This plug-in loads images of the Fsh file format."),
              "",
              "",
              "",
              ("Fsh Image"));
            yield return 
            FileSaveProcedure("file_Fsh_save",
                     "Saves Fsh images",
                     "This plug-in saves Fsh images.",
                     "",
                     "",
                "",
                     "Fsh Image",
                     "RGB*");
         
        }
        protected override void Query()
        {
            base.Query();
            RegisterLoadHandler("fsh", "");
            RegisterSaveHandler("fsh", "");
        }
        private GimpDialog ErrorDlg(string title, string exmessage, string stack)
        {
            gimp_ui_init(title, false);
            GimpDialog errordlg = new GimpDialog(title, title, IntPtr.Zero, DialogFlags.Modal, null, null, "Ok", ResponseType.Close);
            VBox box1 = new VBox(false, 6) {BorderWidth = 6};
            errordlg.VBox.PackStart(box1, true, true, 6);
            Label message = new Label(exmessage);
            box1.PackStart(message,true,false,3);
            if (!string.IsNullOrEmpty(stack))
            {
                Label st = new Label(stack);
                box1.PackStart(st,true,false,3);
            }
            errordlg.ShowAll();
            return errordlg;
        }

        private int fshType = -1;

        protected override Gimp.Image Load()
        {

#if DEBUG
               Debugger.Launch();
#endif
            try
            {
                string filename = string.Empty;
                filename = ((FileStream)Reader.BaseStream).Name;


                LoadSettings();
                using (FshImageLoad loadfsh = new FshImageLoad(Reader.BaseStream))
                {
                    BitmapItem bmpitem = new BitmapItem();


                    loadfsh.Load(Reader.BaseStream);

                    string[] dirname = new string[loadfsh.Bitmaps.Count];
                    short[] width = new short[loadfsh.Bitmaps.Count];
                    short[] height = new short[loadfsh.Bitmaps.Count];

                    for (int i = 0; i < loadfsh.Bitmaps.Count; i++)
                    {
                        FSHEntryHeader entryhead = loadfsh.EntryHeaders[i];
                        width[i] = entryhead.width;
                        height[i] = entryhead.height;
                        dirname[i] = Encoding.ASCII.GetString(loadfsh.Directories[i].name);
                    }

                    string tgistr = filename + ".TGI";

                    if ((!string.IsNullOrEmpty(filename)) && File.Exists(tgistr))
                    {

                        using (StreamReader sr = new StreamReader(tgistr))
                        {
                            string line;
                            int lncnt = 0;

                            while ((line = sr.ReadLine()) != null)
                            {
                                lncnt++;
                                if (!string.IsNullOrEmpty(line))
                                {
                                    if (line.Equals("7ab50e44", StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        if (lncnt == 3)
                                        {
                                            groupid = line;
                                        }
                                        else if (lncnt == 5)
                                        {
                                            instanceid = line;
                                        }
                                    }
                                }
                            }
                        }


                    }
                    bool blendchecked = false;
                    if (!string.IsNullOrEmpty(settings.GetSetting("loaddlg/alphablend", "True")))
                    {
                        blendchecked = bool.Parse(settings.GetSetting("loaddlg/alphablend", "True"));
                    }
                    // to disable alpha blending
                    if ((!string.IsNullOrEmpty(filename)) && filename.Contains("noblend"))
                    {
                        blendchecked = false;
                    }

                    Gimp.Image image = new Gimp.Image(width[0], height[0], ImageBaseType.Rgb);

                    switch (loadfsh.Bitmaps[0].BmpType)
                    {
                        case FSHBmpType.TwentyFourBit:
                            fshType = 0;
                            break;
                        case FSHBmpType.ThirtyTwoBit:
                            fshType = 1;
                            break;
                        case FSHBmpType.DXT1:
                            fshType = 2;
                            break;
                        case FSHBmpType.DXT3:
                            fshType = 3;
                            break;
                    }

                    if (loadfsh.Bitmaps.Count > 1)
                    {
                        for (int cnt = 0; cnt < loadfsh.Bitmaps.Count; cnt++)
                        {
                            bmpitem = (BitmapItem)loadfsh.Bitmaps[cnt];
                            Bitmap bitmap = new Bitmap(bmpitem.Bitmap);
                            Bitmap alpha = new Bitmap(bmpitem.Alpha);

                            buildlayer(image, cnt, bitmap, alpha, width[cnt], height[cnt], blendchecked, fshType);
                        }
                    }
                    else
                    {
                        bmpitem = (BitmapItem)loadfsh.Bitmaps[0];
                        Bitmap bitmap = new Bitmap(bmpitem.Bitmap);
                        Bitmap alpha = new Bitmap(bmpitem.Alpha);

                        buildlayer(image, 0, bitmap, alpha, width[0], height[0], blendchecked, fshType);
                    }
                    if (!string.IsNullOrEmpty(filename))
                    {
                        image.Filename = filename;
                    }
                    
                    return image;
                }

            }
            catch (Exception ex)
            {
                ErrorDlg(Resources.ErrorLoadingCaption, ex.Message, ex.StackTrace).Run();
                return null;
            }
           
        }

        private void buildlayer(Gimp.Image image, int layerpos, Bitmap fshbmp, Bitmap fshalpha, short fshwidth, short fshheight, bool alphablend, int fshType)
        {
            Layer bglayer = new Layer(image, Resources.FshLayerTitle + layerpos.ToString(), Gimp.ImageType.Rgba);



            image.AddLayer(bglayer, layerpos);
            if (alphablend)
            {
                PixelRgn rgn = new PixelRgn(image.Layers[layerpos], true, false);
                Bitmap destbmp = new Bitmap(fshbmp.Width, fshbmp.Height);
                BitmapData bdata = fshbmp.LockBits(new System.Drawing.Rectangle(0, 0, fshbmp.Width, fshbmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                BitmapData aldata = fshalpha.LockBits(new System.Drawing.Rectangle(0, 0, fshalpha.Width, fshalpha.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                BitmapData destdata = destbmp.LockBits(new System.Drawing.Rectangle(0, 0, destbmp.Width, destbmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                IntPtr srcscan0 = bdata.Scan0;
                IntPtr destscan0 = destdata.Scan0;
                int bytes = bdata.Stride * bdata.Height;
                byte[] tmpdata = new byte[bytes];

                unsafe
                {
                    int offset = bdata.Stride - bdata.Width * 4;
                    int destoffset = destdata.Stride - destdata.Width * 4;
                    byte* dest = (byte*)destscan0.ToPointer();
                    byte* src = (byte*)srcscan0.ToPointer();
                    byte* alsrc = (byte*)aldata.Scan0.ToPointer();

                    for (int pPixel = 0; pPixel < destbmp.Width * destbmp.Height; pPixel++)
                    {
                        for (int iBGR = 0; iBGR < 4; iBGR++)
                        {
                            dest[0] = src[2]; // red
                            dest[1] = src[1]; // green
                            dest[2] = src[0]; // blue
                            dest[3] = alsrc[0];
                        }
                        src += 4;
                        dest += 4;
                        alsrc += 4;
                    }
                    Marshal.Copy(destscan0, tmpdata, 0, bytes);
                }
                fshbmp.UnlockBits(bdata);
                fshalpha.UnlockBits(aldata);
                destbmp.UnlockBits(destdata);            
                rgn.SetRect(tmpdata, 0, 0, fshwidth, fshheight);
            }
            else
            {
                PixelRgn rgn = new PixelRgn(image.Layers[layerpos], true, false);
                Bitmap destbmp = new Bitmap(fshbmp.Width, fshbmp.Height);
                BitmapData bdata = fshbmp.LockBits(new System.Drawing.Rectangle(0, 0, fshbmp.Width, fshbmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                BitmapData destdata = destbmp.LockBits(new System.Drawing.Rectangle(0, 0, destbmp.Width, destbmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                IntPtr srcscan0 = bdata.Scan0;
                IntPtr destscan0 = destdata.Scan0;
                int bytes = destdata.Stride * destdata.Height;
                byte[] tmpdata = new byte[bytes];

                unsafe
                {
                    int offset = bdata.Stride - bdata.Width * 3;
                    int destoffset = destdata.Stride - destdata.Width * 4;
                    byte* dest = (byte*)destscan0.ToPointer();
                    byte* src = (byte*)srcscan0.ToPointer();

                    for (int pPixel = 0; pPixel < destbmp.Width * destbmp.Height; pPixel++)
                    {
                        for (int iBGR = 0; iBGR < 4; iBGR++)
                        {
                            dest[0] = src[2]; // red
                            dest[1] = src[1]; // green
                            dest[2] = src[0]; // blue
                            dest[3] = 255; // alpha 
                        }
                        src += 3; // src is 24-bit 
                        dest += 4;
                    }
                    Marshal.Copy(destscan0, tmpdata, 0, bytes);
                }
                fshbmp.UnlockBits(bdata);
                destbmp.UnlockBits(destdata);
                rgn.SetRect(tmpdata, 0, 0, fshwidth, fshheight);
            }
        }
        private string groupid;
        private string instanceid;
        private void WriteTgi(string filename, int zoom)
        {
            char endreg = ' ';
            char end64 = ' ';
            char end32 = ' ';
            char end16 = ' ';
            char end8 = ' ';
            try
            {
                using (FileStream fs = new FileStream(filename + ".TGI", FileMode.OpenOrCreate, FileAccess.Write))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {

                        if (instanceid.ToUpper().EndsWith("E") || instanceid.ToUpper().EndsWith("D") || instanceid.ToUpper().EndsWith("C") || instanceid.ToUpper().EndsWith("B") || instanceid.ToUpper().EndsWith("A"))
                        {
                            endreg = 'E';
                            end64 = 'D';
                            end32 = 'C';
                            end16 = 'B';
                            end8 = 'A';
                        }
                        else if (instanceid.ToUpper().EndsWith("9") || instanceid.ToUpper().EndsWith("8") || instanceid.ToUpper().EndsWith("7") || instanceid.ToUpper().EndsWith("6") || instanceid.ToUpper().EndsWith("5"))
                        {
                            endreg = '9';
                            end64 = '8';
                            end32 = '7';
                            end16 = '6';
                            end8 = '5';
                        }
                        else if (instanceid.ToUpper().EndsWith("0") || instanceid.ToUpper().EndsWith("1") || instanceid.ToUpper().EndsWith("2") || instanceid.ToUpper().EndsWith("3") || instanceid.ToUpper().EndsWith("4"))
                        {
                            endreg = '4';
                            end64 = '3';
                            end32 = '2';
                            end16 = '1';
                            end8 = '0';
                        }
                        sw.WriteLine("7ab50e44\t\n");
                        sw.WriteLine(string.Format("{0:X8}", groupid + "\n"));

                        switch (zoom)
                        {
                            case 0:
                                sw.WriteLine(string.Format("{0:X8}", instanceid.Substring(0, 7) + end8));
                                break;
                            case 1:
                                sw.WriteLine(string.Format("{0:X8}", instanceid.Substring(0, 7) + end16));
                                break;
                            case 2:
                                sw.WriteLine(string.Format("{0:X8}", instanceid.Substring(0, 7) + end32));
                                break;
                            case 3:
                                sw.WriteLine(string.Format("{0:X8}", instanceid.Substring(0, 7) + end64));
                                break;
                            case 4:
                                sw.WriteLine(string.Format("{0:X8}", instanceid.Substring(0, 7) + endreg));
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            
        }

        private void LoadSettings()
        {
            string setpath = System.IO.Path.Combine(Gimp.Gimp.Directory, "GimpsharpFsh.xml");
            if (File.Exists(setpath))
            {
                if (settings == null)
                {
                    settings = new Settings(setpath);
                }
            }
            else
            {
                using (Stream resourceStream = Assembly.GetAssembly(typeof(Fsh)).GetManifestResourceStream("GimpsharpFsh.GimpsharpFsh.xml"))
                {
                    // Now read s into a byte buffer.
                    byte[] bytes = new byte[resourceStream.Length];
                    int numBytesToRead = (int)resourceStream.Length;
                    int numBytesRead = 0;
                    while (numBytesToRead > 0)
                    {
                        // Read may return anything from 0 to numBytesToRead.
                        int n = resourceStream.Read(bytes, numBytesRead, numBytesToRead);
                        // The end of the file is reached.
                        if (n == 0)
                            break;
                        numBytesRead += n;
                        numBytesToRead -= n;
                    }
                    File.WriteAllBytes(setpath, bytes);
                }
                if (settings == null)
                {
                    settings = new Settings(setpath);
                }
            }
        }
        /// <summary>
        /// Saves the Alpha map data from the input bitmap
        /// </summary>
        /// <param name="sourcebmp">The bitmap of the gimp layer</param>
        /// <param name="bmpitem">The item to save the alpha to</param>
        /// <param name="fshtype">The type of Fsh</param>
        private void savealphadata(Bitmap sourcebmp, BitmapItem bmpitem, int fshtype)
        {
            Bitmap alphamap = new Bitmap(sourcebmp.Width, sourcebmp.Height);
            
            for (int y = 0; y < alphamap.Height; y++)
            {
                for (int x = 0; x < alphamap.Width; x++)
                {
                    Color srcpxl = sourcebmp.GetPixel(x, y);
                    alphamap.SetPixel(x, y, Color.FromArgb(srcpxl.A,srcpxl.A, srcpxl.A));
                }
            }         
            bmpitem.Alpha = alphamap;

            switch (fshtype)
            {
                case 0:
                    bmpitem.BmpType = FSHBmpType.ThirtyTwoBit;
                    break;
                case 1:
                    bmpitem.BmpType = FSHBmpType.TwentyFourBit;
                    break;
                case 2:
                    bmpitem.BmpType = FSHBmpType.DXT1;
                    break;
                case 3:
                    bmpitem.BmpType = FSHBmpType.DXT3;
                    break;
            }
           
            #if DEBUG
            alphamap.Save(@"C:\Dev_projects\sc4\gimpsharpfsh\Gimpsharpfsh\bin\Debug\alphamap.png", ImageFormat.Png);
            #endif
           
        }

        private ComboBox combo = null;
        private CheckButton mipbtn = null;
        private CheckButton fshwritecb = null;
        /// <summary>
        /// Create a Fsh save dialog
        /// </summary>
        /// <param name="hd">is the image the size for hd fsh</param>
        /// <param name="mipenabled">enable the generate mips checkbox</param>
        /// <param name="mipchecked">check the generate mips checkbox</param>
        /// <returns></returns>
        protected GimpDialog CreateSaveDialog(int cboindex, bool mipenabled, bool mipchecked, bool fshwritechecked)
        {
            gimp_ui_init(Resources.SaveDialogText, false);
            GimpDialog dialog = new GimpDialog(Resources.SaveDialogText, Resources.SaveDialogText, IntPtr.Zero, DialogFlags.Modal, null, null, Resources.OkBtnText, ResponseType.Ok, Resources.CancelBtnText, ResponseType.Cancel);
            VBox box1 = new VBox(false, 6) {BorderWidth = 6};
            dialog.VBox.PackStart(box1, true, true, 6);
            combo = ComboBox.NewText();
            
            combo.AppendText(Resources.HDBaseFsh);      
            combo.AppendText(Resources.HDFsh);
            combo.AppendText(Resources.DXT1);
            combo.AppendText(Resources.DXT3);
            combo.Active = cboindex;
            box1.PackStart(combo, true, false, 3);
            mipbtn = new CheckButton(Resources.GenMipmaps);
            mipbtn.Active = mipchecked;
            mipbtn.Visible = mipenabled;
            box1.PackStart(mipbtn,true,false,3);
            fshwritecb = new CheckButton(Resources.FshWriteText);
            fshwritecb.Active = fshwritechecked;
            box1.PackStart(fshwritecb, true, false, 3);
            dialog.ShowAll();
            return dialog;
        }
        private Settings settings = null;   
        private FSHImage[] mipimgs = null;
        protected override bool Save(Gimp.Image image, Drawable drawable, string filename)
        {
            FSHImage saveimg = new FSHImage();
            BitmapItem bmpitem = new BitmapItem();  
            PixelRgn pr = new PixelRgn(drawable, false, false);
#if DEBUG
            Debugger.Launch();
#endif
            try
            {
                LoadSettings();
                bool mipenabled = false;
                bool mipchecked = false;
                bool fshwritechecked = false;

                if (image.Width >= 128 && image.Height >= 128)
                {
                    mipenabled = true;
                }
                else
                {
                    mipenabled = false;
                }
                if (!string.IsNullOrEmpty(settings.GetSetting("savedlg/mipchecked", bool.FalseString)))
                {
                    mipchecked = bool.Parse(settings.GetSetting("savedlg/mipchecked", bool.FalseString));
                }
                if (!string.IsNullOrEmpty(settings.GetSetting("savedlg/fshwrite_checked", bool.TrueString)))
                {
                    fshwritechecked = bool.Parse(settings.GetSetting("savedlg/fshwrite_checked", bool.TrueString));
                }
                int selindex = 2;
                if (fshType != -1)
                {
                    selindex = fshType;
                }
                if (System.IO.Path.GetExtension(filename).Equals(".qfs", StringComparison.OrdinalIgnoreCase))
                {
                    saveimg.IsCompressed = true;
                }
                else
                {
                    saveimg.IsCompressed = false;
                }
              
                GimpDialog dlg = CreateSaveDialog(selindex, mipenabled, mipchecked, fshwritechecked);
                if (dlg.Run() == ResponseType.Ok)
                {
                    settings.PutSetting("savedlg/typeSelected", combo.Active);
                    settings.PutSetting("savedlg/mipchecked", mipbtn.Active.ToString());
                    settings.PutSetting("savedlg/fshwrite_checked", fshwritecb.Active.ToString());

                    if (image.Layers.Count > 1)
                    {

                        Bitmap tempbmp = new Bitmap(image.Width, image.Height);

                        for (int i = 0; i < image.Layers.Count; i++)
                        {
                            if (!image.Layers[i].HasAlpha)
                            {
                                image.Layers[i].AddAlpha();
                            }
                            Layer layer = image.Layers[i];
                            PixelRgn pxlrgn = new PixelRgn(layer, false, false);
                            BitmapItem multiitem = new BitmapItem();

                            byte[] buf = new byte[layer.Width * layer.Height * layer.Bpp];
                            buf = pxlrgn.GetRect(0, 0, layer.Width, layer.Height);
                            tempbmp = BitsToBitmapRGB32(buf, tempbmp.Width, tempbmp.Height);
#if DEBUG
                            tempbmp.Save(@"C:\Dev_projects\sc4\gimpsharpfsh\Gimpsharpfsh\bin\Debug\tempbmp" + layer.Name + ".png", ImageFormat.Png);
#endif
                            multiitem.Bitmap = tempbmp;

                            savealphadata(tempbmp, multiitem, combo.Active);
                            if (mipbtn.Active)
                            {
                                Generatemips(image, i, multiitem.BmpType);
                            }
                            saveimg.Bitmaps.Add(multiitem);
                        }

                    }
                    else
                    {
                        if (!image.Layers[0].HasAlpha)
                        {
                            image.Layers[0].AddAlpha();
                        }

                        Layer layer = image.Layers[0];
                        Bitmap tempbmp = new Bitmap(image.Width, image.Height);
                        byte[] buf = new byte[layer.Width * layer.Height * layer.Bpp];
                        buf = pr.GetRect(0, 0, image.Width, image.Height);
                        tempbmp = BitsToBitmapRGB32(buf, tempbmp.Width, tempbmp.Height);
#if DEBUG
                    tempbmp.Save(@"C:\Dev_projects\sc4\gimpsharpfsh\Gimpsharpfsh\bin\Debug\tempbmp.png", ImageFormat.Png);
#endif
                        bmpitem.Bitmap = tempbmp;
                        savealphadata(tempbmp, bmpitem, combo.Active);
                       
                        if (mipbtn.Active)
                        {
                            Generatemips(image, 0, bmpitem.BmpType);
                        }
                        saveimg.Bitmaps.Add(bmpitem);
                    }
                    saveimg.UpdateDirty();

                    using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        SaveFsh(fs, saveimg);
                    }
                    if (!string.IsNullOrEmpty(groupid) && !string.IsNullOrEmpty(instanceid))
                    {
                        WriteTgi(filename + ".TGI", 4);
                    }
                    if (mipbtn.Active)
                    {
                        string filepath = string.Empty;
                        for (int i = 3; i >= 0; i--)
                        {
                            if (mipimgs[i] != null)
                            {

                                filepath = GetFileName(filename, "_s" + i.ToString());
                                using (FileStream fstream = new FileStream(filepath, FileMode.OpenOrCreate, FileAccess.Write))
                                {
                                    mipimgs[i].IsCompressed = saveimg.IsCompressed;
                                    mipimgs[i].UpdateDirty();
                                    SaveFsh(fstream, mipimgs[i]);
                                }
                                if (!string.IsNullOrEmpty(groupid) && !string.IsNullOrEmpty(instanceid))
                                {
                                    WriteTgi(filepath + ".TGI", i);
                                }
                            }

                        }

                    }

                    if (im != null)
                    {
                        im.Delete();
                    } 
                    return true;
                    
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                ErrorDlg(Resources.ErrorSavingCaption, ex.Message, ex.StackTrace).Run();
                return false;
            }
        }
        
        /// <summary>
        /// Saves a fsh using either FshWrite or FSHLib
        /// </summary>
        /// <param name="fs">The stream to save to</param>
        /// <param name="image">The image to save</param>
        private void SaveFsh(Stream fs, FSHImage image)
        {
            try
            {
                if (IsDXTFsh(image) && fshwritecb.Active)
                {
                    Fshwrite fw = new Fshwrite();
                    foreach (BitmapItem bi in image.Bitmaps)
                    {
                        if ((bi.Bitmap != null && bi.Alpha != null) && bi.BmpType == FSHBmpType.DXT1 || bi.BmpType == FSHBmpType.DXT3)
                        {
                            fw.bmp.Add(bi.Bitmap);
                            fw.alpha.Add(bi.Alpha);
                            fw.dir.Add(bi.DirName);
                            fw.code.Add((int)bi.BmpType);
                        }
                    }
                    fw.WriteFsh(fs);
                }
                else
                {
                    image.Save(fs);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        /// <summary>
        /// Test if the fsh only contains DXT1 or DXT3 items
        /// </summary>
        /// <param name="image">The image to test</param>
        /// <returns>True if successful otherwise false</returns>
        private bool IsDXTFsh(FSHImage image)
        {
            bool result = true;
            foreach (BitmapItem bi in image.Bitmaps)
            {
                if (bi.BmpType != FSHBmpType.DXT3 && bi.BmpType != FSHBmpType.DXT1)
                {
                    result = false;
                }
            }
            return result;
        }

       private Bitmap BitsToBitmapRGB32(Byte[] bytes, int width, int height)
       {
           //swap RGBA to BGRA
           Byte tmp;
           for (int x = 4; x < bytes.GetLength(0); x += 4)
           {
               tmp = bytes[(x + 2)];
               bytes[x + 2] = bytes[x];
               bytes[x] = tmp;
           }
           if (bytes.GetLength(0) < width * height * 4)
           {
               return null;
           }
           Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
           int i;
           BitmapData data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
               ImageLockMode.WriteOnly, bmp.PixelFormat);

           if (data.Stride == width * 4)
           {
               Marshal.Copy(bytes, 0, data.Scan0, width * height * 4);
           }
           else
           {
               for (i = 0; i < bmp.Height; i++)
               {
                   IntPtr p = new IntPtr(data.Scan0.ToInt32() + data.Stride * i);
                   Marshal.Copy(bytes, i * bmp.Width * 4, p, bmp.Width * 4);
               }
           }
           bmp.UnlockBits(data);
           return bmp;
       }
        /// <summary>
        /// Gets the alpha map fron the scaled down Bitmap for the GenerateMips function
        /// </summary>
        /// <param name="buf">The scaled bitmap to extract the alpha from</param>
        /// <returns>The resulting alpha map</returns>
       private Bitmap AlphaMapfromScaledBitmap(Bitmap scaledbmp)
       {
            Bitmap alpha = new Bitmap(scaledbmp.Width,scaledbmp.Height);

		    for (int y = 0; y < alpha.Height; y++)
            {
                for (int x = 0; x < alpha.Width; x++)
                {
                    Color srcpxl = scaledbmp.GetPixel(x, y);
                    alpha.SetPixel(x, y, Color.FromArgb(srcpxl.A,srcpxl.A, srcpxl.A));
                }
            } 

            return alpha;
       }

       private Gimp.Image im = null;
       /// <summary>
       /// Generates the scaled down Mipmaps
       /// </summary>
       ///<param name="image">The source image</param>
       /// <param name="layerindex">The index of the source layer to scale from</param>
       /// <param name="BmpType">The FshBmpType of the original BitmapItem</param>
       private void Generatemips(Gimp.Image image,int layerindex, FSHBmpType BmpType)
       {
           if (image.Width >= 128 && image.Height >= 128)
           {
               
               Bitmap[] bmps = new Bitmap[4];
               Bitmap[] alphas = new Bitmap[4];

               // 0 = 8, 1 = 16, 2 = 32, 3 = 64

               int[] size = new int[4] { 8, 16, 32, 64 };        
               int count = image.Layers.Count;
               List<Layer> resizelayers = new List<Layer>(4) {null, null, null, null};           
               im = new Gimp.Image(image);

               for (int i = 3; i >= 0; i--)
               {   
                   im.Scale(size[i], size[i], InterpolationType.Cubic);

                   Layer copy = im.Layers[layerindex];

                   /*if (Gimp.Gimp.Version.Major >= 2 && Gimp.Gimp.Version.Minor >= 6)
                   {
                       Debug.WriteLine("2.6.0 or newer");
                   }*/
                   /*Drawable dw = copy.Scale(0, 0, size[i], size[i], TransformDirection.Forward, InterpolationType.Cubic, true, 4, false);
                   PixelRgn pr = new PixelRgn(dw,false,false);
                   byte[] buf = new byte[dw.Width * dw.Height * dw.Bpp];
                   buf = pr.GetRect(0,0,dw.Width,dw.Height);*/
                   //copy.Scale(size[i], size[i], false, InterpolationType.Cubic); 
                  
                   PixelRgn pr = new PixelRgn(copy, true, false);
                   byte[] buf = new byte[copy.Width * copy.Height * copy.Bpp];
                   buf = pr.GetRect(0, 0, copy.Width, copy.Height);

                   bmps[i] = BitsToBitmapRGB32(buf, copy.Width, copy.Height);

#if DEBUG
                   bmps[i].Save(@"C:\Dev_projects\sc4\gimpsharpfsh\Gimpsharpfsh\bin\Debug\scalebmp" + i.ToString() + ".png", ImageFormat.Png);
#endif
               }
              
               //alpha
               for (int i = 3; i >= 0; i--)
               {
                   alphas[i] = AlphaMapfromScaledBitmap(bmps[i]);

#if DEBUG
                   alphas[i].Save(@"C:\Dev_projects\sc4\gimpsharpfsh\Gimpsharpfsh\bin\Debug\scaleal" + i.ToString() + ".png", ImageFormat.Png);
#endif
               }
           
               if (mipimgs == null)
               {
                   mipimgs = new FSHImage[4];
               }
               for (int i = 0; i < 4; i++)
               {
                   if (bmps[i] != null && alphas[i] != null)
                   {
                       BitmapItem mipitm = new BitmapItem();
                       mipitm.Bitmap = bmps[i];
                       mipitm.Alpha = alphas[i];
                       if (BmpType == FSHBmpType.DXT3 || BmpType == FSHBmpType.ThirtyTwoBit)
                       {
                           mipitm.BmpType = FSHBmpType.DXT3;
                       }
                       else
                       {
                           mipitm.BmpType = FSHBmpType.DXT1;
                       }
                       if (mipimgs[i] == null)
                       {
                           mipimgs[i] = new FSHImage();
                       }
                       if (mipimgs[i] != null)
                       {
                           mipimgs[i].Bitmaps.Add(mipitm);
                       }
                   }
               }
           }
       }

       private string GetFileName(string path, string add)
       {
           return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileNameWithoutExtension(path) + add + System.IO.Path.GetExtension(path));
       }
    }
}
