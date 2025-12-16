// Ported from HlslDecompiler: IStatement interface
using System.Collections.Generic;

namespace DXDecompiler.DX9Shader.Decompiler.FlowControl
{
    public interface IStatement
    {
        IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
        IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }
    }
}
