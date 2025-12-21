using System;
using System.Collections.Generic;
using DXDecompiler.DX9Shader.Decompiler.Operations;
using System.Linq;
using System.Threading;

namespace DXDecompiler.DX9Shader.Decompiler
{
	public class HlslTreeNode
	{
		public IList<HlslTreeNode> Inputs { get; } = new List<HlslTreeNode>();
		public IList<HlslTreeNode> Outputs { get; } = new List<HlslTreeNode>();

		private static readonly ThreadLocal<Dictionary<HlslTreeNode, string>> ToHlslCache =
			new ThreadLocal<Dictionary<HlslTreeNode, string>>(() => new Dictionary<HlslTreeNode, string>());

		protected static bool TryGetCached(HlslTreeNode node, out string value)
		{
			return ToHlslCache.Value.TryGetValue(node, out value);
		}

		protected static void SetCached(HlslTreeNode node, string value)
		{
			ToHlslCache.Value[node] = value;
		}

		protected static void ClearCache()
		{
			ToHlslCache.Value.Clear();
		}

		public static void ClearToHlslCache()
		{
			ClearCache();
		}

		private const int MaxToHlslDepth = 1024; // Increased depth limit for complex shaders
		public virtual string ToHlsl()
		{
			ClearCache();
			return ToHlsl(new HashSet<HlslTreeNode>(), 0);
		}
		public virtual string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			if (depth > MaxToHlslDepth)
			{
				Console.WriteLine($"[HlslTreeNode] Max recursion depth reached in {GetType().Name} at depth {depth}");
				var maxDepthResult = $"/* ERROR: Max recursion depth ({MaxToHlslDepth}) reached in {GetType().Name} */";
				SetCached(this, maxDepthResult);
				return maxDepthResult;
			}
			if (!visited.Add(this))
			{
				Console.WriteLine($"[HlslTreeNode] Cycle detected in {GetType().Name} at depth {depth}");
				var cycleResult = $"/* ERROR: Cycle detected in {GetType().Name} */";
				SetCached(this, cycleResult);
				return cycleResult;
			}
			if (Inputs.Count == 0)
			{
				Console.WriteLine($"[HlslTreeNode] Unmapped leaf node: {GetType().Name} at depth {depth}");
				var unmappedResult = $"/* Unmapped leaf node: {GetType().Name} */";
				SetCached(this, unmappedResult);
				return unmappedResult;
			}
			string result;
			try
			{
				result = string.Join(", ", Inputs.Select(i => i?.ToHlsl(visited, depth + 1) ?? $"/*null:{GetType().Name}*/"));
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[HlslTreeNode] Exception in {GetType().Name}.ToHlsl: {ex.Message}");
				result = $"/* ERROR: Exception in {GetType().Name}.ToHlsl: {ex.Message} */";
			}
			visited.Remove(this);
			SetCached(this, result);
			return result;
		}

		public virtual HlslTreeNode Reduce()
		{
			for(int i = 0; i < Inputs.Count; i++)
			{
				Inputs[i] = Inputs[i].Reduce();
			}
			return this;
		}

		public void Replace(HlslTreeNode with)
		{
			foreach(var input in Inputs)
			{
				input.Outputs.Remove(this);
			}
			foreach(var output in Outputs)
			{
				for(int i = 0; i < output.Inputs.Count; i++)
				{
					if(output.Inputs[i] == this)
					{
						output.Inputs[i] = with;
					}
				}
				with.Outputs.Add(output);
			}
		}

		protected void AddInput(HlslTreeNode node)
		{
			if(node == null)
			{
				// Do not add null nodes as inputs/outputs
				return;
			}
			Inputs.Add(node);
			node.Outputs.Add(this);
			AssertLoopFree();
		}

		private void AssertLoopFree()
		{
			foreach(HlslTreeNode output in Outputs)
			{
				AssertLoopFree(output);
				if(this == output)
				{
					throw new InvalidOperationException();
				}
			}
		}

