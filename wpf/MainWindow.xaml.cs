using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TsActivexGen.Util;
using Ookii.Dialogs;
using Forms = System.Windows.Forms;
using static System.IO.Path;
using static System.IO.File;
using static System.Windows.MessageBoxButton;
using static System.Windows.MessageBoxResult;
using System.Diagnostics;
using static System.Reflection.Assembly;
using TsActivexGen.ActiveX;
using System.Windows.Data;
using System.Collections.ObjectModel;
using static TsActivexGen.Util.Methods;
using static TsActivexGen.Wpf.Functions;

namespace TsActivexGen.Wpf {
    public partial class MainWindow : Window {
        ObservableCollection<OutputFileDetails> fileList = new ObservableCollection<OutputFileDetails>();

        public MainWindow() {
            InitializeComponent();

            dgTypeLibs.ItemsSource = TypeLibDetails.FromRegistry.Value;

            txbFilter.TextChanged += (s, e) => applyFilter();

            dgTypeLibs.SelectionChanged += (s, e) => {
                //List<int> lst = null;
                //Debug.WriteLine(lst.Count);

                if (e.AddedItems.Count == 0) { return; }
                addFiles();
            };

            var fileDlg = new VistaOpenFileDialog() {
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            };
            btnBrowseTypeLibFile.Click += (s, e) => {
                if (!txbTypeLibFromFile.Text.IsNullOrEmpty()) { fileDlg.FileName = txbTypeLibFromFile.Text; }
                if (fileDlg.ShowDialog() == Forms.DialogResult.Cancel) { return; }
                txbTypeLibFromFile.Text = fileDlg.FileName;
                addFiles();
            };

            txbOutputFolder.Text = Combine(GetDirectoryName(GetEntryAssembly().Location), "typings");
            txbOutputFolder.TextChanged += (s, e) => ((IList<OutputFileDetails>)dtgFiles.ItemsSource).ForEach(x => x.OutputFolder = txbOutputFolder.Text);
            btnBrowseOutputFolder.Click += (s, e) => fillFolder();

            Action onCheckToggled = () => ((IEnumerable<OutputFileDetails>)dtgFiles.ItemsSource).ForEach(x => x.PackageForTypings = cbPackageForTypes.IsChecked.Value);
            cbPackageForTypes.Checked += (s, e) => onCheckToggled();
            cbPackageForTypes.Unchecked += (s, e) => onCheckToggled();

            dtgFiles.ItemsSource = fileList;

            btnOutput.Click += (s, e) => {
                ForceCreateDirectory(txbOutputFolder.Text);
                dtgFiles.Items<OutputFileDetails>().ForEach(x => {
                    if (!x.WriteOutput) { return; }
                    if (x.DeclarationFileName.IsNullOrEmpty()) { return; }
                    if (createFile(x.FullDeclarationPath)) {
                        if (x.PackageForTypings) {
                            var subfolder = Combine(txbOutputFolder.Text, x.DeclarationFileName);

                            //create subdirectory for all files
                            ForceCreateDirectory(subfolder);

                            //create tsconfig.json
                            WriteAllText(Combine(subfolder, "tsconfig.json"), GetTsConfig(x.DeclarationFileName));

                            //create index.d.ts
                            var s1 = GetHeaders(x.Name, x.LibraryUrl, txbAuthorName.Text, txbAuthorURL.Text);
                            s1 += Environment.NewLine;
                            s1 += ReferenceDirectives(x.Output.Dependencies);
                            s1 += x.Output.MainFile;
                            WriteAllText(Combine(subfolder, "index.d.ts"), s1);

                            //create tests file
                            if (!x.Output.TestsFile.IsNullOrEmpty()) {
                                s1 = ReferenceDirectives(new[] { x.Name }) + x.Output.TestsFile;
                                WriteAllText(Combine(subfolder, $"{x.DeclarationFileName}-tests.ts"), s1);
                            }
                        } else {
                            WriteAllText(x.FullDeclarationPath, x.Output.MainFile);
                        }
                    }
                });
                var firstFilePath = dtgFiles.Items<OutputFileDetails>().FirstOrDefault()?.FullDeclarationPath;
                if (firstFilePath.IsNullOrEmpty()) { return; }
                var psi = new ProcessStartInfo("explorer.exe", "/n /e,/select,\"" + firstFilePath + "\"");
                Process.Start(psi);
            };

            btnClearFiles.Click += (s, e) => {
                tlbGenerator = new TlbInf32Generator();
                fileList.Clear();
            };
        }

        private bool createFile(string path) {
            if (!Exists(path)) { return true; }
            return MessageBox.Show($"Overwrite '{path}`?", "", YesNo) == Yes;
        }

        VistaFolderBrowserDialog folderDlg = new VistaFolderBrowserDialog() {
            ShowNewFolderButton = true
        };

        TlbInf32Generator tlbGenerator = new TlbInf32Generator();
        private void addFiles() {
            switch (cmbDefinitionType.SelectedIndex) {
                case 0:
                    var details = dgTypeLibs.SelectedItem<TypeLibDetails>();
                    tlbGenerator.AddFromRegistry(details.TypeLibID, details.MajorVersion, details.MinorVersion, details.LCID);
                    break;
                case 1:
                    tlbGenerator.AddFromFile(txbTypeLibFromFile.Text);
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidOperationException();
            }

            fileList.Clear();
            new TSBuilder().GetTypescript(tlbGenerator.NSSet).SelectKVP((name, x) => new OutputFileDetails {
                Name = name,
                DeclarationFileName = $"activex-{name.ToLower()}.d.ts",
                OutputFolder = txbOutputFolder.Text,
                WriteOutput = true,
                PackageForTypings = cbPackageForTypes.IsChecked.Value,
                Output = x
            }).AddRangeTo(fileList);
        }

        private bool fillFolder() {
            if (!txbOutputFolder.Text.IsNullOrEmpty()) { folderDlg.SelectedPath = txbOutputFolder.Text; }
            var result = folderDlg.ShowDialog();
            if (result == Forms.DialogResult.Cancel) { return false; }
            txbOutputFolder.Text = folderDlg.SelectedPath;
            return true;
        }

        private void applyFilter() {
            CollectionViewSource.GetDefaultView(dgTypeLibs.ItemsSource).Filter = x => (((TypeLibDetails)x).Name ?? "").Contains(txbFilter.Text, StringComparison.OrdinalIgnoreCase);
        }
    }
}