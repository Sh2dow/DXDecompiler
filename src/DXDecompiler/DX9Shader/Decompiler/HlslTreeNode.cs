using System;
using System.Collections.Generic;
using DXDecompiler.DX9Shader.Decompiler.Operations;
using System.Linq;

namespace DXDecompiler.DX9Shader.Decompiler
{
	public class HlslTreeNode
	{
		public IList<HlslTreeNode> Inputs { get; } = new List<HlslTreeNode>();
		public IList<HlslTreeNode> Outputs { get; } = new List<HlslTreeNode>();

		private const int MaxToHlslDepth = 1024; // Increased depth limit for complex shaders
		public virtual string ToHlsl()
		{
			return ToHlsl(new HashSet<HlslTreeNode>(), 0);
		}
		public virtual string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			if (depth > MaxToHlslDepth)
			{
				Console.WriteLine($"[HlslTreeNode] Max recursion depth reached in {GetType().Name} at depth {depth}");
				return $"/* ERROR: Max recursion depth ({MaxToHlslDepth}) reached in {GetType().Name} */";
			}
			if (!visited.Add(this))
			{
				Console.WriteLine($"[HlslTreeNode] Cycle detected in {GetType().Name} at depth {depth}");
				return $"/* ERROR: Cycle detected in {GetType().Name} */";
			}
			if (Inputs.Count == 0)
			{
				Console.WriteLine($"[HlslTreeNode] Unmapped leaf node: {GetType().Name} at depth {depth}");
				return $"/* Unmapped leaf node: {GetType().Name} */";
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
			return $"lit({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "null"})";
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
			return $"sign({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "null"})";
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
			return $"exp2({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "null"})";
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
			return $"exp({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "null"})";
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
			return $"clip({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "null"})";
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
			return $"({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "0"} + {Inputs[1]?.ToHlsl(visited, depth + 1) ?? "0"})";
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
			return $"({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "0"} * {Inputs[1]?.ToHlsl(visited, depth + 1) ?? "0"})";
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
			return $"({Inputs[0]?.ToHlsl(visited, depth + 1) ?? "0"} - {Inputs[1]?.ToHlsl(visited, depth + 1) ?? "0"})";
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
			int n = Inputs.Count / 2;
			var v1 = string.Join(", ", Inputs.Take(n).Select(i => i?.ToHlsl(visited, depth + 1) ?? "0"));
			var v2 = string.Join(", ", Inputs.Skip(n).Select(i => i?.ToHlsl(visited, depth + 1) ?? "0"));
			return $"dot({v1}, {v2})";
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
			return $"log2({Input?.ToHlsl(visited, depth + 1) ?? "null"})";
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
			var sampler = SamplerInput?.ToHlsl(visited, depth + 1) ?? "sampler";
			var coords = string.Join(", ", TextureCoordinateInputs.Select(tc => tc.ToHlsl(visited, depth + 1)));
			return $"tex2D({sampler}, {coords})";
		}
	}

	public class ConstantNode : HlslTreeNode
	{
		public ConstantNode(float value) { Value = value; }
		public float Value { get; set; }
		public override string ToHlsl() => ToHlsl(new HashSet<HlslTreeNode>(), 0);
		public override string ToHlsl(HashSet<HlslTreeNode> visited, int depth)
		{
			return Util.ConstantFormatter.FormatFloat(Value);
		}
	}
}
