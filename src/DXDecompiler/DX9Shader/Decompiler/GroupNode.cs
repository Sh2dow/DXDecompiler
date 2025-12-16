using System.Linq;

namespace DXDecompiler.DX9Shader.Decompiler
{
    public class GroupNode : HlslTreeNode
    {
        public GroupNode(params HlslTreeNode[] components)
        {
            foreach (HlslTreeNode component in components)
            {
                AddInput(component);
            }
        }

        public int Length => Inputs.Count;

        public HlslTreeNode this[int index]
        {
            get => Inputs[index];
            set => Inputs[index] = value;
        }

        public override string ToString()
        {
            return $"({string.Join(",", Inputs)})";
        }

        public override string ToHlsl(System.Collections.Generic.HashSet<HlslTreeNode> visited, int depth)
        {
            if (depth > 128)
                return "/*max depth reached*/";
            if (!visited.Add(this))
                return "/*cycle detected*/";
            var result = $"({string.Join(", ", Inputs.Select(i => i?.ToHlsl(visited, depth + 1) ?? "null"))})";
            visited.Remove(this);
            return result;
        }
    }
}
