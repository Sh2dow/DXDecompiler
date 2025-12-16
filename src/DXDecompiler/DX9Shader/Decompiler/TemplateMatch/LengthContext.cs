// Ported from HlslDecompiler: LengthContext for template matching
// ...full implementation from HlslDecompiler/Hlsl/TemplateMatch/LengthContext.cs...

namespace DXDecompiler.DX9Shader.Decompiler.TemplateMatch
{
    public class LengthContext : IGroupContext
    {
        public LengthContext(GroupNode value)
        {
            Value = value;
        }

        public GroupNode Value { get; private set; }
    }
}
