// Ported from HlslDecompiler: FourCC utility
// ...full implementation from HlslDecompiler/Util/FourCC.cs...
using System.Text;

namespace DXDecompiler.DX9Shader.Decompiler.Util
{
    public static class FourCC
    {
        public static string ToString(uint fourCC)
        {
            var bytes = new byte[4];
            bytes[0] = (byte)(fourCC & 0xFF);
            bytes[1] = (byte)((fourCC >> 8) & 0xFF);
            bytes[2] = (byte)((fourCC >> 16) & 0xFF);
            bytes[3] = (byte)((fourCC >> 24) & 0xFF);
            return Encoding.ASCII.GetString(bytes);
        }

        public static uint FromString(string str)
        {
            if (str.Length != 4)
                throw new System.ArgumentException("FourCC string must be 4 characters long.");
            var bytes = Encoding.ASCII.GetBytes(str);
            return (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
        }
    }
}
