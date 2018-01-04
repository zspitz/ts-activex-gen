using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TsActivexGen.idlbuilder;
using TsActivexGen.tlibuilder;
using TsActivexGen.Util;
using static System.Environment;
using static System.IO.File;
using static System.IO.Path;
using static System.Reflection.Assembly;
using static System.Windows.MessageBoxButton;
using static System.Windows.MessageBoxResult;
using static TsActivexGen.Functions;
using static TsActivexGen.idlbuilder.Context;
using static TsActivexGen.Wpf.Misc;
using static System.StringComparison;

namespace TsActivexGen.Wpf {
    public partial class MainWindow : Window {
        ObservableCollection<DiskOutputDetails> fileList = new ObservableCollection<DiskOutputDetails>();
        TextBox txbFileBaseName;
        TextBox txbAuthorName;
        TextBox txbAuthorURL;

        public MainWindow() {
            InitializeComponent();

            btnGenerate.Click += (s, e) => generateNSSet();

            //we're setting this in code because it's easier to do so than in XAML
            txbKeywords.Text = @"ole automation
scripting runtime
wmi scripting
activex data objects 6.1
access database engine
microsoft xml, v6.0
microsoft access 14.0 object library
microsoft excel
microsoft word
microsoft powerpoint
microsoft outlook 14.0 object library
infopath 3.0 type library
fax service extended com library
internet controls
shell controls and automation
speech
acquisition
microsoft forms
disk quota";

            brOutputFolder.Path = Combine(GetDirectoryName(GetEntryAssembly().Location), "typings");
            brOutputFolder.SelectionChanged += updateOptions;

            brDoxygenXMLFolder.Path = new[] { "../../../idlbuilder/output/xml", "output/xml" }.Select(x => GetFullPath(Combine(GetDirectoryName(GetEntryAssembly().Location), x))).FirstOrDefault(x => Directory.Exists(x));

            var treeviewSource = new ObservableCollection<TreeNodeVM<(string name, object o)>>();
            dgTypeLibs1.SelectionDataChanged += (s, e) => {
                e.AddedItems.Cast<TypeLibDetails>().ForEach(x => {
                    var tli = x.GetTypeLibInfo();
                    var name = tli.Name;
                    if (treeviewSource.Any(y => y.Data.name == name)) { return; }

                    var root = CreateTreeNode((tli.Name, (object)tli));

                    var generators = new Dictionary<string, IEnumerable<(string name, object o)>> {
                        {"Constants", tli.Constants.Cast().Select(y=>(y.Name,(object)y)) },
                        {"CoClasses", tli.CoClasses.Cast().Select(y=>(y.Name,(object)y)) },
                        {"Declarations", tli.Declarations.Cast().Select(y=>(y.Name,(object)y)) },
                        {"Interfaces", tli.Interfaces.Cast().Select(y=>(y.Name,(object)y)) },
                        {"IntrinsicAliases", tli.IntrinsicAliases.Cast().Select(y=>(y.Name,(object)y)) },
                        {"Records", tli.Records.Cast().Select(y=>(y.Name,(object)y)) },
                        {"Unions", tli.Unions.Cast().Select(y=>(y.Name,(object)y)) },
                    };

                    foreach (var (collectionName, generator) in generators) {
                        var items = generator.ToList();
                        if (items.None()) { continue; }
                        var collection = root.AddChild<(string Name, object obj), TreeNodeVM<(string Name, object obj)>>((collectionName, null));
                        items.OrderBy(y => y.name).ForEach(y => collection.AddChild(y));
                    }

                    treeviewSource.Add(root);
                });
                e.RemovedItems.Cast<TypeLibDetails>().ForEach(x => {
                    treeviewSource.Remove(treeviewSource.FirstOrDefault(y => y.Data.name == x.Name));
                });
            };
            tvwSelectedTypes.ItemsSource = treeviewSource;

            btnSelectTypeFromFile.Click += (s, e) => {
                throw new NotImplementedException();
            };

            treeviewFilter.TextChanged += (s, e) => {
                var text = treeviewFilter.Text;
                Action<TreeNodeVM<(string name, object o)>> action;
                if (text.IsNullOrEmpty()) {
                    action = x => x.ResetFilter();
                } else {
                    action = x => x.ApplyFilter(data => data.name.Contains(text, InvariantCultureIgnoreCase));
                }
                treeviewSource.ForEach(action);
            };

            dtgFiles.ItemsSource = fileList;

            lbFilePerNamespace.SelectionChanged += (sender, e) => {
                if ((sender as ListBox).SelectedValue<bool>()) {
                    var current = fileList.FirstOrDefault();
                    fileList.Clear();
                    current?.NamespaceOutputs.SelectKVP((name, nsOutput) => {
                        var ret = DiskOutputDetails.Create(name, nsOutput);
                        ret.IsActiveX = current.IsActiveX;
                        ret.OutputFolderRoot = brOutputFolder.Path;
                        ret.IsPackage = lbPackageForDefinitelyTyped.SelectedValue<bool>();
                        return ret;
                    }).AddRangeTo(fileList);
                } else {
                    var current = fileList.Where(x => x != null).ToList();
                    fileList.Clear();
                    if (current.Any()) {
                        var first = current.First();
                        var ret = DiskOutputDetails.Create(first.Name, first.SingleOutput);
                        ret.IsActiveX = first.IsActiveX;
                        ret.OutputFolderRoot = first.OutputFolderRoot;
                        ret.IsPackage = lbPackageForDefinitelyTyped.SelectedValue<bool>();
                        current.Select(x => KVP(x.Name, x.SingleOutput)).AddRangeTo(ret.NamespaceOutputs);
                        fileList.Add(ret);
                    }
                }
            };

            lbPackageForDefinitelyTyped.SelectionChanged += updateOptions;

            btnOutput.Click += (s, e) => {
                var toOutput = dtgFiles.Items<DiskOutputDetails>().Where(x => x.WriteOutput && !x.Name.IsNullOrEmpty()).ToList();
                if (toOutput.None()) { return; }

                Directory.CreateDirectory(brOutputFolder.Path);

                string selectedPath = "";

                if (lbPackageForDefinitelyTyped.SelectedValue<bool>()) {
                    if (!writePackages(ref selectedPath)) { return; }
                } else if (lbFilePerNamespace.SelectedValue<bool>()) {
                    toOutput.ForEach(x => WriteAllText(x.SingleFilePath, x.SingleOutput.MainFile));
                    selectedPath = toOutput.First().SingleFilePath;
                } else {
                    var first = toOutput.First();
                    var ts = first.NamespaceOutputs.First().Value.MergedNominalTypes + NewLine + first.NamespaceOutputs.JoinedKVP((name, nsOutput) => nsOutput.LocalTypes, NewLine);
                    WriteAllText(first.SingleFilePath, ts);
                    selectedPath = first.SingleFilePath;
                }

                var psi = new ProcessStartInfo("explorer.exe", $"/n /e,/select,\"{selectedPath}\"");
                Process.Start(psi);
            };

            btnClearFiles.Click += (s, e) => {
                tlbGenerator = new TlbInf32Generator();
                fileList.Clear();
            };

            void RunTests(IEnumerable<string> jsonPaths) =>
                jsonPaths.ForEach(x => {
                    RunCommandlineAsync($"tsc -p {x} && tslint --project {x} --type-check");
                });

            btnTest.Click += (s, e) => RunTests(Directory.EnumerateFiles(brOutputFolder.Path, "tsconfig.json", SearchOption.AllDirectories));
            btnTestListed.Click += (s, e) => RunTests(fileList.Select(x => Combine(x.PackagePath, "tsconfig.json")));
        }

