using System.Collections.Generic;

namespace DXDecompiler.DX9Shader.Decompiler.FlowControl
{
    // Represents an assignment statement in the AST
    public class AssignmentStatement : IStatement
    {
        public HlslTreeNode Target { get; }
        public HlslTreeNode Value { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

        public AssignmentStatement(HlslTreeNode target, HlslTreeNode value, IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
        {
            Target = target;
            Value = value;
            Inputs = new Dictionary<RegisterComponentKey, HlslTreeNode>(inputs);
            Outputs = new Dictionary<RegisterComponentKey, HlslTreeNode>(inputs);
        }
    }
}
