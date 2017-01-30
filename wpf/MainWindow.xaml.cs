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

namespace TsActivexGen.wpf {
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();

            txbOutput.Text = Combine(GetDirectoryName(GetEntryAssembly().Location), "typings");

            dgTypeLibs.ItemsSource = TypeLibDetails.FromRegistry.Value;

            txbFilter.TextChanged += (s, e) => ApplyFilter();

            btnBrowseOutputFolder.Click += (s, e) => fillFolder();

            dgTypeLibs.SelectionChanged += (s, e) => {
                if (e.AddedItems.Count == 0) { return; }
                loadPreviewText();
            };

            btnOutput.Click += (s, e) => {
                if (txbFilename.Text.IsNullOrEmpty()) { return; }
                if (txbOutput.Text.IsNullOrEmpty()) {
                    var filled = fillFolder();
                    if (!filled) { return; }
                }
                if (!Directory.Exists(txbOutput.Text)) {
                    Directory.CreateDirectory(txbOutput.Text);
                }

                currentTypescript.ForEachKVP((name, ts, index) => {
                    var basePath = index == 0 ? txbFilename.Text : InputBox(name, "Enter path for file");
                    if (basePath == "") { return; }
                    var filepath = Combine(txbOutput.Text, $"{basePath}.d.ts");
                    WriteAllText(filepath, ts);
                    if (chkOutputTests.IsChecked == true) {
                        var testsFilePath = Combine(txbOutput.Text, $"{basePath}-tests.ts");
                        if (!Exists(testsFilePath) || MessageBox.Show("Overwrite existing test file?", "", YesNo) == Yes) {
                            WriteAllLines(testsFilePath, new[] {
                                    $"/// <reference path=\"{filepath}\" />",""
                                });
                        }
                    }
                });

                var firstFilePath = Combine(txbOutput.Text, $"{txbFilename.Text}.d.ts");
                var psi = new ProcessStartInfo("explorer.exe", "/n /e,/select,\"" + firstFilePath + "\"");
                Process.Start(psi);
            };

            var fileDlg = new VistaOpenFileDialog();
            fileDlg.CheckFileExists = true;
            fileDlg.CheckPathExists = true;
            fileDlg.Multiselect = false;
            btnBrowseTypeLibFile.Click += (s, e) => {
                if (!txbTypeLibFromFile.Text.IsNullOrEmpty()) { fileDlg.FileName = txbTypeLibFromFile.Text; }
                if (fileDlg.ShowDialog() == Forms.DialogResult.Cancel) { return; }
                txbTypeLibFromFile.Text = fileDlg.FileName;
                loadPreviewText();
            };
        }

        VistaFolderBrowserDialog folderDlg = new VistaFolderBrowserDialog() {
            ShowNewFolderButton = true
        };

        private void loadPreviewText() {
            loadTypescript();
            tbPreview.Text= currentTypescript.JoinedKVP((name, text) => new string[] {
                $"// {Enumerable.Repeat("-", 80).Joined("")}",
                $"// {name}",
                text
            }.Joined(Environment.NewLine), Environment.NewLine);
        }

        private bool fillFolder() {
            if (!txbOutput.Text.IsNullOrEmpty()) { folderDlg.SelectedPath = txbOutput.Text; }
            var result = folderDlg.ShowDialog();
            if (result == Forms.DialogResult.Cancel) { return false; }
            txbOutput.Text = folderDlg.SelectedPath;
            return true;
        }

        List<KeyValuePair<string, string>> currentTypescript;

        private void loadTypescript() {
            var headers = new List<string>();
            ITSNamespaceGenerator generator = null;
            TlbInf32Generator tlbGenerator;

            switch (tcMain.SelectedIndex) {
                case 0:
                    var details = dgTypeLibs.SelectedItem<TypeLibDetails>();
                    //new[] {
                    //    $"// Type definitions for {details.Name}",
                    //    "// Project: <project url>",
                    //    "// Definitions by: Zev Spitz <https://github.com/zspitz>",
                    //    "// Definitions: https://github.com/DefinitelyTyped/DefinitelyTyped"
                    //}.AddRangeTo(headers);
                    //generator = TlbInf32Generator.CreateFromRegistry(details.TypeLibID, details.MajorVersion, details.MinorVersion, details.LCID);
                    tlbGenerator = new TlbInf32Generator();
                    tlbGenerator.AddFromRegistry(details.TypeLibID, details.MajorVersion, details.MinorVersion, details.LCID);
                    generator = tlbGenerator;
                    break;
                case 1:
                    //new[] {
                    //    "// Type definitions for <library description here>",
                    //    "// Project: <project url>",
                    //    "// Definitions by: Zev Spitz <https://github.com/zspitz>",
                    //    "// Definitions: https://github.com/DefinitelyTyped/DefinitelyTyped"
                    //}.AddRangeTo(headers);
                    //generator = TlbInf32Generator.CreateFromFile(txbTypeLibFromFile.Text);
                    tlbGenerator = new TlbInf32Generator();
                    tlbGenerator.AddFromFile(txbTypeLibFromFile.Text);
                    generator = tlbGenerator;
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidOperationException();
            }

            var nsSet = generator.Generate();
            var builder = new TSBuilder() { WriteValueOnlyNamespaces = (bool)chkModulesWithConstants.IsChecked };
            currentTypescript = builder.GetTypescript(nsSet);
        }

        private void ApplyFilter() {
            CollectionViewSource.GetDefaultView(dgTypeLibs.ItemsSource).Filter = x => (((TypeLibDetails)x).Name ?? "").Contains(txbFilter.Text, StringComparison.OrdinalIgnoreCase);
        }
    }
}