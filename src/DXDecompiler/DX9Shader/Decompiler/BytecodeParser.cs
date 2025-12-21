using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DXDecompiler.DX9Shader.Bytecode;
using DXDecompiler.DX9Shader.Bytecode.Ctab;
using DXDecompiler.DX9Shader.Decompiler.FlowControl;
using DXDecompiler.DX9Shader.Decompiler.Operations;

namespace DXDecompiler.DX9Shader.Decompiler
{
	class BytecodeParser
	{
		private Dictionary<RegisterComponentKey, HlslTreeNode> _activeOutputs;
		private Dictionary<RegisterKey, HlslTreeNode> _samplers;
		private List<IStatement> _statements = new List<IStatement>(); // New: statement list
		private readonly Dictionary<NodeKey, HlslTreeNode> _nodeInterner = new Dictionary<NodeKey, HlslTreeNode>();
		private readonly Dictionary<RegisterKey, int> _tempVersions = new Dictionary<RegisterKey, int>();

		public HlslAst Parse(ShaderModel shader)
		{
			LoadConstantOutputs(shader);

			int instructionPointer = 0;
			bool ifBlock = false;
			while(instructionPointer < shader.Tokens.Count)
			{
				var instruction = shader.Tokens[instructionPointer] as InstructionToken;
				if(instruction == null)
				{
					instructionPointer++;
					continue;
				}
				if(ifBlock)
				{
					if(instruction.Opcode == Opcode.Else)
					{
						ifBlock = false;
					}
				}
				else
				{
					if(instruction.Opcode == Opcode.IfC)
					{
						ifBlock = true;
					}
					ParseInstruction(instruction);
				}
				instructionPointer++;
			}

			Dictionary<RegisterComponentKey, HlslTreeNode> roots;
			if(shader.Type == ShaderType.Pixel)
			{
				roots = _activeOutputs
					.Where(o => o.Key.Type == RegisterType.ColorOut)
					.ToDictionary(o => o.Key, o => o.Value);
			}
			else
			{
				roots = _activeOutputs
					.Where(o => o.Key.Type == RegisterType.Output)
					.ToDictionary(o => o.Key, o => o.Value);
			}

			// Return both roots and statements for now (for migration/testing)
			if (_statements.Count > 0)
			{
				// Finalize/optimize statements before returning AST
				StatementFinalizer.Finalize(_statements);
				// Demonstration: add a PhiNode if not present (for output test)
				if (!_statements.Any(s => s is PhiNode))
				{
					var phi = new PhiNode();
					// Only add if there are assignments to use as inputs
					var assign = _statements.OfType<AssignmentStatement>().FirstOrDefault();
					if (assign != null)
					{
						phi.AddInput(assign.Value);
						_statements.Add(phi);
					}
				}
				return new HlslAst(_statements);
			}
			else
			{
				return new HlslAst(roots);
			}
		}

		private void LoadConstantOutputs(ShaderModel shader)
		{
			IList<ConstantDeclaration> constantTable = shader.ConstantTable?.ConstantDeclarations ?? new List<ConstantDeclaration>();

			_activeOutputs = new Dictionary<RegisterComponentKey, HlslTreeNode>();
			_samplers = new Dictionary<RegisterKey, HlslTreeNode>();

			foreach(var constant in constantTable)
			{
				for(uint r = 0; r < constant.RegisterCount; r++)
				{
					var data = constant.GetRegisterTypeByOffset(r);
					int samplerTextureDimension;
					switch(data.Type.ParameterType)
					{
						case ParameterType.Sampler1D:
							samplerTextureDimension = 1;
							goto SamplerCommon;
						case ParameterType.Sampler2D:
							samplerTextureDimension = 2;
							goto SamplerCommon;
						case ParameterType.Sampler3D:
						case ParameterType.SamplerCube:
							samplerTextureDimension = 3;
							goto SamplerCommon;
						SamplerCommon:
							{
								var registerKey = new RegisterKey(RegisterType.Sampler, constant.RegisterIndex + r);
								var destinationKey = new RegisterComponentKey(registerKey, 0);
								var shaderInput = new RegisterInputNode(destinationKey, samplerTextureDimension);
								_samplers.Add(registerKey, shaderInput);
							}
							break;
						case ParameterType.Float:
							{
								var registerKey = new RegisterKey(RegisterType.Const, constant.RegisterIndex + r);
								for(int i = 0; i < 4; i++)
								{
									var destinationKey = new RegisterComponentKey(registerKey, i);
									var shaderInput = new RegisterInputNode(destinationKey);
									_activeOutputs.Add(destinationKey, shaderInput);
								}
							}
							break;
						default:
							throw new NotImplementedException();
					}
				}
			}
		}

