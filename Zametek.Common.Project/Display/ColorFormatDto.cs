using System;

namespace Zametek.Common.Project
{
    [Serializable]
    public class ColorFormatDto
    {
        public byte A { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
    }
}
