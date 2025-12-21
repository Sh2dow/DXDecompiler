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
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			if (depth > 1024)
			{
				var maxDepthResult = $"/* ERROR: Max recursion depth reached in NormalizeOutputNode */";
				SetCached(this, maxDepthResult);
				return maxDepthResult;
			}
			if (!visited.Add(this))
			{
				var cycleResult = $"/* ERROR: Cycle detected in NormalizeOutputNode */";
				SetCached(this, cycleResult);
				return cycleResult;
			}
			// Output a placeholder for normalization, or try to output the input if possible
			string inputStr = Inputs.Count > 0 ? Inputs[0]?.ToHlsl(visited, depth + 1) ?? "null" : "null";
			visited.Remove(this);
			var result = $"normalize({inputStr})";
			SetCached(this, result);
			return result;
		}
	}
}
