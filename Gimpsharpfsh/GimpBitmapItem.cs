using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GimpsharpFsh
{
 
    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    internal class GimpBitmapItem
    {
        private byte[] imageBytes;
        private int width;
        private int height;
        private FshFileFormat format;

        public byte[] ImageData
        {
            get
            {
                return imageBytes;
            }
            set
            {
                imageBytes = value;
            }
        }

        public int Width
        {
            get
            {
                return width;
            }
        }

        public int Height
        {
            get
            {
                return height;
            }
        }

        public FshFileFormat BmpType
        {
            get
            {
                return format;
            }
        }

 

        public GimpBitmapItem(int width, int height, FshFileFormat format)
        {
            this.imageBytes = null;
            this.width = width;
            this.height = height;
            this.format = format;
        }

    }
}
