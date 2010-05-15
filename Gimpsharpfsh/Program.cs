using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing;
using System.Xml;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using Gimp;
using Gtk;
using FSHLib;
using System.Reflection;

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
            RegisterLoadHandler("fsh,qfs", "");
            RegisterSaveHandler("fsh,qfs", "");
            
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
        private string groupid = null;
        private string instanceid = null;
        override protected Gimp.Image Load(string filename)
        {
            if (File.Exists(filename))
            {
#if DEBUG
            //    Debugger.Launch();
#endif
                try
                {
                    LoadSettings();
                    FSHImage loadfsh = new FSHImage();
                    BitmapItem bmpitem = new BitmapItem();

                    using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                    {
                        loadfsh.Load(fs);

                        int dirnum = -1;
                        string[] dirname = new string[loadfsh.Bitmaps.Count];
                        short[] width = new short[loadfsh.Bitmaps.Count];
                        short[] height = new short[loadfsh.Bitmaps.Count];

                        foreach (FSHDirEntry dir in loadfsh.Directory)
                        {
                            dirnum++;
                            FSHEntryHeader entryhead = new FSHEntryHeader();
                            entryhead = loadfsh.GetEntryHeader(dir.offset);
                            width[dirnum] = entryhead.width;
                            height[dirnum] = entryhead.height;
                            dirname[dirnum] = Encoding.ASCII.GetString(dir.name);
                        }


                        string tgistr = filename + ".TGI";
                        if (File.Exists(tgistr))
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
                                        if (line.Equals("7ab50e44", StringComparison.InvariantCultureIgnoreCase))
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
                        if (filename.Contains("noblend"))
                        {
                            blendchecked = false;
                        }

                        Gimp.Image image = new Gimp.Image(width[0], height[0], ImageBaseType.Rgb);

                        if (loadfsh.Bitmaps.Count > 1)
                        {
                            for (int cnt = 0; cnt < loadfsh.Bitmaps.Count; cnt++)
                            {
                                bmpitem = (BitmapItem)loadfsh.Bitmaps[cnt];
                                Bitmap bitmap = new Bitmap(bmpitem.Bitmap);
                                Bitmap alpha = null;
                                if (bmpitem.BmpType == FSHBmpType.TwentyFourBit)
                                {
                                    alpha = new Bitmap(bitmap.Width, bitmap.Height);
                                    for (int y = 0; y < alpha.Height; y++)
                                    {
                                        for (int x = 0; x < alpha.Width; x++)
                                        {
                                            alpha.SetPixel(x, y, Color.White);
                                        }
                                    }
                                }
                                else
                                {
                                    alpha = new Bitmap(bmpitem.Alpha);
                                }
                                buildlayer(image, cnt, bitmap, alpha, width[cnt], height[cnt], blendchecked);
                            }
                        }
                        else
                        {
                            bmpitem = (BitmapItem)loadfsh.Bitmaps[0];
                            Bitmap bitmap = new Bitmap(bmpitem.Bitmap);
                            Bitmap alpha = null;
                            if (bmpitem.BmpType == FSHBmpType.TwentyFourBit)
                            {
                                alpha = new Bitmap(bitmap.Width, bitmap.Height);
                                for (int y = 0; y < alpha.Height; y++)
                                {
                                    for (int x = 0; x < alpha.Width; x++)
                                    {
                                        alpha.SetPixel(x, y, Color.White);
                                    }
                                }
                            }
                            else
                            {
                                alpha = new Bitmap(bmpitem.Alpha);
                            }
                            buildlayer(image, 0, bitmap, alpha, width[0], height[0], blendchecked);
                        }


                        image.Filename = filename;

                        return image;
                    }
                }
                catch (Exception ex)
                {
                    // Console.WriteLine(ex.Message);
                    ErrorDlg("Error loading fsh", ex.Message, ex.StackTrace).Run();
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
        private void buildlayer(Gimp.Image image, int layerpos,Bitmap fshbmp,Bitmap fshalpha, short fshwidth, short fshheight, bool alphablend)
        {
            Layer bglayer = new Layer(image, "Fsh Bitmap" + layerpos.ToString(), Gimp.ImageType.Rgba);
            image.AddLayer(bglayer, layerpos);
            if (alphablend)
            {
                PixelRgn rgn = new PixelRgn(image.Layers[layerpos], true, false);
                bglayer.AddAlpha();
                Bitmap destbmp = new Bitmap(fshbmp);
                BitmapData bdata = fshbmp.LockBits(new System.Drawing.Rectangle(0, 0, fshbmp.Width, fshbmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                BitmapData aldata = fshalpha.LockBits(new System.Drawing.Rectangle(0, 0, fshalpha.Width, fshalpha.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
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
                Bitmap destbmp = new Bitmap(fshbmp);
                BitmapData bdata = fshbmp.LockBits(new System.Drawing.Rectangle(0, 0, fshbmp.Width, fshbmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                BitmapData destdata = destbmp.LockBits(new System.Drawing.Rectangle(0, 0, destbmp.Width, destbmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                IntPtr srcscan0 = bdata.Scan0;
                IntPtr destscan0 = destdata.Scan0;
                int bytes = bdata.Stride * bdata.Height;
                byte[] tmpdata = new byte[bytes];

                unsafe
                {
                    int offset = bdata.Stride - bdata.Width * 3;
                    int destoffset = destdata.Stride - destdata.Width * 3;
                    byte* dest = (byte*)destscan0.ToPointer();
                    byte* src = (byte*)srcscan0.ToPointer();

                    for (int pPixel = 0; pPixel < destbmp.Width * destbmp.Height; pPixel++)
                    {
                        for (int iBGR = 0; iBGR < 3; iBGR++)
                        {
                            dest[0] = src[2]; // red
                            dest[1] = src[1]; // green
                            dest[2] = src[0]; // blue
                        }
                        src += 4;
                        dest += 4;
                    }
                    Marshal.Copy(destscan0, tmpdata, 0, bytes);
                }
                fshbmp.UnlockBits(bdata);
                destbmp.UnlockBits(destdata);
                rgn.SetRect(tmpdata, 0, 0, fshwidth, fshheight);
            }
        }
        private void WriteTgi(string filename, int zoom)
        {
            char endreg = Convert.ToChar("");
            char end64 = Convert.ToChar("");
            char end32 = Convert.ToChar("");
            char end16 = Convert.ToChar("");
            char end8 = Convert.ToChar("");
            try
            {
                using (FileStream fs = new FileStream(filename + ".TGI", FileMode.OpenOrCreate, FileAccess.Write))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {

                        if (instanceid.ToUpper().EndsWith("E") || instanceid.ToUpper().EndsWith("D") || instanceid.ToUpper().EndsWith("C") || instanceid.ToUpper().EndsWith("B") || instanceid.ToUpper().EndsWith("A"))
                        {
                            endreg = Convert.ToChar("E");
                            end64 = Convert.ToChar("D");
                            end32 = Convert.ToChar("C");
                            end16 = Convert.ToChar("B");
                            end8 = Convert.ToChar("A");
                        }
                        else if (instanceid.ToUpper().EndsWith("9") || instanceid.ToUpper().EndsWith("8") || instanceid.ToUpper().EndsWith("7") || instanceid.ToUpper().EndsWith("6") || instanceid.ToUpper().EndsWith("5"))
                        {
                            endreg = Convert.ToChar("9");
                            end64 = Convert.ToChar("8");
                            end32 = Convert.ToChar("7");
                            end16 = Convert.ToChar("6");
                            end8 = Convert.ToChar("5");
                        }
                        else if (instanceid.ToUpper().EndsWith("0") || instanceid.ToUpper().EndsWith("1") || instanceid.ToUpper().EndsWith("2") || instanceid.ToUpper().EndsWith("3") || instanceid.ToUpper().EndsWith("4"))
                        {
                            endreg = Convert.ToChar("4");
                            end64 = Convert.ToChar("3");
                            end32 = Convert.ToChar("2");
                            end16 = Convert.ToChar("1");
                            end8 = Convert.ToChar("0");
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
                Assembly.GetAssembly(typeof(Fsh)).GetManifestResourceStream("GimpsharpFsh.GimpsharpFsh.xml");
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
        /// <param name="hd">Is the Hd bitmap type enabled</param>
        private void savealphadata(Bitmap sourcebmp, BitmapItem bmpitem, int fshtype, bool hd)
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
            if (hd)
            {
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
            }
            else
            {
                switch (fshtype)
                {
                    case 0:
                        bmpitem.BmpType = FSHBmpType.DXT1;
                        break;
                    case 1:
                        bmpitem.BmpType = FSHBmpType.DXT3;
                        break;
                }
            }
            #if DEBUG
            alphamap.Save(@"C:\Dev_projects\sc4\gimpsharpfsh\Gimpsharpfsh\bin\Debug\alphamap.png", ImageFormat.Png);
            #endif
           
        }

        private ComboBox combo = null;
        private CheckButton mipbtn = null;
        /// <summary>
        /// Create a Fsh save dialog
        /// </summary>
        /// <param name="hd">is the image the size for hd fsh</param>
        /// <param name="mipenabled">enable the generate mips checkbox</param>
        /// <param name="mipchecked">check the generate mips checkbox</param>
        /// <returns></returns>
        protected GimpDialog CreateSaveDialog(bool hd, int cboindex, bool mipenabled, bool mipchecked)
        {
            gimp_ui_init("Fsh Save", false);
            GimpDialog dialog = new GimpDialog("Fsh Save", "Fsh Save", IntPtr.Zero, DialogFlags.Modal, null, null,"Ok",ResponseType.Ok,"Cancel",ResponseType.Cancel);
            VBox box1 = new VBox(false, 6) {BorderWidth = 6};
            dialog.VBox.PackStart(box1, true, true, 6);
            combo = ComboBox.NewText();
            if (hd)
            {
                combo.AppendText("Hd Fsh");
                combo.AppendText("Hd base Fsh");
            }
            combo.AppendText("Dxt1");
            combo.AppendText("Dxt3");
            combo.Active = cboindex;
            box1.PackStart(combo, true, false, 3);
            mipbtn = new CheckButton("Generate Mipmaps");
            mipbtn.Active = mipchecked;
            mipbtn.Visible = mipenabled;
            box1.PackStart(mipbtn,true,false,3);
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
           // Debugger.Launch();
#endif
            try
            {
                LoadSettings();
                bool mipenabled = false;
                bool mipchecked = false;
                bool hd = false;
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
                hd = (image.Width >= 256 && image.Height >= 256) ? true : false; // is the image hd size
                int selindex = 0;
                if (GetAlphaType(image)) // is the alpha dxt3 or 32-bit?
                {
                    if (hd)
                    {
                        selindex = 0; // 32-bit RGBA
                    }
                    else
                    {
                        selindex = 3; // Dxt3
                    }
                }
                else
                {
                    if (hd)
                    {
                        selindex = 0; // 24-bit RGB
                    }
                    else
                    {
                        selindex = 1; // Dxt1
                    }
                }
                GimpDialog dlg = CreateSaveDialog(hd,selindex, mipenabled, mipchecked);
                if (dlg.Run() == ResponseType.Ok)
                {
                    settings.PutSetting("savedlg/typeSelected", combo.Active);
                    settings.PutSetting("savedlg/mipchecked", mipbtn.Active.ToString());


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

                            savealphadata(tempbmp, multiitem, combo.Active, hd);
                            if (mipbtn.Active)
                            {
                                Generatemips(multiitem);
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
                        savealphadata(tempbmp, bmpitem, combo.Active, hd);
                       
                        if (mipbtn.Active == true)
                        {
                            Generatemips(bmpitem);
                        }
                        saveimg.Bitmaps.Add(bmpitem);
                    }
                    saveimg.UpdateDirty();
                    if (System.IO.Path.GetExtension(filename).Equals(".qfs"))
                    {
                        saveimg.IsCompressed = true;
                    }
                    else
                    {
                        saveimg.IsCompressed = false;
                    }
                    
                    using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        SaveFsh(fs, saveimg);
                    }
                    if (groupid != null && instanceid != null)
                    {
                        WriteTgi(filename + ".TGI", 4);
                    }
                    if (mipbtn.Active)
                    {
                        string filepath = null;
                        for (int i = 3; i >= 0; i--)
                        {
                            if (mipimgs[i] != null)
                            {

                                filepath = GetFileName(filename, "_s" + i.ToString());
                                using (FileStream fstream = new FileStream(filepath, FileMode.OpenOrCreate, FileAccess.Write))
                                {
                                    mipimgs[i].IsCompressed = saveimg.IsCompressed;
                                    SaveFsh(fstream, mipimgs[i]);
                                }
                                if (groupid != null && instanceid != null)
                                {
                                    WriteTgi(filepath + ".TGI", i);
                                }
                            }

                        }

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
                ErrorDlg("Error saving fsh", ex.Message, ex.StackTrace).Run();
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
                if (IsDXTFsh(image))
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

        private bool GetAlphaType(Gimp.Image image)
        {
            bool dxt3alpha = false; 
            if (!image.Layers[0].HasAlpha)
            {
                image.Layers[0].AddAlpha();
            }
            Layer layer = image.Layers[0];
            PixelRgn pxlrgn = new PixelRgn(layer, false, false);
            byte[] buf = new byte[layer.Width * layer.Height * layer.Bpp];
            buf = pxlrgn.GetRect(0, 0, layer.Width, layer.Height);

            using (Bitmap tempbmp = BitsToBitmapRGB32(buf, image.Width, image.Height))
            {
                if (tempbmp.GetPixel(0, 0).ToArgb() == Color.Black.ToArgb())
                {
                    dxt3alpha = true;
                }
                else
                {
                    dxt3alpha = false;
                }
            }
            return dxt3alpha;
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
           private void Generatemips(BitmapItem bmpitem)
           {
               if (bmpitem.Bitmap.Width >= 128 && bmpitem.Bitmap.Height >= 128)
               {
                   
                   Bitmap[] bmps = new Bitmap[4];
                   Bitmap[] alphas = new Bitmap[4];

                   // 0 = 8, 1 = 16, 2 = 32, 3 = 64
                   System.Drawing.Image.GetThumbnailImageAbort abort = new System.Drawing.Image.GetThumbnailImageAbort(thabort);
                   if (bmpitem.Bitmap.Width >= 128 && bmpitem.Bitmap.Height >= 128)
                   {
                       
                       Bitmap bmp = new Bitmap(bmpitem.Bitmap);
                       bmps[0] = (Bitmap)bmp.GetThumbnailImage(8, 8, abort, IntPtr.Zero);
                       bmps[1] = (Bitmap)bmp.GetThumbnailImage(16, 16, abort, IntPtr.Zero);
                       bmps[2] = (Bitmap)bmp.GetThumbnailImage(32, 32, abort, IntPtr.Zero);
                       bmps[3] = (Bitmap)bmp.GetThumbnailImage(64, 64, abort, IntPtr.Zero);
                       //alpha
                       Bitmap alpha = new Bitmap(bmpitem.Alpha);
                       alphas[0] = (Bitmap)alpha.GetThumbnailImage(8, 8, abort, IntPtr.Zero);
                       alphas[1] = (Bitmap)alpha.GetThumbnailImage(16, 16, abort, IntPtr.Zero);
                       alphas[2] = (Bitmap)alpha.GetThumbnailImage(32, 32, abort, IntPtr.Zero);
                       alphas[3] = (Bitmap)alpha.GetThumbnailImage(64, 64, abort, IntPtr.Zero);
                   
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
                               if (alphas[i].GetPixel(0, 0).ToArgb() == Color.Black.ToArgb())
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
                                   mipimgs[i].UpdateDirty();
                               }
                           }
                       }
                   }
               }
           }
           private bool thabort()
           {
               return false;
           }
           private string GetFileName(string path, string add)
           {
               return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path) + System.IO.Path.DirectorySeparatorChar, System.IO.Path.GetFileNameWithoutExtension(path) + add + System.IO.Path.GetExtension(path));
           }
        }
}
