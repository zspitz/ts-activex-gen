using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using static System.Windows.DependencyProperty;
using static TsActivexGen.FilterState;
using static System.Windows.Visibility;

namespace TsActivexGen.Wpf {
    public abstract class ReadOnlyConverterBase : IValueConverter {
        public abstract object Convert(object value, Type targetType, object parameter, CultureInfo culture);
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => UnsetValue;
    }


    public class ValueTupleNameConverter : ReadOnlyConverterBase {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var t = value as (String name, Object o)?; ;
            if (t == null) { return UnsetValue; }
            return t.Value.name;
        }
    }

    public class FilterStateConverter: ReadOnlyConverterBase {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var filterState = (FilterState)value;
            if (targetType == typeof(Brush)) {
                return filterState == DescendantMatched ? Brushes.Gray : UnsetValue;
            } else if (targetType == typeof(Visibility)) {
                return filterState.In(Matched, DescendantMatched) ? Visible : Collapsed;
            }
            throw new InvalidOperationException();
        }
    }
}
