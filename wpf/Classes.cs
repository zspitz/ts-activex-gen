using PropertyChanged;
using System.IO;
using System.Windows.Controls;
using TsActivexGen.Util;
using System.Windows;

namespace TsActivexGen.Wpf {
    [ImplementPropertyChanged]
    public class OutputFileDetails {
        public string Name { get; set; }
        public string Description { get; set; }
        public string DeclarationFileName { get; set; }
        public bool DeclarationExists => File.Exists(FullDeclarationPath);
        public string FullDeclarationPath => Path.Combine(OutputFolder, DeclarationFileName.ForceEndsWith(".d.ts"));
        public string RuntimeFileName { get; set; }
        public bool RuntimeExists => File.Exists(FullRuntimePath);
        public string FullRuntimePath => Path.Combine(OutputFolder, RuntimeFileName.ForceEndsWith(".ts"));
        public string OutputFolder { get; set; }
        
        public bool PackageForTypings { get; set; }
        public bool WriteOutput { get; set; }
        public NamespaceOutput Output { get; set; }
        
        public bool EmitModuleConstants { get; set; }
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
