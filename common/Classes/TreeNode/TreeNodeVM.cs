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

        //public readonly bool SelectChildrenOnSelected = true;

        public bool? IsSelected { get; set; }
        private void OnIsSelectedChanged(bool? previous, bool? @new) {
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
