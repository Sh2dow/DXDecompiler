using System.Collections.Generic;

namespace DXDecompiler.DX9Shader.Decompiler.FlowControl
{
    public class StatementFinalizer
    {
        private IList<IStatement> _statements;

        private StatementFinalizer(IList<IStatement> statements)
        {
            _statements = statements;
        }

        public static void Finalize(IList<IStatement> statements)
        {
            var finalizer = new StatementFinalizer(statements);
            finalizer.FinalizeStatements();
        }

        private void FinalizeStatements()
        {
            // TODO: Port and implement all statement optimization logic here
        }
    }
}
