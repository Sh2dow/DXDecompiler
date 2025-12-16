// Ported from HlslDecompiler.Hlsl.TemplateMatch.CompareConstantTemplate
namespace DXDecompiler.DX9Shader.Decompiler.TemplateMatch
{
    public class CompareConstantTemplate : NodeTemplate<ComparisonNode>
    {
        public override bool Match(HlslTreeNode node)
        {
            if(node is ComparisonNode cmp)
            {
                return cmp.Left is ConstantNode && cmp.Right is ConstantNode;
            }
            return false;
        }
        public override HlslTreeNode Reduce(ComparisonNode node) { return node; /* TODO: Implement logic */ }
    }
}
