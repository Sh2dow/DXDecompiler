// Ported from HlslDecompiler.Hlsl.TemplateMatch.AddConstantsTemplate

using DXDecompiler.DX9Shader.Decompiler.Operations;

namespace DXDecompiler.DX9Shader.Decompiler.TemplateMatch
{
    public class AddConstantsTemplate : NodeTemplate<AddOperation>
    {
        public override bool Match(HlslTreeNode node)
        {
            if(node is AddOperation add)
            {
                return add.Inputs[0] is ConstantNode && add.Inputs[1] is ConstantNode;
            }
            return false;
        }
        public override HlslTreeNode Reduce(AddOperation node)
        {
            if (node.Inputs[0] is ConstantNode left && node.Inputs[1] is ConstantNode right)
            {
                return new ConstantNode(left.Value + right.Value);
            }
            return node;
        }
    }
}
