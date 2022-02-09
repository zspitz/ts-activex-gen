using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TsActivexGen {
    public abstract class TreeNodeBase<TData> {
        public TData Data { get; set; }

        private IList<TreeNodeBase<TData>> _children;
        public IReadOnlyCollection<TreeNodeBase<TData>> Children { get; }

        protected abstract IList<TreeNodeBase<TData>> InitWith();

        public TreeNodeBase(TData data = default) {
            Data = data;
            _children = InitWith();
            Children = new ReadOnlyCollection<TreeNodeBase<TData>>(_children);
        }

        private TreeNodeBase<TData> _parent;
        public virtual TreeNodeBase<TData> Parent {
            get => _parent;
            set {
                if (_parent == value) { return; }
                if (value.GetType() != GetType()) { throw new InvalidOperationException(); }
                _parent?._children.Remove(this); //do we have to worry that the child might appear multiple times in the parent?
                _parent = value;
                _parent?._children.Add(this);
            }
        }
    }

    public static class TreeNodeExtensions {
        public static TNode Parent<TData, TNode>(this TNode node) where TNode : TreeNodeBase<TData> => (TNode)node.Parent;

        public static TNode AddChild<TData, TNode>(this TNode node, TData data = default) where TNode : TreeNodeBase<TData>, new() =>
            new TNode() { Data = data, Parent = node };

        public static TNode AddSibling<TData, TNode>(this TNode node, TData data = default) where TNode : TreeNodeBase<TData>, new() =>
            node.Parent<TData, TNode>()?.AddChild(data);

        public static IEnumerable<TNode> Parents<TData, TNode>(this TNode node) where TNode : TreeNodeBase<TData> {
            var current = node;
            while (current != null) {
                yield return current;
                current = (TNode)current.Parent;
            }
        }

        public static IEnumerable<TNode> Descendants<TData, TNode>(this TNode node) where TNode : TreeNodeBase<TData> {
            foreach (var child in node.Children.Cast<TNode>()) {
                yield return child;
                foreach (var descendant in child.Descendants<TData, TNode>()) {
                    yield return descendant;
                }
            }
        }
    }
}
