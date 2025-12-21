namespace DXDecompiler.DX9Shader.Decompiler.Operations
{
	public class DerivativeYOperation : UnaryOperation
	{
		public DerivativeYOperation(HlslTreeNode value)
		{
			AddInput(value);
		}

		public override string Mnemonic => "ddy";
	}
}
