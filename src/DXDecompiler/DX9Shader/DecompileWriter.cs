using System.IO;
using System.Text;

namespace DXDecompiler.DX9Shader
{
	public class DecompileWriter
	{
		public int Indent;
		protected TextWriter Writer;

		// Diagnostic: track if any output was written
		private bool _hasOutput = false;

		protected void WriteIndent()
		{
			Writer.Write(new string(' ', Indent * 4));
		}
		protected void WriteLine()
		{
			Writer.WriteLine();
			_hasOutput = true;
		}
		protected void WriteLine(string value)
		{
			Writer.WriteLine(value);
			_hasOutput = true;
		}
		protected void WriteLine(string format, params object[] args)
		{
			Writer.WriteLine(format, args);
			_hasOutput = true;
		}
		protected void Write(string value)
		{
			Writer.Write(value);
			_hasOutput = true;
		}
		protected void Write(string format, params object[] args)
		{
			Writer.Write(format, args);
			_hasOutput = true;
		}
		protected void WriteIndentedLine(string value)
		{
			WriteIndent();
			Writer.WriteLine(value);
			_hasOutput = true;
		}
		protected void WriteIndentedLine(string format, params object[] args)
		{
			WriteIndent();
			Writer.WriteLine(format, args);
			_hasOutput = true;
		}

		protected virtual void Write()
		{
			// To be implemented by derived classes
			// If WriteAst or WriteInstructionList are implemented, call them here for diagnostics
			var type = GetType();
			var writeAst = type.GetMethod("WriteAst", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
			if(writeAst != null)
			{
				writeAst.Invoke(this, null);
				_hasOutput = true;
			}
			var writeInstr = type.GetMethod("WriteInstructionList", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
			if(writeInstr != null)
			{
				writeInstr.Invoke(this, null);
				_hasOutput = true;
			}
		}

		protected void WriteSkippedAssignment(string reason, string details = null)
		{
			Writer.WriteLine("// Skipped assignment: {0}{1}", reason, details != null ? $" ({details})" : "");
			_hasOutput = true;
		}

		// Diagnostic: Write a message if recursion/cyclic issues are detected
		protected void WriteDiagnostic(string message)
		{
			Writer.WriteLine("// DIAGNOSTIC: {0}", message);
			_hasOutput = true;
		}

		public string Decompile()
		{
			try
			{
				using(var stream = new MemoryStream())
				{
					Writer = new StreamWriter(stream);
					try
					{
						Write();
					}
					catch(System.Exception ex)
					{
						Writer.WriteLine("// ERROR: Exception during decompilation: {0}", ex.Message);
					}
					Writer.Flush();
					stream.Position = 0;
					using(var reader = new StreamReader(stream, Encoding.UTF8))
					{
						var result = reader.ReadToEnd();
						if(string.IsNullOrWhiteSpace(result) || !_hasOutput)
						{
							return "// WARNING: No code could be decompiled. Output is a stub.\nvoid main() { /* No code generated */ }\n";
						}
						return result;
					}
				}
			}
			catch(System.Exception ex)
			{
				return $"// ERROR: Failed to decompile shader: {ex.Message}\nvoid main() {{ /* No code generated */ }}\n";
			}
		}
		public void SetStream(StreamWriter str)
		{
			Writer = str;
		}
		// Integration point: If WriteAst or WriteInstructionList are needed, add them here and ensure they are called from Write().
	}
}
