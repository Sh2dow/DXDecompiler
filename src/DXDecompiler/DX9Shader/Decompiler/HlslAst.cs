using System.Collections.Generic;
using System.Linq;
using DXDecompiler.DX9Shader.Decompiler.Compiler;
using DXDecompiler.DX9Shader.Decompiler.TemplateMatch;
using DXDecompiler.DX9Shader.Decompiler.FlowControl;

namespace DXDecompiler.DX9Shader.Decompiler
{
	public class HlslAst
	{
		public Dictionary<RegisterComponentKey, HlslTreeNode> Roots { get; private set; }
		public List<IStatement> Statements { get; private set; } // New: statement-based AST

		public HlslAst(Dictionary<RegisterComponentKey, HlslTreeNode> roots)
		{
			Roots = roots;
		}

		public HlslAst(List<IStatement> statements)
		{
			Statements = statements;
		}

		public void ReduceTree()
		{
			if (Roots != null)
			{
				var grouper = new NodeGrouper(null); // TODO: Pass actual RegisterState if available
				var matcher = new TemplateMatcher(grouper);
				Roots = Roots.ToDictionary(r => r.Key, r => matcher.Reduce(r.Value));
				// Finalize node input order for all roots
				NodeFinalizer.Finalize(Roots.Values.ToList());
			}
			if (Statements != null)
			{
				// TODO: Add statement-level template matching/finalization if needed
			}
		}
	}
}
