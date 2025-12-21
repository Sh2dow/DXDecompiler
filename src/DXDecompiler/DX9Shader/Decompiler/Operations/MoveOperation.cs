using System.Collections.Generic;

namespace DXDecompiler.DX9Shader.Decompiler.Operations
{
	public class MoveOperation : UnaryOperation
	{
		public MoveOperation(HlslTreeNode value)
		{
			AddInput(value);
		}

		public override string Mnemonic => "mov";

		public override HlslTreeNode Reduce()
		{
			return Value.Reduce();
		}

		public override string ToString()
		{
			return Value.ToString();
		}

		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			if (depth > 1024)
			{
				var maxDepthResult = $"/* ERROR: Max recursion depth (1024) reached in {GetType().Name} */";
				SetCached(this, maxDepthResult);
				return maxDepthResult;
			}
			if (!visited.Add(this))
			{
				var cycleResult = $"/* ERROR: Cycle detected in {GetType().Name} */";
				SetCached(this, cycleResult);
				return cycleResult;
			}

			var result = Value?.ToHlsl(visited, depth + 1) ?? "null";
			visited.Remove(this);
			SetCached(this, result);
			return result;
		}
	}
}
