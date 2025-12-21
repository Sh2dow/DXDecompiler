using System;
using System.Collections.Generic;
using System.Linq;

namespace DXDecompiler.DX9Shader.Decompiler.Operations
{
	public abstract class Operation : HlslTreeNode
	{
		private const int MaxToHlslDepth = 1024;
		public abstract string Mnemonic { get; }

		public override string ToString()
		{
			string parameters = string.Join(", ", Inputs.Select(c => c.ToString()));
			return $"{Mnemonic}({parameters})";
		}

		public override string ToHlsl() => ToHlsl(new HashSet<HlslTreeNode>(), 0);

		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			if (depth > MaxToHlslDepth)
			{
				var maxDepthResult = $"/* ERROR: Max recursion depth ({MaxToHlslDepth}) reached in {GetType().Name} */";
				SetCached(this, maxDepthResult);
				return maxDepthResult;
			}
			if (!visited.Add(this))
			{
				var cycleResult = $"/* ERROR: Cycle detected in {GetType().Name} */";
				SetCached(this, cycleResult);
				return cycleResult;
			}

			string result;
			try
			{
				var args = Inputs
					.Select(input => input?.ToHlsl(visited, depth + 1) ?? "null")
					.ToList();
				result = Mnemonic switch
				{
					"add" => $"({args[0]} + {args[1]})",
					"mul" => $"({args[0]} * {args[1]})",
					"sub" => $"({args[0]} - {args[1]})",
					"mad" => $"(({args[0]} * {args[1]}) + {args[2]})",
					"min" => $"min({args[0]}, {args[1]})",
					"max" => $"max({args[0]}, {args[1]})",
					"abs" => $"abs({args[0]})",
					"frc" => $"frac({args[0]})",
					"pow" => $"pow({args[0]}, {args[1]})",
					"rcp" => $"(1.0 / {args[0]})",
					"rsq" => $"rsqrt({args[0]})",
					"lrp" => $"lerp({args[2]}, {args[1]}, {args[0]})",
					"cmp" => $"(({args[0]} >= 0) ? {args[1]} : {args[2]})",
					"sge" => $"(({args[0]} >= {args[1]}) ? 1 : 0)",
					"slt" => $"(({args[0]} < {args[1]}) ? 1 : 0)",
					"cos" => $"cos({args[0]})",
					"sin" => $"sin({args[0]})",
					"sqrt" => $"sqrt({args[0]})",
					_ => $"{Mnemonic}({string.Join(", ", args)})"
				};
			}
			catch (Exception ex)
			{
				result = $"/* ERROR: Exception in {GetType().Name}.ToHlsl: {ex.Message} */";
			}

			visited.Remove(this);
			SetCached(this, result);
			return result;
		}
	}
}
