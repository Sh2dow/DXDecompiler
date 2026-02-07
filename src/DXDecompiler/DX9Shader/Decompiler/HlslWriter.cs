using System;
using System.Collections.Generic;
using System.Linq;
using DXDecompiler.DX9Shader.Asm;
using DXDecompiler.DX9Shader.Bytecode;
using DXDecompiler.DX9Shader.Bytecode.Ctab;
using DXDecompiler.DX9Shader.Decompiler.Compiler;
using DXDecompiler.DX9Shader.Decompiler.FlowControl;
using DXDecompiler.Util;

namespace DXDecompiler.DX9Shader.Decompiler
{
	public class HlslWriter : DecompileWriter
	{
		private class SourceOperand
		{
			public string
				Body { get; set; } // normally, register / constant name, or type name if Literals are not null

			public string[] Literals { get; set; } // either null, or literal values

			public string
				Swizzle { get; set; } // either empty, or a swizzle with leading dot. Empty if Literals are not null.

			public string
				Modifier
			{
				get;
				set;
			} // should be used with string.Format to format the body. Should be "{0}" if Literals are not null.

			public ParameterType? SamplerType { get; set; } // not null if it's a sampler

			public override string ToString()
			{
				var body = Body;
				if(Literals is not null)
				{
					if(Literals.Length == 1)
					{
						if(float.TryParse(Literals[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
							body = FormatHlslFloat(f);
						else
							body = Literals[0];
					}
					else
					{
						var floats = Literals.Select(l => float.TryParse(l, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : float.NaN);
						body = $"float{Literals.Length}({FormatHlslFloatArray(floats)})";
					}
				}

				body = string.Format(Modifier, body);
				if(body.All(char.IsDigit))
				{
					body = $"({body})";
				}

				return body + Swizzle;
			}
		}

		private readonly ShaderModel _shader;
		private readonly AsmWriter _disassember;
		private readonly bool _doAstAnalysis;
		private int _iterationDepth = 0;

		private EffectHLSLWriter _effectWriter;
		private RegisterState _registers;
		string _entryPoint;
		bool _outputDefaultValues;

		// === Added fields/constants for recursion and diagnostics ===
		private const int MaxRecursionDepth = 256;
		private const int MaxAstStatements = 8192;
		private const int MaxInlineExpressionLength = 1024;
		private const int MaxTruncatePreviewLength = 256;
		private const int MaxAstExpressionCost = 24;
		private readonly List<string> skippedAssignments = new List<string>();
		private readonly bool _verbose;
		private int _astTempIndex = 0;

		public HlslWriter(ShaderModel shader, bool doAstAnalysis = false, string entryPoint = null,
			bool outputDefaultValues = true, bool verbose = false)
		{
			_shader = shader;
			_doAstAnalysis = doAstAnalysis;
			if(!_doAstAnalysis)
			{
				_disassember = new(shader);
			}

			_outputDefaultValues = outputDefaultValues;
			if(string.IsNullOrEmpty(entryPoint))
			{
				_entryPoint = $"{_shader.Type}Main";
			}
			else
			{
				_entryPoint = entryPoint;
			}
			_verbose = verbose;
		}

		public static string Decompile(byte[] bytecode, string entryPoint = null, bool verbose = false)
		{
			var shaderModel = ShaderReader.ReadShader(bytecode);
			return Decompile(shaderModel, verbose: verbose);
		}

		public static string Decompile(ShaderModel shaderModel, string entryPoint = null,
			EffectHLSLWriter effect = null, bool outputDefaultValues = true, bool doAstAnalysis = true, bool verbose = false)
		{
			if(shaderModel.Type == ShaderType.Effect)
			{
				return EffectHLSLWriter.Decompile(shaderModel.EffectChunk);
			}

			var hlslWriter = new HlslWriter(shaderModel, doAstAnalysis, entryPoint, outputDefaultValues, verbose)
			{
				_effectWriter = effect
			};
			return hlslWriter.Decompile();
		}

		private string GetDestinationName(InstructionToken instruction, out string writeMaskName)
		{
			return _registers.GetDestinationName(instruction, out writeMaskName);
		}

		private string GetDestinationNameWithWriteMask(InstructionToken instruction)
		{
			var destinationName = GetDestinationName(instruction, out var writeMask);
			return destinationName + writeMask;
		}

		private SourceOperand GetSourceName(InstructionToken instruction, int srcIndex, bool isLogicalIndex = true)
		{
			int dataIndex;
			if(isLogicalIndex)
			{
				// compute the actual data index, which might be different from logical index 
				// because of relative addressing mode.

				// TODO: Handle relative addressing mode in a better way,
				// by using `InstructionToken.Operands`:
				// https://github.com/spacehamster/DXDecompiler/pull/6#issuecomment-782958769

				// if instruction has destination, then source starts at the index 1
				// here we assume destination won't have relative addressing,
				// so we assume destination will only occupy 1 slot,
				// that is, the start index for sources will be 1 if instruction.HasDestination is true.
				var begin = instruction.HasDestination ? 1 : 0;
				dataIndex = begin;
				while(srcIndex > begin)
				{
					if(instruction.IsRelativeAddressMode(dataIndex))
					{
						++dataIndex;
					}

					++dataIndex;
					--srcIndex;
				}
			}
			else
			{
				dataIndex = srcIndex;
			}

			ParameterType? samplerType = null;
			var registerNumber = instruction.GetParamRegisterNumber(dataIndex);
			var registerType = instruction.GetParamRegisterType(dataIndex);
			if(registerType == RegisterType.Sampler)
			{
				var decl = _registers.FindConstant(RegisterSet.Sampler, registerNumber);
				var type = decl.GetRegisterTypeByOffset(registerNumber - decl.RegisterIndex);
				samplerType = type.Type.ParameterType;
			}


			var body = _registers.GetSourceName(instruction, dataIndex, out var swizzle, out var modifier,
				out var literals);
			return new SourceOperand
			{
				Body = body,
				Literals = literals,
				Swizzle = swizzle,
				Modifier = modifier,
				SamplerType = samplerType
			};
		}

		private void WriteInstruction(InstructionToken instruction)
		{
			// Write disassembly as a comment, indented
			var disasm = _disassember?.Disassemble(instruction).Trim();
			if (!string.IsNullOrWhiteSpace(disasm))
			{
				WriteIndentedLine($"// {disasm}");
			}
			switch(instruction.Opcode)
			{
				case Opcode.Def:
				case Opcode.DefI:
				case Opcode.Dcl:
				case Opcode.End:
					return;
				// these opcodes don't need indents:
				case Opcode.Else:
					Indent--;
					WriteIndentedLine("} else {");
					Indent++;
					return;
				case Opcode.Endif:
					Indent--;
					WriteIndentedLine("}");
					return;
				case Opcode.EndRep:
					Indent--;
					_iterationDepth--;
					WriteIndentedLine("}");
					return;
				case Opcode.Phase:
					// Phase: Used in ps_2_x and ps_3_0 to separate shader phases. No output needed in HLSL.
					WriteIndentedLine("// phase");
					return;
			}

			// WriteInstruction

			void WriteAssignment(string sourceFormat, params SourceOperand[] args)
			{
				var destination = GetDestinationName(instruction, out var writeMask);
				var destinationModifier = instruction.GetDestinationResultModifier() switch
				{
					ResultModifier.None => "{0} = {1};",
					ResultModifier.Saturate => "{0} = saturate({1});",
					ResultModifier.PartialPrecision => _verbose ? $"{{0}} = /* partial precision (_pp) modifier not mapped to HLSL, value may differ on some hardware */ {{1}};" : "{0} = {1};",
					ResultModifier.Saturate | ResultModifier.PartialPrecision =>
						"{0} = /* saturate+_pp */ saturate({1});",
					object unknown => ";// error"
				};
				var sourceResult = string.Format(sourceFormat, args);

				var swizzleSizes = args.Select(x => x.Swizzle.StartsWith(".") ? x.Swizzle.Trim('.').Length : -1);
				var returnsScalar = instruction.Opcode.ReturnsScalar() || swizzleSizes.All(x => x == 1);

				// Generalized: Detect scalar output (MaskedLength == 1)
				bool isScalarOutput = false;
				if(instruction.HasDestination)
				{
					var destKey = instruction.GetParamRegisterKey(instruction.GetDestinationParamIndex());
					var decl = _registers.RegisterDeclarations.ContainsKey(destKey)
						? _registers.RegisterDeclarations[destKey]
						: null;
					if(decl != null && decl.MaskedLength == 1)
					{
						isScalarOutput = true;
					}
				}

				if(isScalarOutput)
				{
					for(int i = 0; i < args.Length; i++)
					{
						if(args[i].Literals != null && args[i].Literals.Length > 0)
						{
							args[i].Literals = new[] { args[i].Literals[0] };
							args[i].Swizzle = "";
						}
						else if(!string.IsNullOrEmpty(args[i].Swizzle) && args[i].Swizzle != ".x")
						{
							args[i].Swizzle = ".x";
						}

						if(args[i].Body != null && args[i].Body.StartsWith("float"))
						{
							var start = args[i].Body.IndexOf('(');
							var end = args[i].Body.IndexOf(',');
							if(start >= 0 && end > start)
							{
								args[i].Body = args[i].Body.Substring(start + 1, end - start - 1).Trim();
							}
						}
					}

					sourceResult = string.Format(sourceFormat, args);
				}

				if(writeMask.Length > 0)
				{
					destination += writeMask;
					if(returnsScalar)
					{
						// do nothing, don't need to append write mask as swizzle
					}
					// if the instruction is parallel then we are safe to "edit" source swizzles
					else if(instruction.Opcode.IsParallel(_shader))
					{
						foreach(var arg in args)
						{
							const string xyzw = ".xyzw";

							if(arg.Literals is not null)
							{
								arg.Literals = arg.Literals
									.Where((v, i) => writeMask.Contains(xyzw[i + 1]))
									.ToArray();
								continue;
							}

							var trimmedSwizzle = ".";
							if(string.IsNullOrEmpty(arg.Swizzle))
							{
								arg.Swizzle = xyzw;
							}

							while(arg.Swizzle.Length <= 4)
							{
								arg.Swizzle += arg.Swizzle.Last();
							}

							for(var i = 1; i <= 4; ++i)
							{
								if(writeMask.Contains(xyzw[i]))
								{
									trimmedSwizzle += arg.Swizzle[i];
								}
							}

							arg.Swizzle = trimmedSwizzle;
						}

						sourceResult = string.Format(sourceFormat, args);
					}
					// if we cannot "edit" the swizzles, we need to apply write masks on the source result
					else
					{
						if(sourceResult.Last() != ')')
						{
							sourceResult = $"({sourceResult})";
						}

						sourceResult += writeMask;
					}
				}

				WriteIndentedLine(destinationModifier, destination, sourceResult);
			}

			void WriteTextureAssignment(string postFix, SourceOperand sampler, SourceOperand uv, int? dimension,
				params SourceOperand[] others)
			{
				var (operation, defaultDimension) = sampler.SamplerType switch
				{
					ParameterType.Sampler1D => ("tex1D", 1),
					ParameterType.Sampler2D => ("tex2D", 2),
					ParameterType.Sampler3D => ("tex3D", 3),
					ParameterType.SamplerCube => ("texCUBE", 3),
					ParameterType.Sampler => ("texUnknown", 4),
					_ => throw new InvalidOperationException(sampler.SamplerType.ToString())
				};
				dimension ??= defaultDimension;
				var args = new SourceOperand[others.Length + 2];
				var uvSwizzle = uv.Swizzle.TrimStart('.');
				if(uvSwizzle.Length == 0)
				{
					uvSwizzle = "xyzw";
				}

				if(uvSwizzle.Length > dimension)
				{
					uv.Swizzle = "." + uvSwizzle.Substring(0, dimension.Value);
				}

				args[0] = sampler;
				args[1] = uv;
				others.CopyTo(args, 2);
				var format = string.Join(", ", args.Select((_, i) => $"{{{i}}}"));
				WriteAssignment($"{operation}{postFix}({format})", args);
			}

			switch(instruction.Opcode)
			{
				case Opcode.Abs:
					WriteAssignment("abs({0})", GetSourceName(instruction, 1));
					break;
				case Opcode.Add:
					WriteAssignment("{0} + {1}", GetSourceName(instruction, 1), GetSourceName(instruction, 2));
					break;
				case Opcode.Cmp:
					// TODO: should be per-component
					// TODO: Handle depth output
					WriteAssignment("({0} >= 0) ? {1} : {2}",
						GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
					break;
				case Opcode.Cnd:
					// CND: if src0 >= 0 then dest = src1 else dest = src2
					WriteAssignment("({0} >= 0) ? {1} : {2}",
						GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
					break;
				case Opcode.DP2Add:
					WriteAssignment("dot({0}, {1}) + {2}",
						GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
					break;
				case Opcode.Crs:
					WriteAssignment("cross({0}.xyz, {1}.xyz)", GetSourceName(instruction, 1), GetSourceName(instruction, 2));
					break;
				case Opcode.Dst:
					WriteAssignment("float4(1, {0}.y * {1}.y, {0}.z, {1}.w)",
						GetSourceName(instruction, 1), GetSourceName(instruction, 2));
					break;
				case Opcode.Dp3:
					WriteAssignment("dot({0}, {1})", GetSourceName(instruction, 1), GetSourceName(instruction, 2));
					break;
				case Opcode.Dp4:
					WriteAssignment("dot({0}, {1})", GetSourceName(instruction, 1), GetSourceName(instruction, 2));
					break;
				case Opcode.Exp:
					WriteAssignment("exp2({0})", GetSourceName(instruction, 1));
					break;
				case Opcode.Frc:
					WriteAssignment("frac({0})", GetSourceName(instruction, 1));
					break;
				case Opcode.If:
					WriteLine("if ({0}) {{", GetSourceName(instruction, 0));
					Indent++;
					break;
				case Opcode.IfC:
					if((IfComparison)instruction.Modifier == IfComparison.GE &&
					   instruction.GetSourceModifier(0) == SourceModifier.AbsAndNegate &&
					   instruction.GetSourceModifier(1) == SourceModifier.Abs &&
					   instruction.GetParamRegisterName(0) + instruction.GetSourceSwizzleName(0) ==
					   instruction.GetParamRegisterName(1) + instruction.GetSourceSwizzleName(1))
					{
						WriteLine("if ({0} == 0) {{",
							instruction.GetParamRegisterName(0) + instruction.GetSourceSwizzleName(0));
					}
					else if((IfComparison)instruction.Modifier == IfComparison.LT &&
					        instruction.GetSourceModifier(0) == SourceModifier.AbsAndNegate &&
					        instruction.GetSourceModifier(1) == SourceModifier.Abs &&
					        instruction.GetParamRegisterName(0) + instruction.GetSourceSwizzleName(0) ==
					        instruction.GetParamRegisterName(1) + instruction.GetSourceSwizzleName(1))
					{
						WriteLine("if ({0} != 0) {{",
							instruction.GetParamRegisterName(0) + instruction.GetSourceSwizzleName(0));
					}
					else
					{
						string ifComparison;
						switch((IfComparison)instruction.Modifier)
						{
							case IfComparison.GT:
								ifComparison = ">";
								break;
							case IfComparison.EQ:
								ifComparison = "==";
								break;
							case IfComparison.GE:
								ifComparison = ">=";
								break;
							case IfComparison.LE:
								ifComparison = "<=";
								break;
							case IfComparison.NE:
								ifComparison = "!=";
								break;
							case IfComparison.LT:
								ifComparison = "<";
								break;
							default:
								throw new InvalidOperationException();
						}

						WriteLine("if ({0} {2} {1}) {{", GetSourceName(instruction, 0), GetSourceName(instruction, 1),
							ifComparison);
					}

					Indent++;
					break;
				case Opcode.BreakC:
				{
					string compareOp;
					switch((IfComparison)instruction.Modifier)
					{
						case IfComparison.GT:
							compareOp = ">";
							break;
						case IfComparison.EQ:
							compareOp = "==";
							break;
						case IfComparison.GE:
							compareOp = ">=";
							break;
						case IfComparison.LE:
							compareOp = "<=";
							break;
						case IfComparison.NE:
							compareOp = "!=";
							break;
						case IfComparison.LT:
							compareOp = "<";
							break;
						default:
							throw new InvalidOperationException();
					}

					// Write full if (...) break; into a single WriteLine
					WriteLine("if ({0} {2} {1}) break;", GetSourceName(instruction, 0), GetSourceName(instruction, 1),
						compareOp);
					break;
				}
				case Opcode.Log:
					WriteAssignment("log2({0})", GetSourceName(instruction, 1));
					break;
				case Opcode.LogP:
					WriteAssignment("log2({0})", GetSourceName(instruction, 1));
					break;
				case Opcode.Lrp:
					WriteAssignment("lerp({2}, {1}, {0})",
						GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
					break;
				case Opcode.Mad:
					WriteAssignment("{0} * {1} + {2}",
						GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
					break;
				case Opcode.Max:
					WriteAssignment("max({0}, {1})", GetSourceName(instruction, 1), GetSourceName(instruction, 2));
					break;
				case Opcode.Min:
					WriteAssignment("min({0}, {1})", GetSourceName(instruction, 1), GetSourceName(instruction, 2));
					break;
				case Opcode.Mov:
					WriteAssignment("{0}", GetSourceName(instruction, 1));
					break;
				case Opcode.MovA:
					WriteAssignment("{0}", GetSourceName(instruction, 1));
					break;
				case Opcode.Mul:
					WriteAssignment("{0} * {1}", GetSourceName(instruction, 1), GetSourceName(instruction, 2));
					break;
				case Opcode.Nrm:
				{
					// the nrm opcode actually only works on the 3D vector
					var operand = GetSourceName(instruction, 1);
					if(instruction.GetDestinationMaskedLength() < 4)
					{
						var swizzle = operand.Swizzle.TrimStart('.');
						switch(swizzle.Length)
						{
							case 0:
							case 4:
								WriteAssignment("normalize({0}.xyz)", operand);
								break;
							case 1:
								// let it reach 3 dimensions
								operand.Swizzle += swizzle;
								operand.Swizzle += swizzle;
								goto case 3;
							case 3:
								WriteAssignment("normalize({0})", operand);
								break;
							default:
								WriteAssignment("({0} / length(float3({0}))", operand);
								break;
						}
					}
					else
					{
						WriteAssignment("({0} / length(float3({0}))", operand);
					}

					break;
				}
				case Opcode.Pow:
					WriteAssignment("pow({0}, {1})", GetSourceName(instruction, 1), GetSourceName(instruction, 2));
					break;
				case Opcode.Rcp:
					WriteAssignment("1.0f / {0}", GetSourceName(instruction, 1));
					break;
				case Opcode.Rsq:
					WriteAssignment("1 / sqrt({0})", GetSourceName(instruction, 1));
					break;
				case Opcode.Sge:
					if(instruction.GetSourceModifier(1) == SourceModifier.AbsAndNegate &&
					   instruction.GetSourceModifier(2) == SourceModifier.Abs &&
					   instruction.GetParamRegisterName(1) + instruction.GetSourceSwizzleName(1) ==
					   instruction.GetParamRegisterName(2) + instruction.GetSourceSwizzleName(2))
					{
						WriteAssignment("({0} == 0) ? 1 : 0", new SourceOperand
						{
							Body = instruction.GetParamRegisterName(1),
							Swizzle = instruction.GetSourceSwizzleName(1),
							Modifier = "{0}"
						});
					}
					else
					{
						WriteAssignment("({0} >= {1}) ? 1 : 0", GetSourceName(instruction, 1),
							GetSourceName(instruction, 2));
					}

					break;
				case Opcode.Slt:
					WriteAssignment("({0} < {1}) ? 1 : 0", GetSourceName(instruction, 1),
						GetSourceName(instruction, 2));
					break;
				case Opcode.SinCos:
				{
					var writeMask = instruction.GetDestinationWriteMask();
					var values = new List<string>(2);
					if(writeMask.HasFlag(ComponentFlags.X))
					{
						values.Add("cos({0})");
					}

					if(writeMask.HasFlag(ComponentFlags.Y))
					{
						values.Add("sin({0})");
					}

					var source = string.Join(", ", values);
					source = values.Count > 1 ? $"float{values.Count}({source})" : source;
					WriteAssignment(source, GetSourceName(instruction, 1));
				}
					break;
				case Opcode.Sub:
					WriteAssignment("{0} - {1}", GetSourceName(instruction, 1), GetSourceName(instruction, 2));
					break;
				case Opcode.Sgn:
					WriteAssignment("sign({0})", GetSourceName(instruction, 1));
					break;
				case Opcode.Tex:
					if(_shader.MajorVersion > 1)
					{
						WriteTextureAssignment(string.Empty, GetSourceName(instruction, 2),
							GetSourceName(instruction, 1), null);
					}
					else if(_shader.MajorVersion == 1 && _shader.MinorVersion >= 4)
					{
						// ps_1_4 texld: dst, src
						// src is usually a texture coordinate register, dst is a color register
						// We'll treat it as a 2D texture sample: dst = tex2D(sampler, src)
						var dst = GetDestinationName(instruction, out var writeMask);
						var sampler = GetSourceName(instruction, 1); // t#
						var uv = new SourceOperand { Body = dst, Swizzle = string.Empty, Modifier = "{0}" };
						WriteIndentedLine("{0} = tex2D({1}, {2});", dst, sampler, uv.Body);
					}
					else
					{
						// ps_1_0-1_3: tex dst
						var dst = GetDestinationName(instruction, out var writeMask);
						WriteIndentedLine("// tex {0}; // Texture sample, implicit texcoord and sampler", dst);
					}

					break;
				case Opcode.TexLDL:
					WriteTextureAssignment("lod", GetSourceName(instruction, 2), GetSourceName(instruction, 1), 4);
					break;
				case Opcode.Comment:
				{
					byte[] bytes = new byte[instruction.Data.Length * sizeof(uint)];
					Buffer.BlockCopy(instruction.Data, 0, bytes, 0, bytes.Length);
					var ascii = FormatUtil.BytesToAscii(bytes);
					WriteIndentedLine($"// Comment: {ascii}");
					break;
				}
				case Opcode.Rep:
					WriteIndentedLine("for (int it{0} = 0; it{0} < {1}; ++it{0}) {{", _iterationDepth,
						GetSourceName(instruction, 0));
					_iterationDepth++;
					Indent++;
					break;
				case Opcode.TexKill:
					if(instruction.GetDestinationResultModifier() is not ResultModifier.None)
					{
						throw new NotImplementedException("Result modifier in texkill");
					}
					WriteIndentedLine("clip({0});", GetDestinationNameWithWriteMask(instruction));
					break;
				case Opcode.DSX:
					WriteAssignment("ddx({0})", GetSourceName(instruction, 1));
					break;
				case Opcode.DSY:
					WriteAssignment("ddy({0})", GetSourceName(instruction, 1));
					break;
				case Opcode.Lit:
					WriteAssignment("lit({0}.x, {0}.y, {0}.w)", GetSourceName(instruction, 1));
					break;
				case Opcode.TexReg2AR:
					// TexReg2AR: Sample texture using .a and .r components of the input register as texture coordinates
					// Example: dest = tex2D(sampler, float2(src.a, src.r));
				{
					string writeMask;
					WriteIndentedLine("{0} = tex2D({1}, float2({2}.a, {2}.r));",
						GetDestinationName(instruction, out writeMask),
						GetSourceName(instruction, 1), // sampler
						GetSourceName(instruction, 0)); // input register
				}
					break;
				case Opcode.TexReg2GB:
					// TexReg2GB: Sample texture using .g and .b components of the input register as texture coordinates
					// Example: dest = tex2D(sampler, float2(src.g, src.b));
				{
					string writeMask;
					WriteIndentedLine("{0} = tex2D({1}, float2({2}.g, {2}.b));",
						GetDestinationName(instruction, out writeMask),
						GetSourceName(instruction, 1), // sampler
						GetSourceName(instruction, 0)); // input register
				}
					break;
				case Opcode.TexCoord:
					// TexCoord: Used to declare texture coordinate input registers in assembly. Output as a comment.
					WriteIndentedLine("// texcoord");
					break;
				case Opcode.ExpP:
					// ExpP: Partial-precision exponential base 2. Output as exp2 for HLSL.
					WriteAssignment("exp2({0})", GetSourceName(instruction, 1));
					break;
				default:
					throw new NotImplementedException(instruction.Opcode.ToString());
			}
		}

		void WriteTemps()
		{
			Dictionary<RegisterKey, int> tempRegisters = new Dictionary<RegisterKey, int>();
			foreach(var inst in _shader.Instructions)
			{
				foreach(var operand in inst.Operands)
				{
					if(operand is DestinationOperand dest)
					{
						if(dest.RegisterType == RegisterType.Temp
						   || (_shader.Type == ShaderType.Vertex && dest.RegisterType == RegisterType.Addr))
						{
							var registerKey = new RegisterKey(dest.RegisterType, dest.RegisterNumber);
							if(!tempRegisters.ContainsKey(registerKey))
							{
								var reg = new RegisterDeclaration(registerKey);
								_registers.RegisterDeclarations[registerKey] = reg;
								tempRegisters[registerKey] = (int)inst.GetDestinationWriteMask();
							}
							else
							{
								tempRegisters[registerKey] |= (int)inst.GetDestinationWriteMask();
							}
						}
					}
				}
			}

			if(tempRegisters.Count == 0) return;
			foreach(IGrouping<int, RegisterKey> group in tempRegisters.GroupBy(
				        kv => kv.Value,
				        kv => kv.Key))
			{
				int writeMask = group.Key;
				string writeMaskName = writeMask switch
				{
					0x1 => "float",
					0x3 => "float2",
					0x7 => "float3",
					0xF => "float4",
					_ => "float4", // TODO
				};
				WriteIndent();
				WriteLine("{0} {1};", writeMaskName, string.Join(", ", group));
			}
		}

		protected override void Write()
		{
			if(_shader.Type == ShaderType.Expression)
			{
				throw new InvalidOperationException(
					$"Expression should be written using {nameof(ExpressionHLSLWriter)} in {nameof(EffectHLSLWriter)}");
			}

			_registers = new RegisterState(_shader);

			foreach(var declaration in _registers.ConstantDeclarations)
			{
				if(_effectWriter?.CommonConstantDeclarations.ContainsKey(declaration.Name) is true)
				{
					// skip common constant declarations
					continue;
				}

				// write constant declaration
				var decompiled = ConstantTypeWriter.Decompile(declaration, _shader);
				var assignment = string.IsNullOrEmpty(decompiled.DefaultValue)
					? string.Empty
					: $" = {decompiled.DefaultValue}";
				if(_outputDefaultValues)
					WriteLine($"{decompiled.Code}{decompiled.RegisterAssignmentString}{assignment};");
				else
					WriteLine($"{decompiled.Code}{decompiled.RegisterAssignmentString};");
			}

			ProcessMethodInputType(out var methodParameters);
			ProcessMethodOutputType(out var methodReturnType, out var methodSemantic);
			WriteLine("{0} {1}({2}){3}",
				methodReturnType,
				_entryPoint,
				methodParameters,
				methodSemantic);
			WriteLine("{");
			Indent++;

			if(_shader.Preshader != null)
			{
				var preshader = PreshaderWriter.Decompile(_shader.Preshader, Indent, out var ctabOverride);
				_registers.CtabOverride = ctabOverride;
				WriteLine(preshader);
			}

			if(_registers.MethodOutputRegisters.Count > 1)
			{
				WriteIndent();
				WriteLine($"{methodReturnType} o;");
			}
			else if(_shader is not { Type: ShaderType.Pixel, MajorVersion: 1 }) // sm1 pixel shader use temp0 as output
			{
				var output = _registers.MethodOutputRegisters.First().Value;
				WriteIndent();
				WriteLine("{0} {1};", methodReturnType, _registers.GetRegisterName(output.RegisterKey));
			}

			WriteTemps();
			bool wroteAssignment = false;
			HlslAst ast = null;
			bool triedAst = false;
			bool astError = false;
			bool astFallback = false;
			bool fallbackToInstructions = false;
			if(_doAstAnalysis)
			{
				triedAst = true;
				Console.WriteLine("[HlslWriter] Starting AST construction");
				var parser = new BytecodeParser();
				try
				{
					ast = parser.Parse(_shader);
					Console.WriteLine("[HlslWriter] AST constructed, starting ReduceTree");
					ast.ReduceTree();
					Console.WriteLine($"[HlslWriter] AST reduced, writing statements (ast.Statements count: {(ast.Statements == null ? -1 : ast.Statements.Count)})");

					if (ast.Statements != null && ast.Statements.Count > 0)
					{
						HlslTreeNode.ClearToHlslCache();
						int statementCount = 0;
						foreach (var statement in ast.Statements)
						{
							if (statement is AssignmentStatement assign)
							{
								var lhs = GetAssignmentTargetName(assign.Target);
								var rhs = assign.Value?.ToHlsl(new HashSet<HlslTreeNode>(), MaxRecursionDepth);

								bool lhsIsOutput = IsOutputTarget(assign.Target, lhs);
								bool rhsIsValid = IsValidAstExpression(rhs, isOutputTarget: lhsIsOutput);
								bool lhsIsValid = IsValidAstExpression(lhs, isOutputTarget: lhsIsOutput);
								bool notIdentity = !IsIdentityAssignment(lhs, rhs);

								if (lhsIsOutput && lhsIsValid && notIdentity)
								{
									var rhsInline = BuildReadableExpression(assign.Value, out var tempLines);
									foreach (var line in tempLines)
									{
										WriteIndent();
										WriteLine(line);
									}
									if (rhsIsValid)
									{
										WriteIndent();
										WriteLine($"{lhs} = {rhsInline};");
										Console.WriteLine($"[HlslWriter]   -> Written: {lhs} = {rhsInline}");
									}
									else
									{
										WriteIndent();
										WriteLine($"{lhs} = /* invalid/cyclic RHS */ {rhsInline};");
										Console.WriteLine($"[HlslWriter]   -> Written with invalid RHS: {lhs} = {rhsInline}");
									}
									wroteAssignment = true;
								}
								else if (!lhsIsOutput && lhsIsValid && rhsIsValid && notIdentity)
								{
									// Emit non-output assignments in instruction order to preserve original flow.
									var rhsInline = BuildReadableExpression(assign.Value, out var tempLines);
									foreach (var line in tempLines)
									{
										WriteIndent();
										WriteLine(line);
									}
									WriteIndent();
									WriteLine($"{lhs} = {rhsInline};");
									wroteAssignment = true;
								}
								else if (!rhsIsValid || !lhsIsValid)
								{
									// Track skipped assignments but don't log each one to avoid slowdown
									string reason = $"[HlslWriter]   -> Skipped invalid/cyclic assignment: {lhs?.Substring(0, Math.Min(lhs?.Length ?? 0, 50))}...";
									Console.WriteLine(reason);
									skippedAssignments.Add(reason);
									// If too many invalid assignments, fallback to instructions
									if (skippedAssignments.Count > MaxAstStatements / 2)
									{
										astFallback = true;
										fallbackToInstructions = true;
										break;
									}
								}
							}
							statementCount++;
							if (statementCount > MaxAstStatements)
							{
								Console.WriteLine("[HlslWriter] Too many statements in AST, falling back");
								astFallback = true;
								fallbackToInstructions = true;
								break;
							}
						}
					}
					if (astFallback)
					{
						WriteLine("// Fallback: AST too large or complex, using simplified AST output");
						WriteAst(ast);
						wroteAssignment = true;
					}
					Console.WriteLine($"[HlslWriter] Statement writing complete (wroteAssignment: {wroteAssignment})");
				}
				catch (Exception ex)
				{
					astError = true;
					WriteLine($"// ERROR: Exception during AST analysis: {ex.Message}");
					Console.WriteLine($"[HlslWriter] ERROR: Exception during AST analysis: {ex}");
					fallbackToInstructions = true;
				}
			}

			if (_verbose && skippedAssignments.Count > 0)
			{
				WriteLine("// Skipped assignments during decompilation:");
				foreach (var skipped in skippedAssignments)
				{
					if (!string.IsNullOrWhiteSpace(skipped))
					{
						WriteLine($"// {skipped}");
					}
				}
				WriteLine();
			}

			// Fallback to instruction-based emission if AST failed or produced no valid assignments
			if ((triedAst && (!wroteAssignment || fallbackToInstructions)) || astError)
			{
				Console.WriteLine("[HlslWriter] AST produced no valid assignments, falling back to instruction-based emission");
				WriteIndentedLine("// Fallback: AST analysis failed or produced no valid assignments. Emitting instructions.");
				WriteInstructionList();
				wroteAssignment = true;
				Console.WriteLine("[HlslWriter] Instruction writing complete (fallback)");
			}

			Console.WriteLine($"[HlslWriter] Before fallback: wroteAssignment={wroteAssignment}, MethodInputRegisters={{_registers.MethodInputRegisters.Count}}, MethodOutputRegisters={{_registers.MethodOutputRegisters.Count}}");
			if (!wroteAssignment && _registers.MethodInputRegisters.Count > 0 && _registers.MethodOutputRegisters.Count > 0)
			{
				Console.WriteLine("[HlslWriter] No assignments written, generating passthrough assignments");
				var inputFieldsBySemantic = _registers.MethodInputRegisters.Values.ToDictionary(x => x.Semantic.ToLowerInvariant(), x => x.Name);
				var inputFieldsByName = _registers.MethodInputRegisters.Values.ToDictionary(x => x.Name.ToLowerInvariant(), x => x.Name);
				List<string> unmappedOutputs = new List<string>();
				foreach (var output in _registers.MethodOutputRegisters.Values)
				{
					var semantic = output.Semantic.ToLowerInvariant();
					var name = output.Name.ToLowerInvariant();
					if (inputFieldsBySemantic.TryGetValue(semantic, out var inputName))
					{
						WriteLine($"o.{output.Name} = i.{inputName};");
						wroteAssignment = true;
					}
					else if (inputFieldsByName.TryGetValue(name, out var inputNameByName))
					{
						WriteLine($"o.{output.Name} = i.{inputNameByName};");
						wroteAssignment = true;
					}
					else
					{
						WriteLine($"o.{output.Name} = 0; // Unmapped output");
						unmappedOutputs.Add(output.Name);
						wroteAssignment = true;
					}
				}
				if (unmappedOutputs.Count > 0)
				{
					WriteLine($"// Warning: The following outputs could not be mapped and were omitted: {string.Join(", ", unmappedOutputs)}");
				}
				Console.WriteLine("[HlslWriter] Passthrough assignments written");
			}

			if (!wroteAssignment)
			{
				WriteLine("// WARNING: No valid assignments or instructions could be decompiled. Output is a stub.");
				WriteLine("// This may be due to excessive recursion, cyclic dependencies, or unsupported shader structure.");
				WriteLine("void main() { /* No code generated */ }");
				Console.WriteLine("[HlslWriter] No code generated, wrote stub function.");
			}

			WriteLine();
			// Only write return if methodReturnType is not void
			if(_registers.MethodOutputRegisters.Count > 1)
			{
				WriteIndent();
				WriteLine($"return o;");
			}
			else if(_registers.MethodOutputRegisters.Count == 1)
			{
				var output = _registers.MethodOutputRegisters.First().Value;
				if (!string.Equals(methodReturnType, "void", StringComparison.OrdinalIgnoreCase))
				{
					WriteIndent();
					WriteLine($"return {_registers.GetRegisterName(output.RegisterKey)};");
				}
			}
			else
			{
				WriteLine("// No output registers found");
			}

			Console.WriteLine("[HlslWriter] Function body writing complete, output should be written now");

			Indent--;
			WriteLine("}");

			// return Writer?.ToString() ?? string.Empty;
		}

		private void WriteDeclarationsAsStruct(string typeName, IEnumerable<RegisterDeclaration> declarations)
		{
			WriteLine($"struct {typeName}");
			WriteLine("{");
			Indent++;
			foreach(var register in declarations)
			{
				WriteIndent();
				WriteLine($"{register.TypeName} {register.Name} : {register.Semantic};");
			}

			Indent--;
			WriteLine("};");
			WriteLine();
		}

		private void ProcessMethodInputType(out string methodParameters)
		{
			var registers = _registers.MethodInputRegisters.Values;
			switch(registers.Count)
			{
				case 0:
					methodParameters = string.Empty;
					break;
				case 1:
					var input = registers.First();
					methodParameters = $"{input.TypeName} {input.Name} : {input.Semantic}";
					break;
				default:
					var inputTypeName = $"{_entryPoint}_Input";
					WriteDeclarationsAsStruct(inputTypeName, registers);
					methodParameters = $"{inputTypeName} i";
					break;
			}
		}

		private void ProcessMethodOutputType(out string methodReturnType, out string methodSemantic)
		{
			var registers = _registers.MethodOutputRegisters.Values;
			switch(registers.Count)
			{
				case 0:
					throw new InvalidOperationException();
				case 1:
					methodReturnType = registers.First().TypeName;
					string semantic = registers.First().Semantic;
					methodSemantic = $" : {semantic}";
					break;
				default:
					methodReturnType = $"{_entryPoint}_Output";
					WriteDeclarationsAsStruct(methodReturnType, registers);
					methodSemantic = string.Empty;
					break;
			}

			;
		}

		private static bool IsValidHlslIdentifier(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return false;
			// Exclude numeric literals
			if (char.IsDigit(s[0])) return false;
			// Exclude type names (namespace or class)
			if (s.Contains("DXDecompiler.")) return false;
			// Exclude other obvious non-identifiers
			if (s == "0" || s == "1" || s == "true" || s == "false") return false;
			// Basic check for valid C# identifier (can be improved)
			return s.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.');
		}

		private static bool IsIdentityAssignment(string lhs, string rhs)
		{
			if (string.IsNullOrEmpty(lhs) || string.IsNullOrEmpty(rhs))
			{
				return false;
			}

			// Compare while skipping spaces to avoid large allocations.
			int i = 0;
			int j = 0;
			while (i < lhs.Length && j < rhs.Length)
			{
				while (i < lhs.Length && lhs[i] == ' ')
				{
					i++;
				}
				while (j < rhs.Length && rhs[j] == ' ')
				{
					j++;
				}

				if (i >= lhs.Length || j >= rhs.Length)
				{
					break;
				}

				if (lhs[i] != rhs[j])
				{
					return false;
				}

				i++;
				j++;
			}

			while (i < lhs.Length && lhs[i] == ' ')
			{
				i++;
			}
			while (j < rhs.Length && rhs[j] == ' ')
			{
				j++;
			}

			return i == lhs.Length && j == rhs.Length;
		}

		private static bool IsValidAstExpression(string value, bool isOutputTarget)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return false;
			}
			if (value.All(char.IsDigit))
			{
				return false;
			}
			// Skip expensive scans for non-output assignments to keep AST emission fast.
			if (!isOutputTarget)
			{
				return true;
			}

			if (value.Contains("unhandled-leaf", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			if (value.Contains("not implemented", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			if (value.Contains("unmapped", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			if (value.Contains("/* ERROR: Max recursion depth", StringComparison.Ordinal))
			{
				return false;
			}
			if (value.Contains("/* ERROR: Cycle detected", StringComparison.Ordinal))
			{
				return false;
			}

			return true;
		}

		private string BuildReadableExpression(HlslTreeNode node, out List<string> tempLines)
		{
			tempLines = new List<string>();
			if (node == null)
			{
				return "0";
			}
			var useCounts = new Dictionary<HlslTreeNode, int>();
			CollectUseCounts(node, useCounts, new HashSet<HlslTreeNode>());
			var costCache = new Dictionary<HlslTreeNode, int>();
			var tempMap = new Dictionary<HlslTreeNode, string>();
			var renderCache = new Dictionary<HlslTreeNode, string>();
			var widthCache = new Dictionary<HlslTreeNode, int>();
			return EmitNodeExpression(node, useCounts, costCache, widthCache, tempMap, tempLines, renderCache, spillRoot: true, new HashSet<HlslTreeNode>(), 0);
		}

		private void CollectUseCounts(HlslTreeNode node, Dictionary<HlslTreeNode, int> counts, HashSet<HlslTreeNode> visited)
		{
			if (node == null || !visited.Add(node))
			{
				return;
			}
			if (!counts.TryAdd(node, 1))
			{
				counts[node]++;
			}
			foreach (var input in node.Inputs)
			{
				CollectUseCounts(input, counts, visited);
			}
		}

		private int EstimateCost(HlslTreeNode node, Dictionary<HlslTreeNode, int> cache)
		{
			if (node == null)
			{
				return 0;
			}
			if (cache.TryGetValue(node, out var cached))
			{
				return cached;
			}
			int cost = 1;
			foreach (var input in node.Inputs)
			{
				cost += EstimateCost(input, cache);
				if (cost > MaxAstExpressionCost * 2)
				{
					break;
				}
			}
			cache[node] = cost;
			return cost;
		}

		private string EmitNodeExpression(
			HlslTreeNode node,
			Dictionary<HlslTreeNode, int> useCounts,
			Dictionary<HlslTreeNode, int> costCache,
			Dictionary<HlslTreeNode, int> widthCache,
			Dictionary<HlslTreeNode, string> tempMap,
			List<string> tempLines,
			Dictionary<HlslTreeNode, string> renderCache,
			bool spillRoot,
			HashSet<HlslTreeNode> visiting,
			int depth)
		{
			if (node == null)
			{
				return "0";
			}
			if (tempMap.TryGetValue(node, out var existing))
			{
				return existing;
			}
			if (renderCache.TryGetValue(node, out var cached))
			{
				return cached;
			}
			if (!visiting.Add(node))
			{
				return "/* cycle */";
			}

			var cost = EstimateCost(node, costCache);
			var shouldSpill = !spillRoot &&
				((useCounts.TryGetValue(node, out var uses) && uses > 1) || cost > MaxAstExpressionCost);

			var expr = BuildNodeExpression(node, useCounts, costCache, widthCache, tempMap, tempLines, renderCache, visiting, depth + 1);
			if (!spillRoot && expr.Length > MaxInlineExpressionLength)
			{
				shouldSpill = true;
			}

			string result;
			if (shouldSpill)
			{
				var tempVar = $"ast_tmp{_astTempIndex++}";
				tempMap[node] = tempVar;
				var width = GetExpressionWidth(node, widthCache);
				tempLines.Add($"{GetTypeName(width)} {tempVar} = {expr};");
				result = tempVar;
			}
			else
			{
				result = expr;
			}

			renderCache[node] = result;
			visiting.Remove(node);
			return result;
		}

		private string BuildNodeExpression(
			HlslTreeNode node,
			Dictionary<HlslTreeNode, int> useCounts,
			Dictionary<HlslTreeNode, int> costCache,
			Dictionary<HlslTreeNode, int> widthCache,
			Dictionary<HlslTreeNode, string> tempMap,
			List<string> tempLines,
			Dictionary<HlslTreeNode, string> renderCache,
			HashSet<HlslTreeNode> visiting,
			int depth)
		{
			if (node is ConstantNode constantNode)
			{
				return Util.ConstantFormatter.FormatFloat(constantNode.Value);
			}
			if (node is RegisterInputNode regInput)
			{
				if (regInput.RegisterComponentKey.Type == RegisterType.Sampler)
				{
					return _registers.GetRegisterName(regInput.RegisterComponentKey.RegisterKey);
				}
				return regInput.ToHlsl(new HashSet<HlslTreeNode>(), 0);
			}
			if (node is TempValueNode tempNode)
			{
				return tempNode.Name;
			}
			if (node is TextureLoadOutputNode texNode)
			{
				var sampler = EmitNodeExpression(texNode.SamplerInput, useCounts, costCache, widthCache, tempMap, tempLines, renderCache, spillRoot: false, visiting, depth);
				var coords = string.Join(", ",
					texNode.TextureCoordinateInputs.Select(tc =>
						EmitNodeExpression(tc, useCounts, costCache, widthCache, tempMap, tempLines, renderCache, spillRoot: false, visiting, depth)));
				return $"tex2D({sampler}, {coords})";
			}
			if (node is NormalizeOutputNode normalizeNode)
			{
				var inputExpr = EmitNodeExpression(normalizeNode.Inputs.FirstOrDefault(), useCounts, costCache, widthCache, tempMap, tempLines, renderCache, spillRoot: false, visiting, depth);
				return $"normalize({inputExpr})";
			}
			if (node is LogOperation logNode)
			{
				var inputExpr = EmitNodeExpression(logNode.Input, useCounts, costCache, widthCache, tempMap, tempLines, renderCache, spillRoot: false, visiting, depth);
				return $"log2({inputExpr})";
			}

			if (node is DXDecompiler.DX9Shader.Decompiler.Operations.Operation op)
			{
				var args = op.Inputs
					.Select(input => EmitNodeExpression(input, useCounts, costCache, widthCache, tempMap, tempLines, renderCache, spillRoot: false, visiting, depth))
					.ToList();
				switch (op.Mnemonic)
				{
					case "add":
						return $"({args[0]} + {args[1]})";
					case "sub":
						return $"({args[0]} - {args[1]})";
					case "mul":
						return $"({args[0]} * {args[1]})";
					case "mad":
					case "madd":
						return $"({args[0]} * {args[1]} + {args[2]})";
					case "min":
						return $"min({args[0]}, {args[1]})";
					case "max":
						return $"max({args[0]}, {args[1]})";
					case "abs":
						return $"abs({args[0]})";
					case "frc":
						return $"frac({args[0]})";
					case "pow":
						return $"pow({args[0]}, {args[1]})";
					case "rcp":
						if (op.Inputs.Count == 1 && op.Inputs[0] is DXDecompiler.DX9Shader.Decompiler.Operations.Operation rcpInputOp)
						{
							if (rcpInputOp.Mnemonic == "rsq" && rcpInputOp.Inputs.Count == 1)
							{
								var baseExpr = EmitNodeExpression(rcpInputOp.Inputs[0], useCounts, costCache, widthCache, tempMap, tempLines, renderCache, false, visiting, depth);
								return $"sqrt({baseExpr})";
							}
						}
						return $"(1.0 / {args[0]})";
					case "rsq":
						if (op.Inputs.Count == 1 && op.Inputs[0] is DXDecompiler.DX9Shader.Decompiler.Operations.Operation rsqInputOp)
						{
							if (rsqInputOp.Mnemonic == "rcp" && rsqInputOp.Inputs.Count == 1 && rsqInputOp.Inputs[0] is DXDecompiler.DX9Shader.Decompiler.Operations.Operation innerOp && innerOp.Mnemonic == "rsq" && innerOp.Inputs.Count == 1)
							{
								var baseExpr = EmitNodeExpression(innerOp.Inputs[0], useCounts, costCache, widthCache, tempMap, tempLines, renderCache, false, visiting, depth);
								return $"rsqrt({baseExpr})";
							}
						}
						return $"rsqrt({args[0]})";
					case "lrp":
						return $"lerp({args[2]}, {args[1]}, {args[0]})";
					case "cmp":
						return $"(({args[0]} >= 0) ? {NormalizeZeroLiteral(args[1])} : {NormalizeZeroLiteral(args[2])})";
					case "sge":
						return $"(({args[0]} >= {args[1]}) ? 1 : 0)";
					case "slt":
						return $"(({args[0]} < {args[1]}) ? 1 : 0)";
					case "cos":
						return $"cos({args[0]})";
					case "sin":
						return $"sin({args[0]})";
					case "sqrt":
						return $"sqrt({args[0]})";
					case "lit":
						return $"lit({args[0]})";
					case "sign":
						return $"sign({args[0]})";
					case "-":
						return $"(-{args[0]})";
					case "dot":
						{
							int n = args.Count / 2;
							var v1 = string.Join(", ", args.Take(n));
							var v2 = string.Join(", ", args.Skip(n));
							var vecType = n switch
							{
								2 => "float2",
								3 => "float3",
								4 => "float4",
								_ => $"float{n}"
							};
							return $"dot({vecType}({v1}), {vecType}({v2}))";
						}
					default:
						return $"{op.Mnemonic}({string.Join(", ", args)})";
				}
			}

			return node.ToHlsl(new HashSet<HlslTreeNode>(), 0);
		}

		private static int GetExpressionWidth(HlslTreeNode node, Dictionary<HlslTreeNode, int> cache)
		{
			if (node == null)
			{
				return 1;
			}
			if (cache.TryGetValue(node, out var cached))
			{
				return cached;
			}

			int width = 1;
			switch (node)
			{
				case ConstantNode:
				case RegisterInputNode:
				case VersionedRegisterInputNode:
				case TempValueNode:
				case TextureLoadOutputNode:
				case NormalizeOutputNode:
				case LogOperation:
					width = 1;
					break;
				case DXDecompiler.DX9Shader.Decompiler.Operations.Operation op:
					switch (op.Mnemonic)
					{
						case "dot":
						case "cmp":
						case "sge":
						case "slt":
						case "rcp":
						case "rsq":
						case "pow":
						case "abs":
						case "frc":
						case "sin":
						case "cos":
						case "sqrt":
						case "sign":
							width = 1;
							break;
						default:
							width = op.Inputs.Count == 0 ? 1 : op.Inputs.Max(i => GetExpressionWidth(i, cache));
							break;
					}
					break;
				default:
					width = 1;
					break;
			}

			cache[node] = width;
			return width;
		}

		private static string GetTypeName(int width)
		{
			return width switch
			{
				1 => "float",
				2 => "float2",
				3 => "float3",
				4 => "float4",
				_ => "float"
			};
		}

		private static string NormalizeZeroLiteral(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return value;
			}
			switch (value.Trim())
			{
				case "-0":
				case "-0.0":
				case "(-0)":
				case "(-0.0)":
					return "0";
				default:
					return value;
			}
		}

		private string GetAssignmentTargetName(HlslTreeNode target)
		{
			if (target is RegisterInputNode registerTarget)
			{
				var registerKey = registerTarget.RegisterComponentKey.RegisterKey;
				string registerName;
				uint registerLength;
				try
				{
					registerName = _registers.GetRegisterName(registerKey);
					registerLength = _registers.GetRegisterFullLength(registerKey);
				}
				catch (KeyNotFoundException)
				{
					// Fallback for registers not declared in RegisterState (e.g., consts in AST targets)
					registerName = registerKey.ToString();
					registerLength = 4;
				}
				if (registerLength == 1)
				{
					return registerName;
				}

				var componentSuffix = registerTarget.RegisterComponentKey.ComponentIndex switch
				{
					0 => ".x",
					1 => ".y",
					2 => ".z",
					3 => ".w",
					_ => string.Empty
				};
				return registerName + componentSuffix;
			}

			return target?.ToHlsl(new HashSet<HlslTreeNode>(), MaxRecursionDepth);
		}

		private bool IsOutputTarget(HlslTreeNode target, string fallbackName)
		{
			if (target is RegisterInputNode registerTarget)
			{
				return _registers.MethodOutputRegisters.ContainsKey(registerTarget.RegisterComponentKey.RegisterKey);
			}

			return fallbackName != null &&
			       (fallbackName.StartsWith("o.") || fallbackName.StartsWith("o[") || fallbackName.StartsWith("out_"));
		}

		private void WriteAst(HlslAst ast)
		{
			if (ast.Statements != null && ast.Statements.Count > 0)
			{
				HlslTreeNode.ClearToHlslCache();
				foreach (var statement in ast.Statements)
				{
					if (statement is AssignmentStatement assign)
					{
						var lhs = GetAssignmentTargetName(assign.Target) ?? assign.Target?.ToString();
						var rhs = assign.Value?.ToString();
						// Only output if LHS is a valid HLSL identifier and not an identity assignment
						if (IsValidHlslIdentifier(lhs) && !IsIdentityAssignment(lhs, rhs))
						{
							WriteIndent();
							WriteLine($"{lhs} = {rhs};");
						}
						// else skip
					}
					else if (statement is IfStatement ifStmt)
					{
						WriteIndent();
						WriteLine($"if ({string.Join(", ", ifStmt.Comparison.Select(c => c.ToString()))}) {{ ... }}");
					}
					else if (statement is ReturnStatement retStmt)
					{
						WriteIndent();
						WriteLine($"return ...;");
					}
					else if (statement is BreakStatement)
					{
						WriteIndent();
						WriteLine($"break;");
					}
					else if (statement is ClipStatement)
					{
						WriteIndent();
						WriteLine($"clip(...);");
					}
					else if (statement is PhiNode)
					{
						continue;
					}
					else
					{
						WriteIndent();
						WriteLine($"// Unhandled statement: {statement.GetType().Name}");
					}
				}
				return;
			}
			WriteAstFormatted(ast);
		}

		private void WriteInstructionList()
		{

			foreach(InstructionToken instruction in _shader.Tokens.OfType<InstructionToken>())
			{
				WriteInstruction(instruction);
			}
		}

		// Integrate pretty-printing logic for AST output
		private void WriteAstFormatted(HlslAst ast)
		{
			// Output function signature (adapt as needed)
			WriteLine("float4 main(float3 texcoord : TEXCOORD) : COLOR");
			WriteLine("{");
			Indent++;
			WriteLine(); // Ensure a blank line after function opening
			// Output all root assignments in the AST (using Roots, not Statements)
			var compiler = new NodeCompiler(_registers);
			var rootGroups = ast.Roots.GroupBy(r => r.Key.RegisterKey);
			int assignmentCount = 0;
			foreach(var rootGroup in rootGroups)
			{
				var registerKey = rootGroup.Key;
				var roots = rootGroup.OrderBy(r => r.Key.ComponentIndex).Select(r => r.Value).ToList();
				RegisterDeclaration outputRegister = _registers.MethodOutputRegisters[registerKey];
				string statement = compiler.Compile(roots, roots.Count);
				WriteLine($"o.{outputRegister.Name} = {statement};");
				assignmentCount++;
			}

			// Force passthrough assignments for all output fields if no assignments were written
			if (assignmentCount == 0 && _registers.MethodInputRegisters.Count > 0 && _registers.MethodOutputRegisters.Count > 0)
			{
				var inputFieldsBySemantic = _registers.MethodInputRegisters.Values.ToDictionary(x => x.Semantic.ToLowerInvariant(), x => x.Name);
				var inputFieldsByName = _registers.MethodInputRegisters.Values.ToDictionary(x => x.Name.ToLowerInvariant(), x => x.Name);
				foreach (var output in _registers.MethodOutputRegisters.Values)
				{
					var semantic = output.Semantic.ToLowerInvariant();
					var name = output.Name.ToLowerInvariant();
					if (inputFieldsBySemantic.TryGetValue(semantic, out var inputName))
					{
						WriteLine($"o.{output.Name} = i.{inputName};");
					}
					else if (inputFieldsByName.TryGetValue(name, out var inputNameByName))
					{
						WriteLine($"o.{output.Name} = i.{inputNameByName};");
					}
					else
					{
						WriteLine($"// o.{output.Name} = ...;");
					}
				}
			}

			WriteLine();
			WriteLine("return o;");
			Indent--;
			WriteLine("}");
		}

		private static string FormatHlslFloat(float value)
		{
			string formatted = Math.Abs(value) >= 1e4f || (Math.Abs(value) > 0 && Math.Abs(value) < 1e-3f)
				? value.ToString("0.######E+0", System.Globalization.CultureInfo.InvariantCulture)
				: value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
			// if (Math.Abs(value) > 1e6f)
			// 	formatted += " /* Unusually large value */";
			// else if (Math.Abs(value) > 0 && Math.Abs(value) < 1e-6f)
			// 	formatted += " /* Unusually small value */";
			return formatted;
		}

		private static string FormatHlslFloatArray(IEnumerable<float> values)
		{
			return string.Join(", ", values.Select(FormatHlslFloat));
		}
	}
}
