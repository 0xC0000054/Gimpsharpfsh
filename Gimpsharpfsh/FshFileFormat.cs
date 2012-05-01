using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GimpsharpFsh
{
    internal enum FshFileFormat 
    {
        /// <summary>
        /// 24-bit RGB (0:8:8:8)
        /// </summary>
        TwentyFourBit = 0x7f,
        /// <summary>
        /// 32-bit ARGB (8:8:8:8)
        /// </summary>
        ThirtyTwoBit = 0x7d,
        /// <summary>
        /// 16-bit RGB (0:5:5:5)
        /// </summary>
        SixteenBit = 0x78,
        /// <summary>
        /// 16-bit ARGB (1:5:5:5)
        /// </summary>
        SixteenBitAlpha = 0x7e,
        /// <summary>
        /// 16-bit ARGB (4:4:4:4)
        /// </summary>
        SixteenBit4x4 = 0x6d,
        /// <summary>
        /// DXT1 4x4 block compression  
        /// </summary>
        DXT1 = 0x60,
        /// <summary>
        /// DXT3 4x4 block compression  
        /// </summary>
        DXT3 = 0x61
    }


}
