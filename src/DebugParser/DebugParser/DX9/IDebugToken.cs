using DXDecompiler.DX9Shader;
using System.Collections.Generic;
using DXDecompiler.DX9Shader.Bytecode;

namespace DebugParser.DebugParser.DX9
{
	public interface IDebugToken
	{
		Opcode Opcode { get; set; }
		List<IDebugOperand> Operands { get; set; }
	}
}
