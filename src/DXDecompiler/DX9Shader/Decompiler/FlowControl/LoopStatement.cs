using System.Collections.Generic;

namespace DXDecompiler.DX9Shader.Decompiler.FlowControl
{
    public class LoopStatement : IStatement
    {
        public IList<IStatement> Body { get; set; } = new List<IStatement>();
        public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

        public LoopStatement(IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
        {
            Inputs = new Dictionary<RegisterComponentKey, HlslTreeNode>(inputs);
            Outputs = new Dictionary<RegisterComponentKey, HlslTreeNode>(inputs);
        }
    }
}
