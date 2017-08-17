using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsActivexGen {
    public class SimpleTreeNode<TData> {
        private List<SimpleTreeNode<TData>> _children = new List<SimpleTreeNode<TData>>();
        public readonly ReadOnlyCollection<SimpleTreeNode<TData>> Children;

        private SimpleTreeNode<TData> _parent;
        public SimpleTreeNode<TData> Parent {
            get => _parent;
            set {
                if (_parent == value) { return; }
                _parent?._children.Remove(this);
                _parent = value;
                _parent?._children.Add(this);
            }
        }

        public TData Data { get; set; }

        public SimpleTreeNode(TData data = default(TData)) {
            Data = data;
            Children = new ReadOnlyCollection<SimpleTreeNode<TData>>(_children);
        }

        public SimpleTreeNode<TData> AddChild(TData data = default(TData)) => new SimpleTreeNode<TData>(data) {
            Parent = this
        };
        public SimpleTreeNode<TData> AddSibling(TData data = default(TData)) => Parent?.AddChild(data);

        public IEnumerable<SimpleTreeNode<TData>> Descendants() => _children.SelectMany(child => child.Descendants());
    }
}
