using System.Collections.Generic;
using DXDecompiler.DX9Shader.Bytecode;

namespace DXDecompiler.DX9Shader.Decompiler
{
	public class RegisterInputNode : HlslTreeNode, IHasComponentIndex
	{
		public RegisterInputNode(RegisterComponentKey registerComponentKey, int samplerTextureDimension = 0)
		{
			RegisterComponentKey = registerComponentKey;
			SamplerTextureDimension = samplerTextureDimension;
		}

		public RegisterComponentKey RegisterComponentKey { get; }
		public int SamplerTextureDimension { get; }

		public int ComponentIndex => RegisterComponentKey.ComponentIndex;

		public override string ToString()
		{
			return RegisterComponentKey.ToString();
		}

		public override string ToHlsl()
		{
			return ToHlsl(new HashSet<HlslTreeNode>(), 0);
		}
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (depth > 128)
				return "/*max depth reached*/";
			if (!visited.Add(this))
				return "/*cycle detected*/";
			var regType = RegisterComponentKey.Type;
			var regNum = RegisterComponentKey.Number;
			var comp = "";
			switch (RegisterComponentKey.ComponentIndex)
			{
				case 0: comp = ".x"; break;
				case 1: comp = ".y"; break;
				case 2: comp = ".z"; break;
				case 3: comp = ".w"; break;
				default: comp = ""; break;
			}
			// Map common DX9 input registers to HLSL semantic names
			string semantic = regType switch
			{
				RegisterType.Input => regNum switch
				{
					0 => "i.position",
					1 => "i.normal",
					2 => "i.color",
					3 => "i.texcoord",
					_ => $"i.input{regNum}"
				},
				RegisterType.Texture => $"i.texcoord{regNum}",
				RegisterType.ColorOut => $"i.color{regNum}",
				RegisterType.Temp => $"temp{regNum}",
				_ => $"i.input{regNum}"
			};
			visited.Remove(this);
			return semantic + comp;
		}
	}
}
