using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TsActivexGen.Util;
using Ookii.Dialogs;
using Forms = System.Windows.Forms;
using System.IO;
using static System.IO.Path;
using static System.IO.File;
using static System.Windows.MessageBoxButton;
using static System.Windows.MessageBoxResult;
using System.Diagnostics;
using static System.Reflection.Assembly;
using static System.Windows.Input.Key;
using static Microsoft.VisualBasic.Interaction;
using TsActivexGen.ActiveX;
using System.Windows.Data;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace TsActivexGen.Wpf {
    public partial class MainWindow : Window {
        ObservableCollection<OutputFileDetails> fileList = new ObservableCollection<OutputFileDetails>();
        public MainWindow() {
            InitializeComponent();

            dgTypeLibs.ItemsSource = TypeLibDetails.FromRegistry.Value;

            txbFilter.TextChanged += (s, e) => ApplyFilter();

            dgTypeLibs.SelectionChanged += (s, e) => {
                if (e.AddedItems.Count == 0) { return; }
                addFiles();
            };

            var fileDlg = new VistaOpenFileDialog();
            fileDlg.CheckFileExists = true;
            fileDlg.CheckPathExists = true;
            fileDlg.Multiselect = false;
            btnBrowseTypeLibFile.Click += (s, e) => {
                if (!txbTypeLibFromFile.Text.IsNullOrEmpty()) { fileDlg.FileName = txbTypeLibFromFile.Text; }
                if (fileDlg.ShowDialog() == Forms.DialogResult.Cancel) { return; }
                txbTypeLibFromFile.Text = fileDlg.FileName;
                addFiles();
            };

            txbOutputFolder.Text = Combine(GetDirectoryName(GetEntryAssembly().Location), "typings");
            txbOutputFolder.TextChanged += (s, e) => ((List<OutputFileDetails>)dtgFiles.ItemsSource).ForEach(x => x.OutputFolder = txbOutputFolder.Text);
            btnBrowseOutputFolder.Click += (s, e) => fillFolder();

            Action onCheckToggled = () => ((IEnumerable<OutputFileDetails>)dtgFiles.ItemsSource).ForEach(x => x.PackageForTypings = cbPackageForTypes.IsChecked.Value);
            cbPackageForTypes.Checked += (s, e) => onCheckToggled();
            cbPackageForTypes.Unchecked += (s, e) => onCheckToggled();

            dtgFiles.ItemsSource = fileList;

            btnOutput.Click += (s, e) => {
                if (!Directory.Exists(txbOutputFolder.Text)) {
                    Directory.CreateDirectory(txbOutputFolder.Text);
                }
                dtgFiles.Items<OutputFileDetails>().ForEach(x => {
                    if (!x.WriteOutput) { return; }
                    if (x.FileName.IsNullOrEmpty()) { return; }
                    if (createFile(x.FullPath)) {
                        if (x.PackageForTypings) {
                            throw new NotImplementedException(); //TODO
                        } else {
                            WriteAllText(x.FullPath, x.OutputText);
                        }
                    }
                });
                var firstFilePath = dtgFiles.Items<OutputFileDetails>().FirstOrDefault()?.FullPath;
                if (firstFilePath.IsNullOrEmpty()) { return; }
                var psi = new ProcessStartInfo("explorer.exe", "/n /e,/select,\"" + firstFilePath + "\"");
                Process.Start(psi);
            };

            btnClearFiles.Click += (s, e) => fileList.Clear();

            //btnOutput.Click += (s, e) => {
            //    if (txbFilename.Text.IsNullOrEmpty()) { return; }
            //    if (txbOutput.Text.IsNullOrEmpty()) {
            //        var filled = fillFolder();
            //        if (!filled) { return; }
            //    }
            //    if (!Directory.Exists(txbOutput.Text)) {
            //        Directory.CreateDirectory(txbOutput.Text);
            //    }

            //    currentTypescript.ForEachKVP((name, ts, index) => {
            //        var basePath = index == 0 ? txbFilename.Text : InputBox(name, "Enter path for file");
            //        if (basePath == "") { return; }
            //        var filepath = Combine(txbOutput.Text, $"{basePath}.d.ts");
            //        WriteAllText(filepath, ts);
            //        if (chkOutputTests.IsChecked == true) {
            //            var testsFilePath = Combine(txbOutput.Text, $"{basePath}-tests.ts");
            //            if (!Exists(testsFilePath) || MessageBox.Show("Overwrite existing test file?", "", YesNo) == Yes) {
            //                WriteAllLines(testsFilePath, new[] {
            //                        $"/// <reference path=\"{filepath}\" />",""
            //                    });
            //            }
            //        }
            //    });

            //    var firstFilePath = Combine(txbOutput.Text, $"{txbFilename.Text}.d.ts");
            //    var psi = new ProcessStartInfo("explorer.exe", "/n /e,/select,\"" + firstFilePath + "\"");
            //    Process.Start(psi);
            //};

        }

        private bool createFile(string path) {
            if (!Exists(path)) { return true; }
            return MessageBox.Show($"Overwrite '{path}`?", "", YesNo) == Yes;
        }

        VistaFolderBrowserDialog folderDlg = new VistaFolderBrowserDialog() {
            ShowNewFolderButton = true
        };

        TlbInf32Generator tlbGenerator = new TlbInf32Generator();
        private async void addFiles() {
            switch (cmbDefinitionType.SelectedIndex) {
                case 0:
                    var details = dgTypeLibs.SelectedItem<TypeLibDetails>();
                    await tlbGenerator.AddFromRegistry(details.TypeLibID, details.MajorVersion, details.MinorVersion, details.LCID);
                    break;
                case 1:
                    await tlbGenerator.AddFromFile(txbTypeLibFromFile.Text);
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidOperationException();
            }

            fileList.Clear();
            new TSBuilder().GetTypescript(tlbGenerator.NSSet).SelectKVP((name, ts) => new OutputFileDetails {
                Name = name,
                FileName = $"activex-{name.ToLower()}.d.ts",
                OutputFolder = txbOutputFolder.Text,
                WriteOutput = true,
                PackageForTypings = cbPackageForTypes.IsChecked.Value,
                OutputText = ts
            }).AddRangeTo(fileList);
        }

        private bool fillFolder() {
            if (!txbOutputFolder.Text.IsNullOrEmpty()) { folderDlg.SelectedPath = txbOutputFolder.Text; }
            var result = folderDlg.ShowDialog();
            if (result == Forms.DialogResult.Cancel) { return false; }
            txbOutputFolder.Text = folderDlg.SelectedPath;
            return true;
        }

        private void ApplyFilter() {
            CollectionViewSource.GetDefaultView(dgTypeLibs.ItemsSource).Filter = x => (((TypeLibDetails)x).Name ?? "").Contains(txbFilter.Text, StringComparison.OrdinalIgnoreCase);
        }
    }
}