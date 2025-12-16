using System;

namespace DXDecompiler.DX9Shader.Decompiler.TemplateMatch
{
    public abstract class NodeTemplate<T> : INodeTemplate where T : HlslTreeNode
    {
        public abstract bool Match(HlslTreeNode node);
        public abstract HlslTreeNode Reduce(T node);
        public HlslTreeNode Reduce(HlslTreeNode node)
        {
            if (!(node is T tNode))
                throw new ArgumentException($"Node is not of type {typeof(T).Name}");
            return Reduce(tNode);
        }
    }
}
