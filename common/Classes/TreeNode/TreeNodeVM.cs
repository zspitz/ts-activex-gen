using System.Collections.Generic;
using System.Collections.ObjectModel;
using PropertyChanged;
using System.Linq;
using System;
using static TsActivexGen.FilterState;

namespace TsActivexGen {
    [AddINotifyPropertyChangedInterface]
    public class TreeNodeVM<TData> : TreeNodeBase<TData> {
        private ObservableCollection<TreeNodeBase<TData>> children = new ObservableCollection<TreeNodeBase<TData>>();
        protected override IList<TreeNodeBase<TData>> InitWith() => children;

        public ReadOnlyObservableCollection<TreeNodeBase<TData>> ChildrenOC { get; set; }
        public TreeNodeVM(): this(default) {}
        public TreeNodeVM(TData data): base(data) => ChildrenOC = new ReadOnlyObservableCollection<TreeNodeBase<TData>>(children);

        //public readonly bool SelectChildrenOnSelected = true;

        public bool? IsSelected { get; set; } = false;
        public void OnIsSelectedChanged(object oPrevious, object oNew) {
            var previous = (bool?)oPrevious;
            var @new = (bool?)oNew;
            if (@new.HasValue) {
                children.Cast<TreeNodeVM<TData>>().ForEach(x=> x.IsSelected = @new);
            }

            if (Parent != null) {
                var parent = ((TreeNodeVM<TData>)Parent);
                if (@new== null) {
                    parent.IsSelected = null;
                } else if (parent.children.Cast<TreeNodeVM<TData>>().Any(x => x.IsSelected != @new)) {
                    parent.IsSelected = null;
                } else {
                    parent.IsSelected = @new;
                }
            }
        }

        public FilterState FilterState { get; set; }

        public void ApplyFilter(Predicate<TData> predicate) {
            var matched = predicate(Data);
            if (matched) {
                FilterState = Match;
            } else {
                FilterState = NotMatched;
            }

            children.Cast<TreeNodeVM<TData>>().ForEach(x => x.ApplyFilter(predicate));
        }
    }
}
