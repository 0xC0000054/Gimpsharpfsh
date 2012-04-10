
namespace GimpsharpFsh
{
    /// <summary>
    /// DXT Decompression code based off Simon Brown's Squish library 
    /// http://code.google.com/p/libsquish/
    /// </summary>
    static class DXTComp
    {
        /// <summary>
        /// Unpacks the DXT compression.
        /// </summary>
        /// <param name="blocks">The compressed blocks.</param>
        /// <param name="width">The width of the final image.</param>
        /// <param name="height">The height of the final image.</param>
        /// <param name="dxt1">set to <c>true</c> if the image is DXT1.</param>
        /// <returns>The decompressed pixels.</returns>
        public static unsafe byte[] UnpackDXT(byte[] blocks, int width, int height, bool dxt1)
        {
            byte[] pixelData = new byte[(width * height) * 4];

            fixed (byte* rgba = pixelData)
            {
                fixed (byte* pBlocks = blocks) // fix the array in place
                {
                    int bytesPerBlock = dxt1 ? 8 : 16;
                    byte* targetRGBA = stackalloc byte[4 * 16];
                    byte* pBlock = pBlocks;
                    byte* sourcePixel; // define the pointers outside the loop to help performance
                    byte* targetPixel;
                    for (int y = 0; y < height; y += 4)
                    {
                        for (int x = 0; x < width; x += 4)
                        {
                            // decompress the block.
                            Decompress(targetRGBA, pBlock, dxt1);

                            // write the decompressed pixels to the correct image locations
                            sourcePixel = targetRGBA;
                            for (int py = 0; py < 4; py++)
                            {
                                for (int px = 0; px < 4; px++)
                                {
                                    // get the target location
                                    int sx = x + px;
                                    int sy = y + py;

                                    if (sy < width && sy < height)
                                    {
                                        targetPixel = rgba + 4 * ((width * sy) + sx);

                                        for (int p = 0; p < 4; p++)
                                        {
                                            *targetPixel++ = *sourcePixel++; // copy the target value
                                        }
                                    }
                                    else
                                    {
                                        // skip the pixel as its outside the range
                                        sourcePixel += 4;
                                    }
                                }
                            }

                            pBlock += bytesPerBlock;
                        }
                    }
                }
            }

            return pixelData;
        }

        /// <summary>
        /// Decompresses the DXT compressed block.
        /// </summary>
        /// <param name="rgba">The output rgba data.</param>
        /// <param name="block">The compressed block.</param>
        /// <param name="isDxt1">set to <c>true</c> if the image is DXT1.</param>
        private static unsafe void Decompress(byte* rgba, byte* block, bool isDxt1)
        {
            byte* colorBlock = block;
            byte* alphaBlock = block;

            if (isDxt1)
            {
                DecompressColor(rgba, colorBlock, true);
            }
            else
            {
                colorBlock = block + 8;
                DecompressColor(rgba, colorBlock, false);
                DecompressDXT3Alpha(rgba, alphaBlock);
            }

        }

        /// <summary>
        /// Unpacks 565 packed color values.
        /// </summary>
        /// <param name="packed">The packed values.</param>
        /// <param name="colors">The unpacked colors.</param>
        /// <returns></returns>
        private static unsafe int Unpack565(byte* packed, byte* colors)
        {
            int value = packed[0] | (packed[1] << 8);

            byte red = (byte)((value >> 11) & 0x1f);
            byte green = (byte)((value >> 5) & 0x3f);
            byte blue = (byte)(value & 0x1f);

            colors[0] = (byte)((red << 3) | (red >> 2));
            colors[1] = (byte)((green << 2) | (green >> 4));
            colors[2] = (byte)((blue << 3) | (blue >> 2));
            colors[3] = 255;

            return value;
        }

        /// <summary>
        /// Decompresses the DXT color data.
        /// </summary>
        /// <param name="rgba">The output rgba data.</param>
        /// <param name="blocks">The compressed block.</param>
        /// <param name="isDxt1">Set to <c>true</c> if the image is DXT1 to handle it's alpha channel.</param>
        private static unsafe void DecompressColor(byte* rgba, byte* blocks, bool isDxt1)
        {
            byte* codes = stackalloc byte[16];

            int a = Unpack565(blocks, codes);
            int b = Unpack565(blocks + 2, codes + 4);

            // unpack the midpoints
            for (int i = 0; i < 3; i++)
            {
                int c = codes[i];
                int d = codes[4 + i];

                if (isDxt1 && a <= b) // dxt1 alpha is a special case
                {
                    codes[8 + i] = (byte)((c + d) / 2);
                    codes[12 + i] = 0;
                }
                else
                {
                    // handle the other mask cases from FSHTool.
                    if (a > b)
                    {
                        codes[8 + i] = (byte)((2 * c + d) / 3);
                        codes[12 + i] = (byte)((c + 2 * d) / 3);
                    }
                    else
                    {
                        codes[8 + i] = (byte)((c + d) / 2);
                        codes[12 + i] = (byte)((c + d) / 2);
                    }

                }
            }


            // fill in alpha for the intermediate values
            codes[8 + 3] = 255;
            codes[12 + 3] = (isDxt1 && a <= b) ? (byte)0 : (byte)255;

            byte* indices = stackalloc byte[16];

            for (int i = 0; i < 4; i++)
            {
                byte* ind = indices + 4 * i;
                byte packed = blocks[4 + i];

                ind[0] = (byte)(packed & 3);
                ind[1] = (byte)((packed >> 2) & 3);
                ind[2] = (byte)((packed >> 4) & 3);
                ind[3] = (byte)((packed >> 6) & 3);
            }
            // store out the colors
            for (int i = 0; i < 16; i++)
            {
                int offset = 4 * indices[i];
                int index = 4 * i;
                for (int j = 0; j < 4; j++)
                {
                    rgba[index + j] = codes[offset + j];
                }
            }
        }

        /// <summary>
        /// Decompresses the DXT3 compressed alpha.
        /// </summary>
        /// <param name="rgba">The output rgba values.</param>
        /// <param name="block">The compressed alpha block.</param>
        private static unsafe void DecompressDXT3Alpha(byte* rgba, byte* block)
        {
            for (int i = 0; i < 8; i++)
            {
                byte quant = block[i];

                // extract the values
                int lo = quant & 0x0f;
                int hi = quant & 0xf0;
                int index = 8 * i;
                // convert back up to bytes
                rgba[index + 3] = (byte)(lo | (lo << 4));
                rgba[index + 7] = (byte)(hi | (hi >> 4));
            }
        }
    }
}
