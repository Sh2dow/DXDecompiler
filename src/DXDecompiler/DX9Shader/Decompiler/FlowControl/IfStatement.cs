// Ported from HlslDecompiler: IfStatement AST node
// ...full implementation from HlslDecompiler/Hlsl/FlowControl/IfStatement.cs...

using System.Collections.Generic;
using System.Linq;

namespace DXDecompiler.DX9Shader.Decompiler.FlowControl
{
    public class IfStatement : IStatement
    {
        public HlslTreeNode[] Comparison { get; }
        public IList<IStatement> TrueBody { get; set; } = new List<IStatement>();
        public IList<IStatement> FalseBody { get; set; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

        public bool IsTrueParsed { get; set; } = false;
        public bool IsParsed { get; set; } = false;

        public IfStatement(HlslTreeNode[] comparison, IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
        {
            Comparison = comparison;
            Inputs = new Dictionary<RegisterComponentKey, HlslTreeNode>(inputs);
            Outputs = new Dictionary<RegisterComponentKey, HlslTreeNode>(inputs);
        }

        public override string ToString()
        {
            return "if (" + string.Join(", ", Comparison.Select(c => c.ToString())) + ")";
        }
    }
}
