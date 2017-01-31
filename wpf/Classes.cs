using PropertyChanged;
using System.IO;
using System.Windows.Controls;
using TsActivexGen.Util;

namespace TsActivexGen.Wpf {
    [ImplementPropertyChanged]
    public class OutputFileDetails {
        public string Name { get; set; }
        public string Description { get; set; }
        public string FileName { get; set; }
        public string OutputFolder { get; set; }
        public bool Exists => File.Exists(FullPath);
        public bool PackageForTypings { get; set; }
        public bool WriteOutput { get; set; }
        public string OutputText { get; set; }
        public string FullPath => Path.Combine(OutputFolder, FileName.ForceEndsWith(".d.ts"));
        public bool EmitModuleConstants { get; set; }
    }

    public class DefinitionTypesComboBox : ComboBox {
        public DefinitionTypesComboBox() {
            ItemsSource = new[] { "Type lib from registry", "Type lib from file", "WMI class" };
            SelectedIndex = 0;
        }
    }
}
