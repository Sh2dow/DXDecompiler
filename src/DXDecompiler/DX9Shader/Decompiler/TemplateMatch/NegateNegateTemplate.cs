// Ported from HlslDecompiler.Hlsl.TemplateMatch.NegateNegateTemplate

using DXDecompiler.DX9Shader.Decompiler.Operations;

namespace DXDecompiler.DX9Shader.Decompiler.TemplateMatch
{
    public class NegateNegateTemplate : NodeTemplate<NegateOperation>
    {
        public override bool Match(HlslTreeNode node)
        {
            return node is NegateOperation neg && neg.Inputs.Count == 1 && neg.Inputs[0] is NegateOperation;
        }
        public override HlslTreeNode Reduce(NegateOperation node)
        {
            // -(-x) => x
            if(node.Inputs.Count == 1 && node.Inputs[0] is NegateOperation innerNeg)
            {
                return innerNeg.Inputs[0];
            }
            return node;
        }
    }
}

