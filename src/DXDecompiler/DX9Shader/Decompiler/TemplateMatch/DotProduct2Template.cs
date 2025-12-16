// Ported from HlslDecompiler.Hlsl.TemplateMatch.DotProduct2Template

using DXDecompiler.DX9Shader.Decompiler.Operations;

namespace DXDecompiler.DX9Shader.Decompiler.TemplateMatch
{
    public class DotProduct2Template : IGroupTemplate
    {
        private readonly TemplateMatcher _matcher;
        public DotProduct2Template(TemplateMatcher matcher) { _matcher = matcher; }

        public IGroupContext Match(HlslTreeNode node)
        {
            // Match Add(Multiply(a.x, b.x), Multiply(a.y, b.y))
            if (node is AddOperation add &&
                add.Inputs[0] is MultiplyOperation mul1 &&
                add.Inputs[1] is MultiplyOperation mul2)
            {
                // Check if the factors are from the same two sources, but swizzled .x and .y
                var a1 = mul1.Factor1;
                var b1 = mul1.Factor2;
                var a2 = mul2.Factor1;
                var b2 = mul2.Factor2;
                // Accept both (a.x*b.x + a.y*b.y) and (a.y*b.y + a.x*b.x)
                if ((a1 == a2 && b1 != b2) || (a1 != a2 && b1 == b2))
                {
                    return new DotProduct2Context(a1, b1, a2, b2);
                }
            }
            return null;
        }

        public HlslTreeNode Reduce(HlslTreeNode node, IGroupContext groupContext)
        {
            if (groupContext is DotProduct2Context ctx)
            {
                // Replace with a DotProduct2Operation node
                return new DotProduct2Operation(ctx.A, ctx.B);
            }
            return node;
        }
    }

    public class DotProduct2Context : IGroupContext
    {
        public HlslTreeNode A { get; }
        public HlslTreeNode B { get; }
        public HlslTreeNode A2 { get; }
        public HlslTreeNode B2 { get; }
        public DotProduct2Context(HlslTreeNode a, HlslTreeNode b, HlslTreeNode a2, HlslTreeNode b2)
        {
            A = a;
            B = b;
            A2 = a2;
            B2 = b2;
        }
    }

    public class DotProduct2Operation : Operation
    {
        public DotProduct2Operation(HlslTreeNode a, HlslTreeNode b)
        {
            AddInput(a);
            AddInput(b);
        }
        public override string Mnemonic => "dp2";
    }
}