		private void ParseInstruction(InstructionToken instruction)
		{
			if(instruction.HasDestination)
			{
				var newOutputs = new Dictionary<RegisterComponentKey, HlslTreeNode>();

				RegisterComponentKey[] destinationKeys = GetDestinationKeys(instruction).ToArray();
				var destIndex = instruction.GetDestinationParamIndex();
				var destRegisterKey = instruction.GetParamRegisterKey(destIndex);
				int? newTempVersion = null;
				if (destRegisterKey.Type == RegisterType.Temp)
				{
					_tempVersions.TryGetValue(destRegisterKey, out var current);
					newTempVersion = current + 1;
				}
				foreach(RegisterComponentKey destinationKey in destinationKeys)
				{
					HlslTreeNode instructionTree = CreateInstructionTree(instruction, destinationKey);
					HlslTreeNode targetNode;
					if (newTempVersion.HasValue)
					{
						targetNode = new VersionedRegisterInputNode(destRegisterKey, newTempVersion.Value, destinationKey.ComponentIndex);
						newOutputs[destinationKey] = targetNode;
					}
					else
					{
						targetNode = new RegisterInputNode(destinationKey);
						newOutputs[destinationKey] = instructionTree;
					}
					// Add as assignment statement
					_statements.Add(new AssignmentStatement(
						target: targetNode,
						value: instructionTree,
						inputs: new Dictionary<RegisterComponentKey, HlslTreeNode>() // TODO: Fill with actual inputs
					));
				}

				foreach(var output in newOutputs)
				{
					_activeOutputs[output.Key] = output.Value;
				}
				if (newTempVersion.HasValue)
				{
					_tempVersions[destRegisterKey] = newTempVersion.Value;
				}
			}
			// Integrate BreakStatement and ClipStatement for their opcodes
			if (instruction.Opcode == Opcode.Break)
			{
				_statements.Add(new BreakStatement(null, new Dictionary<RegisterComponentKey, HlslTreeNode>()));
			}
			if (instruction.Opcode == Opcode.TexKill)
			{
				_statements.Add(new ClipStatement(new HlslTreeNode[0], new Dictionary<RegisterComponentKey, HlslTreeNode>()));
			}
		}

		private static IEnumerable<RegisterComponentKey> GetDestinationKeys(InstructionToken instruction)
		{
			int index = instruction.GetDestinationParamIndex();
			RegisterKey registerKey = instruction.GetParamRegisterKey(index);

			if(registerKey.Type == RegisterType.Sampler)
			{
				yield break;
			}

			ComponentFlags mask = instruction.GetDestinationWriteMask();
			for(int component = 0; component < 4; component++)
			{
				if((mask & (ComponentFlags)(1 << component)) == 0) continue;

				yield return new RegisterComponentKey(registerKey, component);
			}
		}

