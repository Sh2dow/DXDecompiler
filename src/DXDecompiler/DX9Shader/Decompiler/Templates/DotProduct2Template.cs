using DXDecompiler.DX9Shader.Decompiler.Operations;
using DXDecompiler.DX9Shader.Decompiler.TemplateMatch;

namespace DXDecompiler.DX9Shader.Decompiler.Templates
{
    public class DotProduct2Template : NodeTemplate<AddOperation>
    {
        public override bool Match(HlslTreeNode node)
        {
            if (node is AddOperation add)
            {
                var a = add.Inputs[0];
                var b = add.Inputs[1];
                // Add matching logic here
                return true; // Placeholder
            }
            return false;
        }
        public override HlslTreeNode Reduce(AddOperation node)
        {
            // Placeholder: just return the node unchanged
            return node;
        }
    }
}
