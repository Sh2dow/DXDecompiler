using DXDecompiler.DX9Shader.Decompiler.TemplateMatch;

// Ported from HlslDecompiler: IGroupTemplate interface
// ...full implementation from HlslDecompiler/Hlsl/TemplateMatch/IGroupTemplate.cs...

namespace DXDecompiler.DX9Shader.Decompiler.TemplateMatch
{
    public interface IGroupTemplate
    {
        IGroupContext Match(HlslTreeNode node);
        HlslTreeNode Reduce(HlslTreeNode node, IGroupContext groupContext);
    }
}
