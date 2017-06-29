using PropertyChanged;
using System.Windows.Controls;
using System.Windows;
using static System.IO.File;
using static System.IO.Path;

namespace TsActivexGen.Wpf {
    [ImplementPropertyChanged]
    public class OutputFileDetails {
        public readonly string InitialName;
        public string Name { get; set; }
        public string Description { get; set; }
        public int MajorVersion { get; set; }
        public int MinorVersion { get; set; }
        public string OutputFolderRoot { get; set; }

        public string FormattedName => $"activex-{Name.ToLower()}";

        public string SingleFilePath  => Combine(OutputFolderRoot, FormattedName + ".d.ts");
        public string PackagedFolderPath => Combine(OutputFolderRoot, FormattedName);
        public string PackagedFilePath  => Combine(OutputFolderRoot, FormattedName, "index.d.ts");

        public string LibraryUrl { get; set; }

        /// <summary>Allows user to control whether to output this specific library</summary>
        public bool WriteOutput { get; set; }

        public NamespaceOutput Output { get; set; }

        public void WritePackageFile(string fileName, string contents) => WriteAllText(Combine(PackagedFolderPath, fileName), contents);
        public void WriteTestsFile(string contents) => WritePackageFile(Combine(PackagedFolderPath, $"{FormattedName}-tests.ts"), contents);

        public OutputFileDetails(string initialName) {
            InitialName = initialName;
            Name = initialName;
            WriteOutput = true;
        }
    }

    public class DefinitionTypesComboBox : ComboBox {
        public DefinitionTypesComboBox() {
            ItemsSource = new[] { "Type lib from registry", "Type lib from file", "WMI class" };
            SelectedIndex = 0;
        }
    }

    public class DataGridTextColumnExt : DataGridTextColumn {
        public TextTrimming? TextTrimming { get; set; }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem) {
            var tb = (TextBlock)base.GenerateElement(cell, dataItem);
            if (TextTrimming != null) {
                if (MaxWidth == 0) { MaxWidth = 250; }
                tb.MaxWidth = MaxWidth;
                tb.TextTrimming = TextTrimming.Value;
                var bnd = tb.GetBindingExpression(TextBlock.TextProperty).ParentBinding;
                tb.SetBinding(FrameworkElement.ToolTipProperty, bnd.Path.Path);
            }
            return tb;
        }
    }
}
