namespace DXDecompiler.DX9Shader.Decompiler.Operations
{
	public class DerivativeXOperation : UnaryOperation
	{
		public DerivativeXOperation(HlslTreeNode value)
		{
			AddInput(value);
		}

		public override string Mnemonic => "ddx";
	}
}
