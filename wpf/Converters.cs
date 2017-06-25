using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace TsActivexGen.Wpf {
    public class ExistsConverter : IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            var x = (OutputFileDetails)values[0];
            var packaged = (bool)values[1];
            if (packaged) { return Directory.Exists(x.PackagedFolderPath); }
            return File.Exists(x.SingleFilePath);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
