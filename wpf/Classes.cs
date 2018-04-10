using PropertyChanged;
using System.Windows.Controls;
using System.Windows;
using static System.IO.File;
using static System.IO.Path;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static TsActivexGen.Wpf.Misc;
using static TsActivexGen.Wpf.Functions;
using static System.Environment;

namespace TsActivexGen.Wpf {
    [AddINotifyPropertyChangedInterface]
    public class DiskOutputDetails {
        public readonly string InitialName;
        public string Name { get; set; }
        public string Description { get; set; }
        public int MajorVersion { get; set; }
        public int MinorVersion { get; set; }
        public string LibraryUrl { get; set; }
        public decimal? TypescriptVersion { get; set; }

        public bool IsActiveX { get; set; } = true;
        public string OutputFolderRoot { get; set; }
        public bool IsPackage { get; set; }

        public List<KeyValuePair<string, NamespaceOutput>> NamespaceOutputs { get; } = new List<KeyValuePair<string, NamespaceOutput>>();

        /// <summary>Allows user to control whether to output this specific library</summary>
        public bool WriteOutput { get; set; } = true;

        public bool Exists {
            get {
                if (IsPackage) { return Directory.Exists(PackagePath); }
                return File.Exists(SingleFilePath);
            }
        }

        public NamespaceOutput SingleOutput {
            get => NamespaceOutputs.Values().FirstOrDefault();
            set {
                if (NamespaceOutputs.Any()) { NamespaceOutputs.Clear(); }
                NamespaceOutputs.Add("", value);
            }
        }

        public string FormattedName => $"{(IsActiveX ? "activex-" : "")}{Name.ToLower()}";
        public string SingleFilePath => Combine(OutputFolderRoot, FormattedName + ".d.ts");
        public string PackagePath => Combine(OutputFolderRoot, FormattedName);
        public string TestsFilePath => Combine(PackagePath, $"{FormattedName}-tests.ts");

        public void WritePackageFile(string fileName, string contents, bool overwrite = false) {
            var destinationPath = Combine(PackagePath, fileName);
            if (!overwrite && Exists(destinationPath)) { return; }
            WriteAllText(destinationPath, contents);
        }
        public void WriteTestsFile(string contents, bool overwrite = false) => WritePackageFile(TestsFilePath, contents, overwrite);

        public void WritePackage(string authorName, string authorUrl) {
            //create subdirectory for all files
            Directory.CreateDirectory(PackagePath);

            //create tsconfig.json
            WritePackageFile("tsconfig.json", GetTsConfig(FormattedName), true);

            //create index.d.ts
            var s1 = GetHeaders(Name, Description, LibraryUrl, authorName, authorUrl, MajorVersion, MinorVersion, TypescriptVersion);
            if (NamespaceOutputs.Count ==1) { //TODO if there are multiple namespaces here, then we're assuming all the dependencies are in the same file
                s1 += ReferenceDirectives(SingleOutput.RootNamespace.Dependencies);
                s1 += SingleOutput.MainFile;
            } else {
                s1 += NamespaceOutputs.First().Value.MergedNominalTypes;
                s1 += NamespaceOutputs.JoinedKVP( (name, nsOutput) => nsOutput.LocalTypes, NewLine);
            }

            WritePackageFile("index.d.ts", s1, true);

            //create tests file; prompt if it exists already
            var overwrite = false;
            //if (Exists(TestsFilePath) && MessageBox.Show("Overwrite tests file?", "", YesNo) == Yes) {
            //    overwrite = true;
            //}

            //TODO generate tests file from all namespace outputs
            WriteTestsFile(SingleOutput.TestsFile, overwrite);

            //create tslint.json
            WritePackageFile("tslint.json", @"
{
    ""extends"": ""dtslint/dt.json"",
    ""rules"": {
        ""no-const-enum"": false,
        ""max-line-length"": false
    }
}".TrimStart(), true);

            //create package.json
            WritePackageFile("package.json", @"
{
    ""private"": true,
    ""dependencies"": {
        ""activex-helpers"": ""*""
    }
}".TrimStart(), true);
        }

        public DiskOutputDetails(string initialName = "") {
            InitialName = initialName;
            Name = initialName;
        }

        public static DiskOutputDetails Create(string name, NamespaceOutput nsOutput) {
            var ret = new DiskOutputDetails(name) {
                Description = nsOutput.RootNamespace.Description,
                MajorVersion = nsOutput.RootNamespace.MajorVersion,
                MinorVersion = nsOutput.RootNamespace.MinorVersion
            };
            StoredDetails.IfContainsKey(name.ToLower(), y => {
                ret.LibraryUrl = y.url;
                if (y.major != 0 || y.minor != 0) {
                    ret.MajorVersion = y.major;
                    ret.MinorVersion = y.minor;
                }
            });
            return ret;
        }
    }

    public class DefinitionTypesComboBox : ComboBox {
        public DefinitionTypesComboBox() {
            ItemsSource = new[] { "Type lib from registry", "Type lib from file", "Default library list", "WMI class", "Doxygen IDL XML files", "Selected types" };
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

    public class ImportedDetails {
        public string url { get; set; }
        public int major { get; set; }
        public int minor { get; set; }
    }
}
