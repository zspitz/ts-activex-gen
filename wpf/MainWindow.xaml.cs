﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TsActivexGen.Util;
using Microsoft.Win32;
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

namespace TsActivexGen.wpf {
    public partial class MainWindow : Window {
        List<TypeLibDetails> typelibs = GetTypeLibs().ToList();
        public MainWindow() {
            InitializeComponent();

            txbOutput.Text = Combine(GetDirectoryName(GetEntryAssembly().Location), "typings");

            dgTypeLibs.ItemsSource = typelibs;

            dgTypeLibs.Loaded += (s, e) => {
                ReloadDatagrid();
            };
            txbFilter.TextChanged += (s, e) => {
                if (txbFilter.Text.EndsWith(" ")) {
                    ReloadDatagrid();
                }
            };
            txbFilter.KeyUp += (s, e) => {
                if (e.Key == Enter) {
                    ReloadDatagrid();
                }
            };

            btnBrowseOutputFolder.Click += (s, e) => {
                fillFolder();
            };

            dgTypeLibs.SelectionChanged += (s, e) => {
                if (e.AddedItems.Count == 0) { return; }
                tbPreview.Text = getTypescript().Joined($"// {Enumerable.Repeat("-",80).Joined("")}");
                txbFilename.Text = dgTypeLibs.SelectedItem<TypeLibDetails>().Name.ToLower().Replace(" ", "-");
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

                getTypescript().ForEach((ts,index) => {
                    var basePath = index == 0 ? txbFilename.Text : InputBox(ts.FirstLine(), "Enter path for file");
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
                tbPreview.Text = getTypescript().Joined($"// {Enumerable.Repeat("-", 80)}");
            };
        }

        VistaFolderBrowserDialog folderDlg = new VistaFolderBrowserDialog() {
            ShowNewFolderButton = true
        };

        private bool fillFolder() {
            if (!txbOutput.Text.IsNullOrEmpty()) { folderDlg.SelectedPath = txbOutput.Text; }
            var result = folderDlg.ShowDialog();
            if (result == Forms.DialogResult.Cancel) { return false; }
            txbOutput.Text = folderDlg.SelectedPath;
            return true;
        }

        private string[] getTypescript() {
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
            return nsSet.Namespaces.SelectKVP((name, ns) => builder.GetTypescript(ns, new[] { $"// {name}" })).ToArray();
        }

        private void ReloadDatagrid() {
            IEnumerable<TypeLibDetails> lst = typelibs.OrderBy(x => x.Name);
            var terms = txbFilter.Text.Trim().Split(' ');
            if (terms.Any()) {
                lst = lst.Where(x => terms.Any(y => (x.Name ?? "").Contains(y, StringComparison.OrdinalIgnoreCase)));
            }
            dgTypeLibs.ItemsSource = lst;
        }

        private static IEnumerable<TypeLibDetails> GetTypeLibs() {
            using (var key = Registry.ClassesRoot.OpenSubKey("TypeLib")) {
                foreach (var tlbid in key.GetSubKeyNames()) {
                    using (var tlbkey = key.OpenSubKey(tlbid)) {
                        foreach (var version in tlbkey.GetSubKeyNames()) {
                            var indexOf = version.IndexOf(".");
                            short majorVersion;
                            short.TryParse(version.Substring(0, indexOf), out majorVersion);
                            short minorVersion;
                            short.TryParse(version.Substring(indexOf + 1), out minorVersion);
                            using (var versionKey = tlbkey.OpenSubKey(version)) {
                                var libraryName = (string)versionKey.GetValue("");
                                foreach (var lcid in versionKey.GetSubKeyNames()) {
                                    short lcidParsed;
                                    if (!short.TryParse(lcid, out lcidParsed)) { continue; } //exclude non-numeric keys such as FLAGS and HELPDIR
                                    using (var lcidKey = versionKey.OpenSubKey(lcid)) {
                                        var names = lcidKey.GetSubKeyNames();
                                        yield return new TypeLibDetails() {
                                            TypeLibID = tlbid,
                                            Name = libraryName,
                                            Version = version,
                                            MajorVersion = majorVersion,
                                            MinorVersion = minorVersion,
                                            LCID = short.Parse(lcid),
                                            Is32bit = names.Contains("win32"),
                                            Is64bit = names.Contains("win64"),
                                            RegistryKey = lcidKey.ToString()
                                        };
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}