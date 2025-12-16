using System.Linq;
using DXDecompiler.DX9Shader.Decompiler;
using DXDecompiler.DX9Shader.Decompiler.Util;

namespace DXDecompiler.DX9Shader.Decompiler.Compiler
{
	public sealed class ConstantCompiler
	{
		private readonly NodeGrouper _nodeGrouper;

		public ConstantCompiler(NodeGrouper nodeGrouper)
		{
			_nodeGrouper = nodeGrouper;
		}

		public string Compile(ConstantNode[] group)
		{
			ConstantNode first = group[0];

			int count = group.Length;
			if(count == 1)
			{
				return CompileConstant(first);
			}

			if(group.All(c => NodeGrouper.AreNodesEquivalent(c, first)))
			{
				return CompileConstant(first);
			}

			string components = string.Join(", ", group.Select(CompileConstant));
			return $"float{count}({components})";
		}

		private string CompileConstant(ConstantNode firstConstant)
		{
			return ConstantFormatter.FormatFloat(firstConstant.Value);
		}
	}
}
