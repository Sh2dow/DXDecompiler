﻿using System.Collections.Generic;

namespace DXDecompiler.DX9Shader.Decompiler
{
	public class NormalizeOutputNode : HlslTreeNode, IHasComponentIndex
	{
		public NormalizeOutputNode(IEnumerable<HlslTreeNode> inputs, int componentIndex)
		{
			foreach(HlslTreeNode input in inputs)
			{
				AddInput(input);
			}

			ComponentIndex = componentIndex;
		}

		public int ComponentIndex { get; }

		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (depth > 1024)
			{
				return $"/* ERROR: Max recursion depth reached in NormalizeOutputNode */";
			}
			if (!visited.Add(this))
			{
				return $"/* ERROR: Cycle detected in NormalizeOutputNode */";
			}
			// Output a placeholder for normalization, or try to output the input if possible
			string inputStr = Inputs.Count > 0 ? Inputs[0]?.ToHlsl(visited, depth + 1) ?? "null" : "null";
			visited.Remove(this);
			return $"normalize({inputStr})";
		}
	}
}
