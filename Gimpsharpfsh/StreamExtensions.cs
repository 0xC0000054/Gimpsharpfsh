using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace GimpsharpFsh
{
    static class StreamExtensions
    {
        /// <summary>
        /// Reads a byte from the Stream and advances the read position by one byte 
        /// </summary>
        /// <returns>The byte read or throws an EndOfStreamException if the stream end has been reached</returns>
        /// <exception cref="System.IO.EndOfStreamException">The end of the stream is reached.</exception>
        public static byte ReadByte2(this Stream s)
        {
            int val = s.ReadByte();

            if (val == -1)
            {
                throw new EndOfStreamException();
            }

            return (byte)val;
        }

        public static int ReadInt32(this Stream s)
        {
            int byte0 = s.ReadByte();

            if (byte0 == -1)
            {
                throw new EndOfStreamException();
            }
            int byte1 = s.ReadByte();

            if (byte1 == -1)
            {
                throw new EndOfStreamException();
            }
            int byte2 = s.ReadByte();

            if (byte2 == -1)
            {
                throw new EndOfStreamException();
            }
            int byte3 = s.ReadByte();

            if (byte3 == -1)
            {
                throw new EndOfStreamException();
            }

            return (int)((byte3 << 24) | (byte2 << 16) | (byte1 << 8) | byte0);
        }

        public static short ReadInt16(this Stream s)
        {
            int byte0 = s.ReadByte();

            if (byte0 == -1)
            {
                throw new EndOfStreamException();
            }
            int byte1 = s.ReadByte();

            if (byte1 == -1)
            {
                throw new EndOfStreamException();
            }

            return (short)((byte1 << 8) | byte0);
        }
        public static int ProperRead(this Stream s, byte[] bytes, int count)
        {
            // Now read s into a byte buffer.
            int numBytesToRead = count;
            int numBytesRead = 0;
            while (numBytesToRead > 0)
            {
                // Read may return anything from 0 to numBytesToRead.
                int n = s.Read(bytes, numBytesRead, numBytesToRead);
                // The end of the file is reached.
                if (n == 0)
                    break;
                numBytesRead += n;
                numBytesToRead -= n;
            }

            return numBytesRead;
        }
    }
}
