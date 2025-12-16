namespace DXDecompiler.DX9Shader.Decompiler.Operations
{
	public class AbsoluteOperation : UnaryOperation
	{
		public AbsoluteOperation(HlslTreeNode value)
		{
			AddInput(value);
		}

		public override string Mnemonic => "abs";
	}
}
