namespace DXDecompiler.DX9Shader.Decompiler
{
	public class TempValueNode : HlslTreeNode
	{
		public TempValueNode(string name)
		{
			Name = name;
		}

		public string Name { get; }

		public override string ToHlsl()
		{
			return Name;
		}

		public override string ToHlsl(System.Collections.Generic.HashSet<HlslTreeNode> visited, int depth)
		{
			return Name;
		}
	}
}
