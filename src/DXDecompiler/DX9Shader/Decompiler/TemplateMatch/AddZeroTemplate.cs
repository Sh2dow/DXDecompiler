// Ported from HlslDecompiler.Hlsl.TemplateMatch.AddZeroTemplate

using DXDecompiler.DX9Shader.Decompiler.Operations;

namespace DXDecompiler.DX9Shader.Decompiler.TemplateMatch
{
    public class AddZeroTemplate : NodeTemplate<DXDecompiler.DX9Shader.Decompiler.Operations.AddOperation>
    {
        public override bool Match(HlslTreeNode node)
        {
            if(node is DXDecompiler.DX9Shader.Decompiler.Operations.AddOperation add)
            {
                return (add.Addend1 is ConstantNode c1 && c1.Value == 0) ||
                       (add.Addend2 is ConstantNode c2 && c2.Value == 0);
            }
            return false;
        }
        public override HlslTreeNode Reduce(DXDecompiler.DX9Shader.Decompiler.Operations.AddOperation node)
        {
            if(node.Addend1 is ConstantNode c1 && c1.Value == 0)
            {
                return node.Addend2;
            }
            if(node.Addend2 is ConstantNode c2 && c2.Value == 0)
            {
                return node.Addend1;
            }
            return node;
        }
    }
}
