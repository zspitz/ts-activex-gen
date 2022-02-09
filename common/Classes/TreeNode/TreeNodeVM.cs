using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using static TsActivexGen.FilterState;
using System.ComponentModel;

namespace TsActivexGen {
    public class TreeNodeVM<TData> : TreeNodeBase<TData>, INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        private ObservableCollection<TreeNodeBase<TData>> children = new ObservableCollection<TreeNodeBase<TData>>();
        protected override IList<TreeNodeBase<TData>> InitWith() => children;

        public ReadOnlyObservableCollection<TreeNodeBase<TData>> ChildrenOC { get; set; }
        public TreeNodeVM() : this(default) { }
        public TreeNodeVM(TData data, FilterState filterState = Matched) : base(data) {
            ChildrenOC = new ReadOnlyObservableCollection<TreeNodeBase<TData>>(children);
            FilterState = filterState;
        }

        //public readonly bool SelectChildrenOnSelected = true;

        public bool? IsSelected { get; set; } = false;
        public void OnIsSelectedChanged(object oPrevious, object oNew) {
            var previous = (bool?)oPrevious;
            var @new = (bool?)oNew;
            if (@new.HasValue) {
                children.Cast<TreeNodeVM<TData>>().ForEach(x => x.IsSelected = @new);
            }

            if (Parent != null) {
                var parent = ((TreeNodeVM<TData>)Parent);
                if (@new == null) {
                    parent.IsSelected = null;
                } else if (parent.children.Cast<TreeNodeVM<TData>>().Any(x => x.IsSelected != @new)) {
                    parent.IsSelected = null;
                } else {
                    parent.IsSelected = @new;
                }
            }
        }

        // FilterState has to be implemented by hand (and not automatically by Fody) because we have to be able to cancel the setter
        private FilterState? _filterState;
        public FilterState? FilterState {
            get => _filterState;
            private set {
                if (value == null && _filterState != null) {
                    _filterState = null;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FilterState"));
                    children.Cast<TreeNodeVM<TData>>().ForEach(x => x.FilterState = null);
                    return;
                }

                if (value == NotMatched && children.Cast<TreeNodeVM<TData>>().Any(x => x.FilterState != NotMatched)) {
                    value = DescendantMatched;
                }
                if (value == _filterState) { return; }
                _filterState = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FilterState"));

                var parent = Parent as TreeNodeVM<TData>;
                switch (parent?.FilterState) {
                    case null:
                    case Matched: // nothing in the current node can affect the parent
                        break;
                    case DescendantMatched when value == NotMatched: // the only possible new value when the parent can be affected
                        parent.FilterState = NotMatched; // if there are other Matched or DescendentMatched children, it will be handled by the parent node's setter
                        break;
                    case NotMatched when value.In(Matched, DescendantMatched):
                        parent.FilterState = DescendantMatched;
                        break;
                }
            }
        }

        //TODO handle child assigned to parent with incompatible filter states (parent==NotMatched and child == something else)

        public void ApplyFilter(Predicate<TData> predicate) {
            var matched = predicate(Data);
            if (matched) {
                FilterState = Matched;
            } else {
                FilterState = NotMatched;
            }

            children.Cast<TreeNodeVM<TData>>().ForEach(x => x.ApplyFilter(predicate));
        }
        public void ResetFilter() => FilterState = null;
    }
}
