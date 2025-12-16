// Ported from HlslDecompiler.Hlsl.TemplateMatch.MoveTemplate

using DXDecompiler.DX9Shader.Decompiler.Operations;

namespace DXDecompiler.DX9Shader.Decompiler.TemplateMatch
{
    public class MoveTemplate : NodeTemplate<MoveOperation>
    {
        public override bool Match(HlslTreeNode node)
        {
            // Always match MoveOperation for simplification
            return node is MoveOperation;
        }
        public override HlslTreeNode Reduce(MoveOperation node)
        {
            // If the input is a MoveOperation, flatten it
            if(node.Inputs.Count == 1 && node.Inputs[0] is MoveOperation innerMove)
            {
                return innerMove.Inputs[0];
            }
            return node;
        }
    }
}