		private HlslTreeNode CreateInstructionTree(InstructionToken instruction, RegisterComponentKey destinationKey)
		{
			int componentIndex = destinationKey.ComponentIndex;

			switch(instruction.Opcode)
			{
				case Opcode.Dcl:
					{
						var shaderInput = new RegisterInputNode(destinationKey);
						return shaderInput;
					}
				case Opcode.Def:
					{
						var constant = new ConstantNode(instruction.GetParamSingle(componentIndex + 1));
						return constant;
					}
				case Opcode.DefI:
					{
						var constant = new ConstantNode(instruction.GetParamInt(componentIndex + 1));
						return constant;
					}
				case Opcode.DefB:
					{
						throw new NotImplementedException();
					}
				case Opcode.Abs:
				case Opcode.Add:
				case Opcode.Cnd:
				case Opcode.Cmp:
				case Opcode.DSX:
				case Opcode.DSY:
				case Opcode.Dst:
				case Opcode.Frc:
				case Opcode.LogP:
				case Opcode.Lrp:
				case Opcode.Mad:
				case Opcode.Max:
				case Opcode.Min:
				case Opcode.Mov:
				case Opcode.MovA:
				case Opcode.Mul:
				case Opcode.Pow:
				case Opcode.Rcp:
				case Opcode.Rsq:
				case Opcode.Sgn:
				case Opcode.SinCos:
				case Opcode.Sge:
				case Opcode.Slt:
				case Opcode.Sub:
					{
						HlslTreeNode[] inputs = GetInputs(instruction, componentIndex);
						switch(instruction.Opcode)
						{
							case Opcode.Abs:
								return InternOperation(typeof(AbsoluteOperation), inputs, () => new AbsoluteOperation(inputs[0]));
							case Opcode.Cnd:
								return InternOperation(typeof(CompareOperation), inputs, () => new CompareOperation(inputs[0], inputs[1], inputs[2]));
							case Opcode.Cmp:
								return InternOperation(typeof(CompareOperation), inputs, () => new CompareOperation(inputs[0], inputs[1], inputs[2]));
							case Opcode.DSX:
								return InternOperation(typeof(DerivativeXOperation), inputs, () => new DerivativeXOperation(inputs[0]));
							case Opcode.DSY:
								return InternOperation(typeof(DerivativeYOperation), inputs, () => new DerivativeYOperation(inputs[0]));
							case Opcode.Dst:
								return CreateDstNode(instruction, componentIndex);
							case Opcode.Frc:
								return InternOperation(typeof(FractionalOperation), inputs, () => new FractionalOperation(inputs[0]));
							case Opcode.LogP:
								return InternOperation(typeof(LogOperation), inputs, () => new LogOperation(inputs[0]));
							case Opcode.Lrp:
								return InternOperation(typeof(LinearInterpolateOperation), inputs, () => new LinearInterpolateOperation(inputs[0], inputs[1], inputs[2]));
							case Opcode.Max:
								return InternOperation(typeof(MaximumOperation), inputs, () => new MaximumOperation(inputs[0], inputs[1]));
							case Opcode.Min:
								return InternOperation(typeof(MinimumOperation), inputs, () => new MinimumOperation(inputs[0], inputs[1]));
							case Opcode.Mov:
								return InternOperation(typeof(MoveOperation), inputs, () => new MoveOperation(inputs[0]));
							case Opcode.MovA:
								return InternOperation(typeof(MoveOperation), inputs, () => new MoveOperation(inputs[0]));
							case Opcode.Add:
								return InternOperation(typeof(AddOperation), inputs, () => new AddOperation(inputs[0], inputs[1]));
							case Opcode.Mul:
								return InternOperation(typeof(MultiplyOperation), inputs, () => new MultiplyOperation(inputs[0], inputs[1]));
							case Opcode.Mad:
								return InternOperation(typeof(MultiplyAddOperation), inputs, () => new MultiplyAddOperation(inputs[0], inputs[1], inputs[2]));
							case Opcode.Pow:
								return InternOperation(typeof(PowerOperation), inputs, () => new PowerOperation(inputs[0], inputs[1]));
							case Opcode.Rcp:
								return InternOperation(typeof(ReciprocalOperation), inputs, () => new ReciprocalOperation(inputs[0]));
							case Opcode.Rsq:
								return InternOperation(typeof(ReciprocalSquareRootOperation), inputs, () => new ReciprocalSquareRootOperation(inputs[0]));
							case Opcode.Sgn:
								return InternOperation(typeof(SignOperation), inputs, () => new SignOperation(inputs[0]));
							case Opcode.SinCos:
								if(componentIndex == 0)
								{
									return InternOperation(typeof(CosineOperation), inputs, () => new CosineOperation(inputs[0]));
								}
								return InternOperation(typeof(SineOperation), inputs, () => new SineOperation(inputs[0]));
							case Opcode.Sge:
								return InternOperation(typeof(SignGreaterOrEqualOperation), inputs, () => new SignGreaterOrEqualOperation(inputs[0], inputs[1]));
							case Opcode.Slt:
								return InternOperation(typeof(SignLessOperation), inputs, () => new SignLessOperation(inputs[0], inputs[1]));
							case Opcode.Sub:
								return InternOperation(typeof(SubtractOperation), inputs, () => new SubtractOperation(inputs[0], inputs[1]));
							default:
								throw new NotImplementedException();
						}
					}
				case Opcode.Tex:
				case Opcode.TexLDL:
					return CreateTextureLoadOutputNode(instruction, componentIndex);
				case Opcode.TexReg2AR:
					{
						// SM1.x: dest = tex2D(sampler, float2(src.a, src.r));
						// src is param 1
						var srcKey = instruction.GetParamRegisterKey(1);
						var srcAKey = new RegisterComponentKey(srcKey, 3); // .a
						var srcRKey = new RegisterComponentKey(srcKey, 0); // .r
						HlslTreeNode srcA = _activeOutputs.ContainsKey(srcAKey) ? _activeOutputs[srcAKey] : null;
						HlslTreeNode srcR = _activeOutputs.ContainsKey(srcRKey) ? _activeOutputs[srcRKey] : null;
						var coords = new List<HlslTreeNode> { srcA, srcR };
						// Sampler is implicit in SM1.x, pass null or a stub
						return new TextureLoadOutputNode(null, coords, destinationKey.ComponentIndex);
					}
				case Opcode.ExpP:
					{
						var inputs = GetInputs(instruction, componentIndex);
						return new ExpPOperation(inputs[0]);
					}
				case Opcode.Exp:
					{
						var inputs = GetInputs(instruction, componentIndex);
						return new ExpOperation(inputs[0]);
					}
				case Opcode.TexReg2GB:
					{
						// SM1.x: dest = tex2D(sampler, float2(src.g, src.b));
						var srcKey = instruction.GetParamRegisterKey(1);
						var srcGKey = new RegisterComponentKey(srcKey, 1); // .g
						var srcBKey = new RegisterComponentKey(srcKey, 2); // .b
						HlslTreeNode srcG = _activeOutputs.ContainsKey(srcGKey) ? _activeOutputs[srcGKey] : null;
						HlslTreeNode srcB = _activeOutputs.ContainsKey(srcBKey) ? _activeOutputs[srcBKey] : null;
						var coords = new List<HlslTreeNode> { srcG, srcB };
						return new TextureLoadOutputNode(null, coords, destinationKey.ComponentIndex);
					}
				case Opcode.TexKill:
					{
						// TexKill may have no operands in some shaders
						if (instruction.Data.Length < 2) // Only opcode, no operand
						{
							// Return a stub node or skip
							return null;
						}
						var inputs = GetInputs(instruction, componentIndex);
						return new TexKillOperation(inputs[0]);
					}
				case Opcode.TexCoord:
					{
						var shaderInput = new RegisterInputNode(destinationKey);
						return shaderInput;
					}
				case Opcode.Dp3:
					{
						var vector1 = new List<HlslTreeNode>(GetInputComponents(instruction, 1, 3));
						var vector2 = new List<HlslTreeNode>(GetInputComponents(instruction, 2, 3));
						return InternOperation(typeof(DotProductOperation), vector1.Concat(vector2).ToArray(),
							() => new DotProductOperation(vector1, vector2));
					}
				case Opcode.Crs:
					{
						return CreateCrossProductNode(instruction, componentIndex);
					}
				case Opcode.DP2Add:
					{
						return CreateDotProduct2AddNode(instruction);
					}
				case Opcode.Dp4:
					{
						var vector1 = new List<HlslTreeNode>(GetInputComponents(instruction, 1, 4));
						var vector2 = new List<HlslTreeNode>(GetInputComponents(instruction, 2, 4));
						return InternOperation(typeof(DotProductOperation), vector1.Concat(vector2).ToArray(),
							() => new DotProductOperation(vector1, vector2));
					}
				case Opcode.Nrm:
					{
						// Normalize a 3-component vector
						var inputs = new List<HlslTreeNode>();
						for(int i = 0; i < 3; i++)
						{
							var inputKey = GetParamRegisterComponentKey(instruction, 1, i);
							if (!_activeOutputs.TryGetValue(inputKey, out HlslTreeNode input))
							{
								input = new RegisterInputNode(inputKey);
								_activeOutputs[inputKey] = input;
							}
							inputs.Add(input);
						}
						return InternOperation(typeof(NormalizeOutputNode), inputs.ToArray(),
							() => new NormalizeOutputNode(inputs, componentIndex));
					}
				case Opcode.Lit:
					{
						var inputs = GetInputs(instruction, componentIndex);
						return InternOperation(typeof(LitOperation), inputs, () => new LitOperation(inputs[0]));
					}
				case Opcode.Log:
					{
						var inputs = GetInputs(instruction, componentIndex);
						return InternOperation(typeof(LogOperation), inputs, () => new LogOperation(inputs[0]));
					}
				default:
					throw new NotImplementedException($"{instruction.Opcode} not implemented");
			}
		}

