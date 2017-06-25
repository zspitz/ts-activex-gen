using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls.Primitives;

namespace TsActivexGen.Util {
    public static class WpfExtensions {
        public static T SelectedItem<T>(this Selector selector)  => (T)selector.SelectedItem;
        public static IEnumerable<T> Items<T>(this Selector selector) => selector.ItemsSource.Cast<T>();
        public static T SelectedValue<T>(this Selector selector) => (T)selector.SelectedValue;
    }
}
