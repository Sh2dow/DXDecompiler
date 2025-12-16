namespace DXDecompiler.DX9Shader.Decompiler.Operations
{
	public abstract class UnaryOperation : Operation
	{
		public HlslTreeNode Value => Inputs[0];
	}
}
