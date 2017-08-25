using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TsActivexGen.Util;
using static System.IO.Path;
using static System.IO.File;
using static System.Windows.MessageBoxButton;
using static System.Windows.MessageBoxResult;
using System.Diagnostics;
using static System.Reflection.Assembly;
using System.Windows.Data;
using System.Collections.ObjectModel;
using static TsActivexGen.Wpf.Functions;
using System.IO;
using static TsActivexGen.Functions;
using static TsActivexGen.Wpf.Misc;
using static System.Environment;
using TsActivexGen.tlibuilder;
using TsActivexGen.idlbuilder;
using static TsActivexGen.idlbuilder.Context;
using System.Windows.Controls;

namespace TsActivexGen.Wpf {
    public partial class MainWindow : Window {
        ObservableCollection<OutputFileDetails> fileList = new ObservableCollection<OutputFileDetails>();
        TextBox txbFileBaseName;
        TextBox txbAuthorName;
        TextBox txbAuthorURL;

        public MainWindow() {
            InitializeComponent();

            dgTypeLibs.ItemsSource = TypeLibDetails.FromRegistry.Value;

            txbFilter.TextChanged += (s, e) => applyFilter();

            dgTypeLibs.SelectionChanged += (s, e) => {
                if (e.AddedItems.Count == 0) { return; }
                addFiles();
            };

            brTypeLibFile.SelectionChanged += (s, e) => addFiles();

            brOutputFolder.Path= Combine(GetDirectoryName(GetEntryAssembly().Location), "typings");
            brOutputFolder.SelectionChanged += (s, e) => ((IList<OutputFileDetails>)dtgFiles.ItemsSource).ForEach(x => x.OutputFolderRoot = brOutputFolder.Path);

            btnLoadDefaultLibs.Click += (s, e) => addFiles();

            brDoxygenXMLFolder.Path = new[] { "../../../idlbuilder/output/xml", "output/xml" }.Select(x => GetFullPath(Combine(GetDirectoryName(GetEntryAssembly().Location), x))).FirstOrDefault(x => Directory.Exists(x));
            brDoxygenXMLFolder.SelectionChanged += (s, e) => addFiles();

            dtgFiles.ItemsSource = fileList;

            btnOutput.Click += (s, e) => {
                var toOutput = dtgFiles.Items<OutputFileDetails>().Where(x => x.WriteOutput && !x.Name.IsNullOrEmpty()).ToList();
                if (toOutput.None()) { return; }

                Directory.CreateDirectory(brOutputFolder.Path);

                string selectedPath = "";

                if (lbFilePerNamespace.SelectedValue<bool>()) {
                    if (lbPackageForDefinitelyTyped.SelectedValue<bool>()) {
                        //package for DefinitelyTyped

                        //prompt about missing common details
                        var missingCommon = new(string description, string value)[] { ("author name", txbAuthorName.Text), ("author URL", txbAuthorURL.Text) }.Where(x => x.value.IsNullOrEmpty());
                        var errors = new List<(string description, string library)>();
                        foreach (var x in toOutput) {
                            if (x.MajorVersion == 0 && x.MinorVersion == 0) { errors.Add("version", x.Name); }
                            if (x.LibraryUrl.IsNullOrEmpty()) { errors.Add("library url", x.Name); }
                        }
                        var missingDetails = errors.GroupBy(x => x.description, (description, x) => (description: description, libs: x.Joined(", ", y => y.library))).ToList();

                        var msg = "";
                        if (missingCommon.Any()) {
                            msg += "The following shared details are missing:" + NewLines(2) + missingCommon.Joined(NewLine, x => $" - {x.description}") + NewLines(2);
                        }
                        if (missingDetails.Any()) {
                            msg += "The following details are missing from individual lbraries:" + NewLines(2) + missingDetails.Joined(NewLine, x => $" - {x.description} ({x.libs})") + NewLines(2);
                        }
                        if (!msg.IsNullOrEmpty()) {
                            if (MessageBox.Show(msg + "Continue anyway?", "Missing details", YesNo) == No) { return; }
                        }

                        //begin output
                        toOutput.ForEach(x => {
                            //create subdirectory for all files
                            Directory.CreateDirectory(x.PackagedFolderPath);

                            //create tsconfig.json
                            x.WritePackageFile("tsconfig.json", GetTsConfig(x.FormattedName), true);

                            //create index.d.ts
                            var s1 = GetHeaders(x.Name, x.Description, x.LibraryUrl, txbAuthorName.Text, txbAuthorURL.Text, x.MajorVersion, x.MinorVersion);
                            s1 += ReferenceDirectives(x.Output.Dependencies);
                            s1 += x.Output.MainFile;
                            WriteAllText(x.PackagedFilePath, s1);

                            //create tests file; prompt if it exists already
                            var overwrite = false;
                            if (Exists(x.TestsFilePath) && MessageBox.Show("Overwrite tests file?", "", YesNo) == Yes) {
                                overwrite = true;
                            }
                            x.WriteTestsFile(x.Output.TestsFile, overwrite);

                            //create tslint.json
                            x.WritePackageFile("tslint.json", @"
{
    ""extends"": ""dtslint/dt.json"",
    ""rules"": {
        ""interface-name"": [false]
    }
}".TrimStart(), true);

                            //create package.json
                            x.WritePackageFile("package.json", @"{ ""dependencies"": { ""activex-helpers"": ""*""}}", true);
                        });
                        selectedPath = toOutput.First().PackagedFolderPath;
                    } else {
                        toOutput.ForEach(x => WriteAllText(x.SingleFilePath, x.Output.MainFile));
                        selectedPath = toOutput.First().SingleFilePath;
                    }
                } else {
                    throw new NotImplementedException();
                }

//                switch (lbPackaging.SelectedValue<PackagingMethod>()) {
//                    case PackagePerNamspace:
//                        //package for DefinitelyTyped

//                        //prompt about missing common details
//                        var missingCommon = new(string description, string value)[] { ("author name", txbAuthorName.Text), ("author URL", txbAuthorURL.Text) }.Where(x => x.value.IsNullOrEmpty());
//                        var errors = new List<(string description, string library)>();
//                        foreach (var x in toOutput) {
//                            if (x.MajorVersion == 0 && x.MinorVersion == 0) { errors.Add("version", x.Name); }
//                            if (x.LibraryUrl.IsNullOrEmpty()) { errors.Add("library url", x.Name); }
//                        }
//                        var missingDetails = errors.GroupBy(x => x.description, (description, x) => (description: description, libs: x.Joined(", ", y => y.library))).ToList();

//                        var msg = "";
//                        if (missingCommon.Any()) {
//                            msg += "The following shared details are missing:" + NewLines(2) + missingCommon.Joined(NewLine, x => $" - {x.description}") + NewLines(2);
//                        }
//                        if (missingDetails.Any()) {
//                            msg += "The following details are missing from individual lbraries:" + NewLines(2) + missingDetails.Joined(NewLine, x => $" - {x.description} ({x.libs})") + NewLines(2);
//                        }
//                        if (!msg.IsNullOrEmpty()) {
//                            if (MessageBox.Show(msg + "Continue anyway?", "Missing details", YesNo) == No) { return; }
//                        }

//                        //begin output
//                        toOutput.ForEach(x => {
//                            //create subdirectory for all files
//                            Directory.CreateDirectory(x.PackagedFolderPath);

//                            //create tsconfig.json
//                            x.WritePackageFile("tsconfig.json", GetTsConfig(x.FormattedName), true);

//                            //create index.d.ts
//                            var s1 = GetHeaders(x.Name, x.Description, x.LibraryUrl, txbAuthorName.Text, txbAuthorURL.Text, x.MajorVersion, x.MinorVersion);
//                            s1 += ReferenceDirectives(x.Output.Dependencies);
//                            s1 += x.Output.MainFile;
//                            WriteAllText(x.PackagedFilePath, s1);

//                            //create tests file; prompt if it exists already
//                            var overwrite = false;
//                            if (Exists(x.TestsFilePath) && MessageBox.Show("Overwrite tests file?", "", YesNo) == Yes) {
//                                overwrite = true;
//                            }
//                            x.WriteTestsFile(x.Output.TestsFile, overwrite);

//                            //create tslint.json
//                            x.WritePackageFile("tslint.json", @"
//{
//    ""extends"": ""dtslint/dt.json"",
//    ""rules"": {
//        ""interface-name"": [false]
//    }
//}".TrimStart(), true);

//                            //create package.json
//                            x.WritePackageFile("package.json", @"{ ""dependencies"": { ""activex-helpers"": ""*""}}", true);
//                        });
//                        selectedPath = toOutput.First().PackagedFolderPath;
//                        break;

//                    case FilePerNamespace:
//                        toOutput.ForEach(x => WriteAllText(x.SingleFilePath, x.Output.MainFile));
//                        selectedPath = toOutput.First().SingleFilePath;
//                        break;

//                    case SingleFile:

//                        break;
//                }

                var psi = new ProcessStartInfo("explorer.exe", $"/n /e,/select,\"{selectedPath}\"");
                Process.Start(psi);
            };

            btnClearFiles.Click += (s, e) => {
                tlbGenerator = new TlbInf32Generator();
                fileList.Clear();
            };

            void RunTests(IEnumerable<string> jsonPaths) =>
                jsonPaths.ForEach(x => {
                    RunCommandlineAsync($"tsc -p {x} && tslint -p {x}");
                });

            btnTest.Click += (s, e) => RunTests(Directory.EnumerateFiles(brOutputFolder.Path, "tsconfig.json", SearchOption.AllDirectories));
            btnTestListed.Click += (s, e) => RunTests(fileList.Select(x => Combine(x.PackagedFolderPath, "tsconfig.json")));
        }

        private bool createFile(string path) {
            if (!Exists(path)) { return true; }
            return MessageBox.Show($"Overwrite '{path}`?", "", YesNo) == Yes;
        }

        TlbInf32Generator tlbGenerator = new TlbInf32Generator();
        private void addFiles() {
            TSNamespaceSet nsset;
            var selected = cmbDefinitionType.SelectedIndex;
            if (selected == 4) {
                var idlBuilder = new DoxygenIDLBuilder(brDoxygenXMLFolder.Path, Automation);
                nsset = idlBuilder.Generate();
            } else {
                switch (selected) {
                    case 0:
                        var details = dgTypeLibs.SelectedItem<TypeLibDetails>();
                        tlbGenerator.AddFromRegistry(details.TypeLibID, details.MajorVersion, details.MinorVersion, details.LCID);
                        break;
                    case 1:
                        if (brOutputFolder.Path.IsNullOrEmpty()) { return; }
                        tlbGenerator.AddFromFile(brOutputFolder.Path);
                        break;
                    case 2:
                        tlbGenerator.AddFromKeywords(new[] {
                        "ole automation", "scripting runtime", "wmi scripting",
                        "activex data objects 6.1", "access database engine", "microsoft xml, v6.0",
                        "microsoft access 14.0 object library", "microsoft excel", "microsoft word", "microsoft powerpoint", "microsoft outlook 14.0 object library", "infopath 3.0 type library",
                        "fax service extended com library", "internet controls", "shell controls and automation", "speech", "acquisition" });
                        break;
                    default:
                        throw new InvalidOperationException();
                }
                nsset = tlbGenerator.NSSet;
            }

            var old = fileList.Select(x => KVP(x.InitialName, x)).ToDictionary();
            fileList.Clear();
            new TSBuilder().GetTypescript(nsset).SelectKVP((name, x) => {
                if (!old.TryGetValue(name, out var ret)) {
                    ret = new OutputFileDetails(name) {
                        Description = x.Description,
                        MajorVersion = x.MajorVersion,
                        MinorVersion = x.MinorVersion
                    };
                    StoredDetails.IfContainsKey(name.ToLower(), y => {
                        ret.LibraryUrl = y.url;
                        if (y.major != 0 || y.minor != 0) {
                            ret.MajorVersion = y.major;
                            ret.MinorVersion = y.minor;
                        }
                    });
                }
                ret.Output = x;
                ret.OutputFolderRoot = brOutputFolder.Path;
                return ret;
            }).AddRangeTo(fileList);
        }

        //private bool fillFolder() {
        //    if (!txbOutputFolder.Text.IsNullOrEmpty()) { folderDlg.SelectedPath = txbOutputFolder.Text; }
        //    var result = folderDlg.ShowDialog();
        //    if (result == Forms.DialogResult.Cancel) { return false; }
        //    txbOutputFolder.Text = folderDlg.SelectedPath;
        //    return true;
        //}

        private void applyFilter() {
            CollectionViewSource.GetDefaultView(dgTypeLibs.ItemsSource).Filter = x => (((TypeLibDetails)x).Name ?? "").Contains(txbFilter.Text, StringComparison.OrdinalIgnoreCase);
        }

        private void TextBox_Loaded(object sender, RoutedEventArgs e) {
            //we need this because these controls are all within a RadioButtonListBox, and name cannot be applied in XAML
            //As long as we don't need any binding, this is good enough
            var tb = (TextBox)sender;
            switch (tb.Tag) {
                case "txbFileBaseName":
                    txbFileBaseName = tb;
                    break;
                case "txbAuthorName":
                    txbAuthorName = tb;
                    break;
                case "txbAuthorURL":
                    txbAuthorURL = tb;
                    break;
            }
        }
    }
}