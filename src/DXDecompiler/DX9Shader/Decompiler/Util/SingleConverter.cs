using System;

namespace DXDecompiler.DX9Shader.Decompiler.Util
{
    public static class SingleConverter
    {
        public static float UIntToFloat(uint value)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(value), 0);
        }

        public static uint FloatToUInt(float value)
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
        }
    }
}
