using System.Collections.Generic;

namespace DXDecompiler.DX9Shader.Decompiler.FlowControl
{
    public class ClipStatement : IStatement
    {
        public HlslTreeNode[] Values { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

        public ClipStatement(HlslTreeNode[] values, IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
        {
            Values = values;
            Inputs = new Dictionary<RegisterComponentKey, HlslTreeNode>(inputs);
            Outputs = new Dictionary<RegisterComponentKey, HlslTreeNode>(inputs);
        }
    }
}
