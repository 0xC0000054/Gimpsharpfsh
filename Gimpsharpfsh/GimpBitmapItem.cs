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
