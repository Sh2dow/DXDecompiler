using DXDecompiler.DX9Shader.Bytecode;

namespace DXDecompiler.DX9Shader.Decompiler
{
	public class RegisterKey
	{
		public RegisterKey(RegisterType registerType, uint registerNumber)
		{
			Type = registerType;
			Number = registerNumber;
		}

		public uint Number { get; }
		public RegisterType Type { get; }
		public bool IsConstant =>
            Type == RegisterType.Const ||
            Type == RegisterType.ConstInt ||
            Type == RegisterType.Const2 ||
            Type == RegisterType.Const3 ||
            Type == RegisterType.Const4 ||
            Type == RegisterType.ConstBool;


		public override bool Equals(object obj)
		{
			if(!(obj is RegisterKey other))
			{
				return false;
			}
			return
				other.Number == Number &&
				other.Type == Type;
		}

		public override int GetHashCode()
		{
			int hashCode =
				Number.GetHashCode() ^
				Type.GetHashCode();
			return hashCode;
		}

		public override string ToString()
		{
			return $"{Type.ToString().ToLower()}{Number}";
		}
	}
}
