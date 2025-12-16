namespace DXDecompiler.DX9Shader.Decompiler.Operations
{
	public class FractionalOperation : UnaryOperation
	{
		public FractionalOperation(HlslTreeNode value)
		{
			AddInput(value);
		}

		public override string Mnemonic => "frc";
	}
}