        private void updateOptions(object s, EventArgs e) => dtgFiles.Items<DiskOutputDetails>().ForEach(x => {
            x.OutputFolderRoot = brOutputFolder.Path;
            x.IsPackage = lbPackageForDefinitelyTyped.SelectedValue<bool>();
        });

        private bool createFile(string path) {
            if (!Exists(path)) { return true; }
            return MessageBox.Show($"Overwrite '{path}`?", "", YesNo) == Yes;
        }

        private bool writePackages(ref string selectedPath) {
            var toOutput = dtgFiles.Items<DiskOutputDetails>().Where(x => x.WriteOutput && !x.Name.IsNullOrEmpty()).ToList();

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
                if (MessageBox.Show(msg + "Continue anyway?", "Missing details", YesNo) == No) { return false; }
            }

            //begin output
            toOutput.ForEach(x => x.WritePackage(txbAuthorName.Text, txbAuthorURL.Text));
            selectedPath = toOutput.First().PackagePath;
            return true;
        }

        TlbInf32Generator tlbGenerator = new TlbInf32Generator();
        private void generateNSSet() {
            TSNamespaceSet nsset;
            var selected = cmbDefinitionType.SelectedIndex;
            if (selected == 4) {
                if (brDoxygenXMLFolder.Path.IsNullOrEmpty()) { return; }
                var idlBuilder = new DoxygenIDLBuilder(brDoxygenXMLFolder.Path, Automation);
                nsset = idlBuilder.Generate();
            } else {
                switch (selected) {
                    case 0:
                        var details = dgTypeLibs.Items;
                        if (details.None()) { return; }
                        details.ForEach(x => tlbGenerator.AddFromRegistry(x.TypeLibID, x.MajorVersion, x.MinorVersion, x.LCID));
                        break;
                    case 1:
                        if (brOutputFolder.Path.IsNullOrEmpty()) { return; }
                        tlbGenerator.AddFromFile(brTypeLibFile.Path);
                        break;
                    case 2:
                        if (txbKeywords.Text.IsNullOrEmpty()) { return; }
                        tlbGenerator.AddFromKeywords(txbKeywords.Text.Split('\n').Select(x => x.Trim()));
                        break;
                    case 3:

                        break;
                    default:
                        throw new InvalidOperationException();
                }
                nsset = tlbGenerator.NSSet;
            }

            var outputs = new TSBuilder().GetTypescript(nsset);
            if (lbFilePerNamespace.SelectedValue<bool>()) {
                var old = fileList.Select(x => KVP(x.InitialName, x)).ToDictionary();
                fileList.Clear();

                outputs.SelectKVP((name, x) => {
                    if (!old.TryGetValue(name, out var ret)) {
                        ret = DiskOutputDetails.Create(name, x);
                    }
                    ret.SingleOutput = x;
                    ret.OutputFolderRoot = brOutputFolder.Path;
                    ret.IsPackage = lbPackageForDefinitelyTyped.SelectedValue<bool>();
                    return ret;
                }).AddRangeTo(fileList);
            } else {
                DiskOutputDetails details = fileList.FirstOrDefault();
                fileList.Clear();
                if (details == null) {
                    var (firstName, firstOutput) = outputs.First();
                    details = DiskOutputDetails.Create(firstName, firstOutput);
                }
                details.NamespaceOutputs.Clear();
                outputs.AddRangeTo(details.NamespaceOutputs);
                details.OutputFolderRoot = brOutputFolder.Path;
                details.IsPackage = lbPackageForDefinitelyTyped.SelectedValue<bool>();
                fileList.Add(details);
            }
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