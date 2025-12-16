// Ported from HlslDecompiler.Hlsl.TemplateMatch.MultiplyAddTemplate

using DXDecompiler.DX9Shader.Decompiler.Operations;

namespace DXDecompiler.DX9Shader.Decompiler.TemplateMatch
{
    public class MultiplyAddTemplate : NodeTemplate<MultiplyAddOperation>
    {
        public override HlslTreeNode Reduce(MultiplyAddOperation node)
        {
            var multiplication = new MultiplyOperation(node.Factor1, node.Factor2);
            return new AddOperation(multiplication, node.Addend);
        }

        public override bool Match(HlslTreeNode node)
        {
            return node is MultiplyAddOperation;
        }
    }
}
