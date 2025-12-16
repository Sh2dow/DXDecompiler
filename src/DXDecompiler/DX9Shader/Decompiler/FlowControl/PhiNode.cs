using System.Collections.Generic;
using System.Linq;
using DXDecompiler.DX9Shader.Bytecode; // For RegisterType

namespace DXDecompiler.DX9Shader.Decompiler.FlowControl
{
    public class PhiNode : IStatement
    {
        // Store phi node inputs as a dictionary for IStatement compatibility
        private readonly Dictionary<RegisterComponentKey, HlslTreeNode> _inputs = new();
        private readonly Dictionary<RegisterComponentKey, HlslTreeNode> _outputs = new();

        // IStatement implementation
        public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs => _inputs;
        public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs => _outputs;

        // Add an input to the phi node
        public void AddInput(HlslTreeNode node)
        {
            // Use RegisterInputNode.RegisterComponentKey.RegisterKey if available, else dummy key
            var key = node is RegisterInputNode rin ? rin.RegisterComponentKey.RegisterKey : new RegisterKey(RegisterType.Temp, 0);
            var compKey = node is RegisterInputNode rin2 ? rin2.RegisterComponentKey : new RegisterComponentKey(key, 0);
            _inputs[compKey] = node;
        }

        public PhiNode(params HlslTreeNode[] inputs)
        {
            foreach (HlslTreeNode input in inputs)
            {
                AddInput(input);
            }
        }

        public override string ToString()
        {
            return "phi(" + string.Join(", ", _inputs.Values.Select(i => i.ToString())) + ")";
        }
    }
}
