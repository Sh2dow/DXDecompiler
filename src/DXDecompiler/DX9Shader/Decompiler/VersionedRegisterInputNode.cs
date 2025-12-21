using System.Collections.Generic;
using DXDecompiler.DX9Shader.Bytecode;

namespace DXDecompiler.DX9Shader.Decompiler
{
	public class VersionedRegisterInputNode : HlslTreeNode, IHasComponentIndex
	{
		public VersionedRegisterInputNode(RegisterKey registerKey, int version, int componentIndex)
		{
			RegisterKey = registerKey;
			Version = version;
			ComponentIndex = componentIndex;
		}

		public RegisterKey RegisterKey { get; }
		public int Version { get; }
		public int ComponentIndex { get; }

		public override string ToHlsl()
		{
			return ToHlsl(new HashSet<HlslTreeNode>(), 0);
		}

		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (depth > 1024)
			{
				return "/*max depth reached*/";
			}
			if (!visited.Add(this))
			{
				return "/*cycle detected*/";
			}
			var comp = ComponentIndex switch
			{
				0 => ".x",
				1 => ".y",
				2 => ".z",
				3 => ".w",
				_ => string.Empty
			};
			var name = $"{RegisterKey}_{Version}{comp}";
			visited.Remove(this);
			return name;
		}
	}
}
