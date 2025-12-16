namespace DXDecompiler.DX9Shader.Decompiler.Operations
{
    public class DotProductOperation : Operation
    {
        public GroupNode X { get; }
        public GroupNode Y { get; }
        public DotProductOperation(GroupNode x, GroupNode y)
        {
            X = x;
            Y = y;
            AddInput(x);
            AddInput(y);
        }
        public override string Mnemonic => "dp";
    }
}

