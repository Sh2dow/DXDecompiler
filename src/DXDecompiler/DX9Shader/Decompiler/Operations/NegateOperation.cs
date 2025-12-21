using System.Collections.Generic;

namespace DXDecompiler.DX9Shader.Decompiler.Operations
{
	public class NegateOperation : UnaryOperation
	{
		public NegateOperation(HlslTreeNode value)
		{
			AddInput(value);
		}

		public override string Mnemonic => "-";

		public override HlslTreeNode Reduce()
		{
			if(Value is NegateOperation negate)
			{
				HlslTreeNode newValue = negate.Value;
				Replace(newValue);
				return newValue;
			}
			if(Value is ConstantNode constant)
			{
				var newValue = new ConstantNode(-constant.Value);
				Replace(newValue);
				return newValue;
			}
			return base.Reduce();
		}
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			if (depth > 1024)
			{
				var maxDepthResult = $"/* ERROR: Max recursion depth reached in NegateOperation */";
				SetCached(this, maxDepthResult);
				return maxDepthResult;
			}
			if (!visited.Add(this))
			{
				var cycleResult = $"/* ERROR: Cycle detected in NegateOperation */";
				SetCached(this, cycleResult);
				return cycleResult;
			}
			string val = Value?.ToHlsl(visited, depth + 1) ?? "null";
			visited.Remove(this);
			var result = $"(-{val})";
			SetCached(this, result);
			return result;
		}
	}
}
