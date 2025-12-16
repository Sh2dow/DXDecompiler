// Ported from HlslDecompiler.Hlsl.TemplateMatch.MultiplyZeroTemplate

using DXDecompiler.DX9Shader.Decompiler.Operations;

namespace DXDecompiler.DX9Shader.Decompiler.TemplateMatch
{
    public class MultiplyZeroTemplate : NodeTemplate<MultiplyOperation>
    {
        public override bool Match(HlslTreeNode node)
        {
            if(node is MultiplyOperation mul)
            {
                return (mul.Factor1 is ConstantNode c1 && c1.Value == 0) ||
                       (mul.Factor2 is ConstantNode c2 && c2.Value == 0);
            }
            return false;
        }
        public override HlslTreeNode Reduce(MultiplyOperation node)
        {
            if(node.Factor1 is ConstantNode c1 && c1.Value == 0)
            {
                return node.Factor1;
            }
            if(node.Factor2 is ConstantNode c2 && c2.Value == 0)
            {
                return node.Factor2;
            }
            return node;
        }
    }
}

