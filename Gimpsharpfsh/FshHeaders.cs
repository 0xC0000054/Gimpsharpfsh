using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GimpsharpFsh
{
    internal struct FSHHeader
    {
        public byte[] SHPI;
        public int size;
        public int numBmps;
        public byte[] dirID;
    }

    internal struct FSHDirEntry
    {
        public byte[] name;
        public int offset;

        internal FSHDirEntry(byte[] name)
        {
            this.name = name;
            this.offset = 0;
        }

    }

    internal struct FSHEntryHeader
    {
        public int code;
        public ushort width;
        public ushort height;
        public ushort[] misc;

        internal FSHEntryHeader(System.IO.Stream stream)
        {
            this.code = stream.ReadInt32();
            this.width = stream.ReadUInt16();
            this.height = stream.ReadUInt16();
            this.misc = new ushort[4];
            for (int m = 0; m < 4; m++)
            {
                this.misc[m] = stream.ReadUInt16();
            }
        }
    }
    
}
