using System.Collections.Generic;

namespace DXDecompiler.DX9Shader.Decompiler.FlowControl
{
    public class ReturnStatement : IStatement
    {
        public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

        public ReturnStatement(IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
        {
            Inputs = new Dictionary<RegisterComponentKey, HlslTreeNode>(inputs);
            Outputs = new Dictionary<RegisterComponentKey, HlslTreeNode>(inputs);
        }
    }
}
