using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace TsActivexGen.Util {
    public static class WpfExtensions {
        public static T SelectedItem<T>(this Selector selector) {
            return (T)selector.SelectedItem;
        }
        public static IEnumerable<T> Items<T>(this Selector selector) {
            return selector.ItemsSource.Cast<T>();
        }
    }
}
