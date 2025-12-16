using System.Text;
using DXDecompiler.Util;

namespace DXDecompiler.Chunks.Fx10.Assignemnt
{
	public class EffectConstantIndexAssignment : EffectAssignment
	{
		public string ArrayName { get; private set; }
		public uint Index { get; private set; }

		public new static EffectConstantIndexAssignment Parse(BytecodeReader reader, BytecodeReader assignmentReader)
		{
			var result = new EffectConstantIndexAssignment();
			var arrayNameOffset = assignmentReader.ReadUInt32();
			var arrayNameReader = reader.CopyAtOffset((int)arrayNameOffset);
			result.ArrayName = arrayNameReader.ReadString();
			result.Index = assignmentReader.ReadUInt32();
			return result;
		}
		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.Append(MemberType.ToString());
			sb.Append(" = ");
			sb.Append(ArrayName);
			sb.Append("[");
			sb.Append(Index);
			sb.Append("];");
			return sb.ToString();
		}
	}
}