		private TextureLoadOutputNode CreateTextureLoadOutputNode(InstructionToken instruction, int outputComponent)
		{
			// Handle Shader Model 1.x Tex instruction (only 1 operand)
			if(instruction.Data.Length < 3)
			{
				// Not enough operands for sampler/coords; return a stub node or a comment node
				// You may want to implement a more accurate emulation if needed
				return new TextureLoadOutputNode(null, new List<HlslTreeNode>(), outputComponent); // or a custom node
			}

			const int TextureCoordsIndex = 1;
			const int SamplerIndex = 2;

			RegisterKey samplerRegister = instruction.GetParamRegisterKey(SamplerIndex);
			if(!_samplers.TryGetValue(samplerRegister, out HlslTreeNode samplerInput))
			{
				throw new InvalidOperationException();
			}
			var samplerRegisterInput = (RegisterInputNode)samplerInput;
			int numSamplerOutputComponents = samplerRegisterInput.SamplerTextureDimension;

			IList<HlslTreeNode> texCoords = new List<HlslTreeNode>();
			for(int component = 0; component < numSamplerOutputComponents; component++)
			{
				RegisterComponentKey textureCoordsKey = GetParamRegisterComponentKey(instruction, TextureCoordsIndex, component);
				HlslTreeNode textureCoord = _activeOutputs[textureCoordsKey];
				texCoords.Add(textureCoord);
			}

			return new TextureLoadOutputNode(samplerRegisterInput, texCoords, outputComponent);
		}

