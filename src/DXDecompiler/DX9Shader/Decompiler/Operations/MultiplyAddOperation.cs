using System.Collections.Generic;

namespace DXDecompiler.DX9Shader.Decompiler.Operations
{
	public class MultiplyAddOperation : Operation
	{
		public MultiplyAddOperation(HlslTreeNode factor1, HlslTreeNode factor2, HlslTreeNode addend)
		{
			AddInput(factor1);
			AddInput(factor2);
			AddInput(addend);
		}

		public HlslTreeNode Factor1 => Inputs[0];
		public HlslTreeNode Factor2 => Inputs[1];
		public HlslTreeNode Addend => Inputs[2];

		public override string Mnemonic => "madd";

		public override HlslTreeNode Reduce()
		{
			Factor1.Outputs.Remove(this);
			Factor2.Outputs.Remove(this);
			var multiplication = new MultiplyOperation(Factor1, Factor2);

			var addition = new AddOperation(multiplication, Addend);
			Replace(addition);

			return addition.Reduce();
		}
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			if (depth > 1024)
			{
				var maxDepthResult = $"/* ERROR: Max recursion depth reached in MultiplyAddOperation */";
				SetCached(this, maxDepthResult);
				return maxDepthResult;
			}
			if (!visited.Add(this))
			{
				var cycleResult = $"/* ERROR: Cycle detected in MultiplyAddOperation */";
				SetCached(this, cycleResult);
				return cycleResult;
			}
			string f1 = Factor1?.ToHlsl(visited, depth + 1) ?? "null";
			string f2 = Factor2?.ToHlsl(visited, depth + 1) ?? "null";
			string add = Addend?.ToHlsl(visited, depth + 1) ?? "null";
			visited.Remove(this);
			var result = $"({f1} * {f2} + {add})";
			SetCached(this, result);
			return result;
		}
	}
}
