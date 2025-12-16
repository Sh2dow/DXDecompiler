using System.Collections.Generic;
using DXDecompiler.DX9Shader.Decompiler.Compiler;
using System;

namespace DXDecompiler.DX9Shader.Decompiler.TemplateMatch
{
    public class TemplateMatcher
    {
        private List<INodeTemplate> _templates;
        private List<IGroupTemplate> _groupTemplates;
        private NodeGrouper _nodeGrouper;

        public TemplateMatcher(NodeGrouper nodeGrouper)
        {
            _templates = new List<INodeTemplate>
            {
                new AddConstantsTemplate(),
                new AddZeroTemplate(),
                new CompareConstantTemplate(),
                new MultiplyAddTemplate(),
                new MultiplyOneTemplate(),
                new MultiplyZeroTemplate(),
                new MoveTemplate(),
                new NegateNegateTemplate(),
                // ...add more templates as ported...
            };
            _groupTemplates = new List<IGroupTemplate>
            {
                new DotProduct2Template(this),
                // ...add more group templates as ported...
            };
            _nodeGrouper = nodeGrouper;
        }

        private const int MaxRecursionDepth = 128;
        private const int MaxTotalReductions = 10000;
        private int _totalReductions = 0;
        private HlslTreeNode ReduceDepthFirst(HlslTreeNode node, HashSet<HlslTreeNode> visited = null, int depth = 0)
        {
            if (visited == null) visited = new HashSet<HlslTreeNode>();
            if (depth > MaxRecursionDepth)
            {
                Console.WriteLine($"[TemplateMatcher] Recursion limit reached at depth {depth} for node {node}");
                return node;
            }
            if (!visited.Add(node))
            {
                Console.WriteLine($"[TemplateMatcher] Node already visited: {node}");
                return node;
            }
            if (++_totalReductions > MaxTotalReductions)
            {
                Console.WriteLine($"[TemplateMatcher] Max total reductions reached: {_totalReductions}");
                return node;
            }
            if (ConstantMatcher.IsConstant(node) || IsRegister(node))
            {
                return node;
            }
            for (int i = 0; i < node.Inputs.Count; i++)
            {
                HlslTreeNode input = node.Inputs[i];
                node.Inputs[i] = ReduceDepthFirst(input, visited, depth + 1);
            }
            foreach (INodeTemplate template in _templates)
            {
                if (template.Match(node))
                {
                    var replacement = template.Reduce(node);
                    if (!ReferenceEquals(replacement, node))
                    {
                        Console.WriteLine($"[TemplateMatcher] Node replaced by template {template.GetType().Name} at depth {depth}");
                        Replace(node, replacement);
                        return ReduceDepthFirst(replacement, visited, depth + 1);
                    }
                }
            }
            foreach (IGroupTemplate template in _groupTemplates)
            {
                IGroupContext groupContext = template.Match(node);
                if (groupContext != null)
                {
                    var replacement = template.Reduce(node, groupContext);
                    Console.WriteLine($"[TemplateMatcher] Node replaced by group template {template.GetType().Name} at depth {depth}");
                    Replace(node, replacement);
                    return ReduceDepthFirst(replacement, visited, depth + 1);
                }
            }
            return node;
        }

        public HlslTreeNode Reduce(HlslTreeNode node)
        {
            _totalReductions = 0; // Reset for each reduction
            Console.WriteLine($"[TemplateMatcher] Starting reduction for node {node}");
            var result = ReduceDepthFirst(node);
            Console.WriteLine($"[TemplateMatcher] Finished reduction for node {node} after {_totalReductions} steps");
            return result;
        }

        public bool CanGroupComponents(HlslTreeNode a, HlslTreeNode b, bool allowMatrixColumn)
        {
            return _nodeGrouper.CanGroupComponents(a, b, allowMatrixColumn);
        }

        public bool SharesMatrixColumnOrRow(HlslTreeNode x, HlslTreeNode y)
        {
            if (x is RegisterInputNode r1 && y is RegisterInputNode r2)
            {
                return _nodeGrouper.SharesMatrixColumnOrRow(r1, r2);
            }
            return false;
        }

        private static void Replace(HlslTreeNode node, HlslTreeNode with)
        {
            if (node == with)
            {
                return;
            }
            foreach (var input in node.Inputs)
            {
                input.Outputs.Remove(node);
            }
            foreach (var output in node.Outputs)
            {
                for (int i = 0; i < output.Inputs.Count; i++)
                {
                    if (output.Inputs[i] == node)
                    {
                        output.Inputs[i] = with;
                    }
                }
                with.Outputs.Add(output);
            }
        }

        private static bool IsRegister(HlslTreeNode node)
        {
            return node is RegisterInputNode;
        }
    }
}