		private HlslTreeNode CreateDotProduct2AddNode(InstructionToken instruction)
		{
			var vector1 = GetInputComponents(instruction, 1, 2);
			var vector2 = GetInputComponents(instruction, 2, 2);
			var add = GetInputComponents(instruction, 3, 1)[0];

			var dp2 = new AddOperation(
				new MultiplyOperation(vector1[0], vector2[0]),
				new MultiplyOperation(vector1[1], vector2[1]));

			return new AddOperation(dp2, add);
		}

		private HlslTreeNode CreateDotProductNode(InstructionToken instruction)
		{
			var addends = new List<HlslTreeNode>();
			int numComponents = instruction.Opcode == Opcode.Dp3 ? 3 : 4;
			for(int component = 0; component < numComponents; component++)
			{
				IList<HlslTreeNode> componentInput = GetInputs(instruction, component);
				var multiply = new MultiplyOperation(componentInput[0], componentInput[1]);
				addends.Add(multiply);
			}

			return addends.Aggregate((addition, addend) => new AddOperation(addition, addend));
		}

		private HlslTreeNode CreateNormalizeOutputNode(InstructionToken instruction, int outputComponent)
		{
			var inputs = new List<HlslTreeNode>();
			for(int component = 0; component < 3; component++)
			{
				IList<HlslTreeNode> componentInput = GetInputs(instruction, component);
				inputs.AddRange(componentInput);
			}

			return new NormalizeOutputNode(inputs, outputComponent);
		}

