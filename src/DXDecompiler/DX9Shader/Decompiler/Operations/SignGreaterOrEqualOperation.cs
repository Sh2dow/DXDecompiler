namespace DXDecompiler.DX9Shader.Decompiler.Operations
{
	public class SignGreaterOrEqualOperation : Operation
	{
		public SignGreaterOrEqualOperation(HlslTreeNode value1, HlslTreeNode value2)
		{
			AddInput(value1);
			AddInput(value2);
		}

		public override string Mnemonic => "sge";
	}
}