		private void AssertLoopFree(HlslTreeNode parent)
		{
			foreach(HlslTreeNode upperParent in parent.Outputs)
			{
				if(this == upperParent)
				{
					throw new InvalidOperationException();
				}
			}
		}
	}

	public class LitOperation : Operation
	{
		public LitOperation(HlslTreeNode input)
			: base()
		{
			AddInput(input);
		}
		public override string Mnemonic => "lit";
		public override string ToHlsl() => ToHlsl(new HashSet<HlslTreeNode>(), 0);
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			var result = $"lit({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "null"})";
			SetCached(this, result);
			return result;
		}
	}

	public class SignOperation : Operation
	{
		public SignOperation(HlslTreeNode input)
			: base()
		{
			AddInput(input);
		}
		public override string Mnemonic => "sign";
		public override string ToHlsl() => ToHlsl(new HashSet<HlslTreeNode>(), 0);
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			var result = $"sign({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "null"})";
			SetCached(this, result);
			return result;
		}
	}

	public class ExpPOperation : Operation
	{
		public ExpPOperation(HlslTreeNode input)
			: base()
		{
			AddInput(input);
		}
		public override string Mnemonic => "exp2";
		public override string ToHlsl() => ToHlsl(new HashSet<HlslTreeNode>(), 0);
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			var result = $"exp2({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "null"})";
			SetCached(this, result);
			return result;
		}
	}

	public class ExpOperation : Operation
	{
		public ExpOperation(HlslTreeNode input)
			: base()
		{
			AddInput(input);
		}
		public override string Mnemonic => "exp";
		public override string ToHlsl() => ToHlsl(new HashSet<HlslTreeNode>(), 0);
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			var result = $"exp({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "null"})";
			SetCached(this, result);
			return result;
		}
	}

	public class TexKillOperation : Operation
	{
		public TexKillOperation(HlslTreeNode input)
			: base()
		{
			AddInput(input);
		}
		public override string Mnemonic => "clip";
		public override string ToHlsl() => ToHlsl(new HashSet<HlslTreeNode>(), 0);
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			var result = $"clip({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "null"})";
			SetCached(this, result);
			return result;
		}
	}

	// Add/complete ToHlsl for common operations
	public class AddOperation : Operation
	{
		public AddOperation(HlslTreeNode left, HlslTreeNode right)
		{
			AddInput(left);
			AddInput(right);
		}
		public override string Mnemonic => "add";
		public override string ToHlsl() => ToHlsl(new HashSet<HlslTreeNode>(), 0);
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			var result = $"({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "0"} + {Inputs[1]?.ToHlsl(visited, depth + 1) ?? "0"})";
			SetCached(this, result);
			return result;
		}
	}

	public class MulOperation : Operation
	{
		public MulOperation(HlslTreeNode left, HlslTreeNode right)
		{
			AddInput(left);
			AddInput(right);
		}
		public override string Mnemonic => "mul";
		public override string ToHlsl() => ToHlsl(new HashSet<HlslTreeNode>(), 0);
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			var result = $"({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "0"} * {Inputs[1]?.ToHlsl(visited, depth + 1) ?? "0"})";
			SetCached(this, result);
			return result;
		}
	}

	public class SubOperation : Operation
	{
		public SubOperation(HlslTreeNode left, HlslTreeNode right)
		{
			AddInput(left);
			AddInput(right);
		}
		public override string Mnemonic => "sub";
		public override string ToHlsl() => ToHlsl(new HashSet<HlslTreeNode>(), 0);
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			var result = $"({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "0"} - {Inputs[1]?.ToHlsl(visited, depth + 1) ?? "0"})";
			SetCached(this, result);
			return result;
		}
	}

	public class DotProductOperation : Operation
	{
		public DotProductOperation(List<HlslTreeNode> vector1, List<HlslTreeNode> vector2)
			: base()
		{
			foreach(var node in vector1)
				AddInput(node);
			foreach(var node in vector2)
				AddInput(node);
		}
		public override string Mnemonic => "dot";
		public override string ToHlsl() => ToHlsl(new HashSet<HlslTreeNode>(), 0);
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			int n = Inputs.Count / 2;
			var v1 = string.Join(", ", Inputs.Take(n).Select(i => i?.ToHlsl(visited, depth + 1) ?? "0"));
			var v2 = string.Join(", ", Inputs.Skip(n).Select(i => i?.ToHlsl(visited, depth + 1) ?? "0"));
			var vecType = n switch
			{
				2 => "float2",
				3 => "float3",
				4 => "float4",
				_ => $"float{n}"
			};
			var result = $"dot({vecType}({v1}), {vecType}({v2}))";
			SetCached(this, result);
			return result;
		}
	}

	public class LogOperation : HlslTreeNode
	{
		public HlslTreeNode Input { get; }
		public LogOperation(HlslTreeNode input)
		{
			Input = input;
		}
		public override string ToHlsl() => ToHlsl(new HashSet<HlslTreeNode>(), 0);
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			var result = $"log2({Input?.ToHlsl(visited, depth + 1) ?? "null"})";
			SetCached(this, result);
			return result;
		}
	}

	public class TextureLoadOutputNode : HlslTreeNode, IHasComponentIndex
	{
		public TextureLoadOutputNode(RegisterInputNode sampler, IEnumerable<HlslTreeNode> textureCoords, int componentIndex)
		{
			SamplerInput = sampler;
			if (textureCoords != null)
			{
				foreach (var coord in textureCoords)
				{
					TextureCoordinateInputs.Add(coord);
				}
			}
			ComponentIndex = componentIndex;
		}
		public HlslTreeNode SamplerInput { get; set; }
		public IList<HlslTreeNode> TextureCoordinateInputs { get; } = new List<HlslTreeNode>();
		public int ComponentIndex { get; set; }

		public override string ToHlsl() => ToHlsl(new HashSet<HlslTreeNode>(), 0);
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			var sampler = SamplerInput?.ToHlsl(visited, depth + 1) ?? "sampler";
			var coords = string.Join(", ", TextureCoordinateInputs.Select(tc => tc.ToHlsl(visited, depth + 1)));
			var result = $"tex2D({sampler}, {coords})";
			SetCached(this, result);
			return result;
		}
	}

	public class ConstantNode : HlslTreeNode
	{
		public ConstantNode(float value) { Value = value; }
		public float Value { get; set; }
		public override string ToHlsl() => ToHlsl(new HashSet<HlslTreeNode>(), 0);
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (TryGetCached(this, out var cached))
			{
				return cached;
			}
			var result = Util.ConstantFormatter.FormatFloat(Value);
			SetCached(this, result);
			return result;
		}
	}
}