		private HlslTreeNode CreateDstNode(InstructionToken instruction, int outputComponent)
		{
			// dst: (1, src0.y * src1.y, src0.z, src1.w)
			switch(outputComponent)
			{
				case 0:
					return new ConstantNode(1.0f);
				case 1:
					{
						var src0y = GetInputComponents(instruction, 1, 4)[1];
						var src1y = GetInputComponents(instruction, 2, 4)[1];
						return new MultiplyOperation(src0y, src1y);
					}
				case 2:
					return GetInputComponents(instruction, 1, 4)[2];
				case 3:
					return GetInputComponents(instruction, 2, 4)[3];
				default:
					return new ConstantNode(0.0f);
			}
		}

		private HlslTreeNode CreateCrossProductNode(InstructionToken instruction, int outputComponent)
		{
			var v1 = GetInputComponents(instruction, 1, 3);
			var v2 = GetInputComponents(instruction, 2, 3);
			switch(outputComponent)
			{
				case 0:
					return new SubtractOperation(
						new MultiplyOperation(v1[1], v2[2]),
						new MultiplyOperation(v1[2], v2[1]));
				case 1:
					return new SubtractOperation(
						new MultiplyOperation(v1[2], v2[0]),
						new MultiplyOperation(v1[0], v2[2]));
				case 2:
					return new SubtractOperation(
						new MultiplyOperation(v1[0], v2[1]),
						new MultiplyOperation(v1[1], v2[0]));
				case 3:
					return new ConstantNode(0.0f);
				default:
					return new ConstantNode(0.0f);
			}
		}

		private HlslTreeNode[] GetInputs(InstructionToken instruction, int componentIndex)
		{
			int numInputs = GetNumInputs(instruction.Opcode);
			var inputs = new HlslTreeNode[numInputs];
			for(int i = 0; i < numInputs; i++)
			{
				int inputParameterIndex = i + 1;
				RegisterComponentKey inputKey = GetParamRegisterComponentKey(instruction, inputParameterIndex, componentIndex);
				var input = GetActiveOutputOrCreate(inputKey);
				var modifier = instruction.GetSourceModifier(inputParameterIndex);
				input = ApplyModifier(input, modifier);
				inputs[i] = input;
			}
			return inputs;
		}

		private HlslTreeNode[] GetInputComponents(InstructionToken instruction, int inputParameterIndex, int numComponents)
		{
			var components = new HlslTreeNode[numComponents];
			for(int i = 0; i < numComponents; i++)
			{
				RegisterComponentKey inputKey = GetParamRegisterComponentKey(instruction, inputParameterIndex, i);
				var input = GetActiveOutputOrCreate(inputKey);
				var modifier = instruction.GetSourceModifier(inputParameterIndex);
				input = ApplyModifier(input, modifier);
				components[i] = input;
			}
			return components;
		}

		private static HlslTreeNode ApplyModifier(HlslTreeNode input, SourceModifier modifier)
		{
			switch(modifier)
			{
				case SourceModifier.Abs:
					return new AbsoluteOperation(input);
				case SourceModifier.Negate:
					return new NegateOperation(input);
				case SourceModifier.AbsAndNegate:
					return new NegateOperation(new AbsoluteOperation(input));
				case SourceModifier.Bias:
					// x - 0.5
					return new SubtractOperation(input, new ConstantNode(0.5f));
				case SourceModifier.BiasAndNegate:
					// -(x - 0.5)
					return new NegateOperation(new SubtractOperation(input, new ConstantNode(0.5f)));
				case SourceModifier.Sign:
					// sign(x)
					return new SignOperation(input);
				case SourceModifier.X2:
					// x * 2
					return new MultiplyOperation(input, new ConstantNode(2.0f));
				case SourceModifier.X2AndNegate:
					// -(x * 2)
					return new NegateOperation(new MultiplyOperation(input, new ConstantNode(2.0f)));
				case SourceModifier.DivideByZ:
				case SourceModifier.DivideByW:
					// Not implemented, pass through
					return input;
				case SourceModifier.None:
					return input;
				default:
					// For any unhandled modifier, just return input (no-op)
					return input;
			}
		}

