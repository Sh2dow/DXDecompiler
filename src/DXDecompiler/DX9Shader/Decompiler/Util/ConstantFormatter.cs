// Ported from HlslDecompiler: ConstantFormatter utility
// ...full implementation from HlslDecompiler/Util/ConstantFormatter.cs...

using System.Globalization;

namespace DXDecompiler.DX9Shader.Decompiler.Util
{
    public static class ConstantFormatter
    {
        public static string FormatFloat(float value)
        {
            if (float.IsNaN(value))
                return "nan";
            if (float.IsPositiveInfinity(value))
                return "inf";
            if (float.IsNegativeInfinity(value))
                return "-inf";
            // Use round-trip format for precision
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        public static string FormatInt(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
