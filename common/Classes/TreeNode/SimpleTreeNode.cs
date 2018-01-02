using System.Collections.Generic;

namespace TsActivexGen {
    public class SimpleTreeNode<TData> : TreeNodeBase<TData> {
        private List<TreeNodeBase<TData>> children = new List<TreeNodeBase<TData>>();

        protected override IList<TreeNodeBase<TData>> InitWith() => children;
    }
}