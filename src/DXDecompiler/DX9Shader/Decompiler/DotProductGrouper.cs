using DXDecompiler.DX9Shader.Decompiler.Operations;

namespace DXDecompiler.DX9Shader.Decompiler
{
    public class DotProductGrouper
    {
        // ...existing code...
        public void SomeMethod(HlslTreeNode node)
        {
            if (node is DXDecompiler.DX9Shader.Decompiler.Operations.AddOperation add)
            {
                var a = add.Addend1;
                var b = add.Addend2;
                // ...existing code...
            }
        }
        // ...existing code...
    }
}
