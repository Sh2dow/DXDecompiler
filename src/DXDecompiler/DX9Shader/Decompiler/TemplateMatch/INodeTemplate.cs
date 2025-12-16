// Ported from HlslDecompiler: INodeTemplate interface
// ...full implementation from HlslDecompiler/Hlsl/TemplateMatch/INodeTemplate.cs...

namespace DXDecompiler.DX9Shader.Decompiler.TemplateMatch
{
    public interface INodeTemplate
    {
        bool Match(HlslTreeNode node);
        HlslTreeNode Reduce(HlslTreeNode node);
    }
}