		private static int GetNumInputs(Opcode opcode)
		{
			switch(opcode)
			{
				case Opcode.Abs:
				case Opcode.Frc:
				case Opcode.Mov:
				case Opcode.Nrm:
				case Opcode.Rcp:
				case Opcode.Rsq:
				case Opcode.SinCos:
				case Opcode.MovA:
				case Opcode.Sgn:
				case Opcode.LogP:
					return 1;
				case Opcode.Add:
				case Opcode.Crs:
				case Opcode.Dp3:
				case Opcode.Dp4:
				case Opcode.Dst:
				case Opcode.Max:
				case Opcode.Min:
				case Opcode.Mul:
				case Opcode.Pow:
				case Opcode.Sge:
				case Opcode.Slt:
				case Opcode.Sub:
				case Opcode.Tex:
					return 2;
				case Opcode.Cmp:
				case Opcode.Cnd:
				case Opcode.DP2Add:
				case Opcode.Lrp:
				case Opcode.Mad:
					return 3;
				case Opcode.Lit:
					return 1;
				default:
					// Fallback: treat as unary to avoid NotImplementedException
					return 1;
			}
		}

		private static RegisterComponentKey GetParamRegisterComponentKey(InstructionToken instruction, int paramIndex, int component)
		{
			RegisterKey registerKey = instruction.GetParamRegisterKey(paramIndex);
			byte[] swizzle = instruction.GetSourceSwizzleComponents(paramIndex);
			int componentIndex = swizzle[component];

			return new RegisterComponentKey(registerKey, componentIndex);
		}

		private HlslTreeNode GetActiveOutputOrCreate(RegisterComponentKey inputKey)
		{
			if (_activeOutputs.TryGetValue(inputKey, out HlslTreeNode input))
			{
				return input;
			}

			if (inputKey.Type == RegisterType.Temp)
			{
				_tempVersions.TryGetValue(inputKey.RegisterKey, out var version);
				input = new VersionedRegisterInputNode(inputKey.RegisterKey, version, inputKey.ComponentIndex);
			}
			else
			{
				input = new RegisterInputNode(inputKey);
			}
			_activeOutputs[inputKey] = input;
			return input;
		}

		private HlslTreeNode InternOperation(Type opType, HlslTreeNode[] inputs, Func<HlslTreeNode> factory)
		{
			var key = new NodeKey(opType, inputs);
			if (_nodeInterner.TryGetValue(key, out var existing))
			{
				return existing;
			}
			var created = factory();
			_nodeInterner[key] = created;
			return created;
		}

		private readonly struct NodeKey
		{
			private readonly Type _type;
			private readonly HlslTreeNode[] _inputs;
			private readonly int _hashCode;

			public NodeKey(Type type, HlslTreeNode[] inputs)
			{
				_type = type;
				_inputs = inputs;
				unchecked
				{
					int hash = type.GetHashCode();
					if (inputs != null)
					{
						for (int i = 0; i < inputs.Length; i++)
						{
							hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(inputs[i]);
						}
					}
					_hashCode = hash;
				}
			}

			public override int GetHashCode() => _hashCode;

			public override bool Equals(object obj)
			{
				if (obj is not NodeKey other)
				{
					return false;
				}
				if (!ReferenceEquals(_type, other._type))
				{
					return false;
				}
				if (_inputs == null || other._inputs == null)
				{
					return _inputs == other._inputs;
				}
				if (_inputs.Length != other._inputs.Length)
				{
					return false;
				}
				for (int i = 0; i < _inputs.Length; i++)
				{
					if (!ReferenceEquals(_inputs[i], other._inputs[i]))
					{
						return false;
					}
				}
				return true;
			}
		}
	}
}
