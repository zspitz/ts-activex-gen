using System;
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

namespace TsActivexGen.wpf {
    public partial class MainWindow : Window {
        List<TypeLibDetails> typelibs = GetTypeLibs().ToList();
        public MainWindow() {
            InitializeComponent();

            txbOutput.Text = Combine(GetDirectoryName(GetEntryAssembly().Location), "typings");

            cmbVarDateHandling.ItemsSource = new List<string>() { "Don't generate", "Include in file", "Generate external" };

            dgTypeLibs.ItemsSource = typelibs;

            dgTypeLibs.Loaded += (s, e) => {
                ReloadDatagrid();
            };
            txbFilter.TextChanged += (s, e) => {
                if (txbFilter.Text.EndsWith(" ")) {
                    ReloadDatagrid();
                }
            };

            btnBrowseOutputFolder.Click += (s, e) => {
                fillFolder();
            };

            dgTypeLibs.SelectionChanged += (s, e) => {
                if (e.AddedItems.Count == 0) { return; }
                var details = (TypeLibDetails)e.AddedItems[0];
                tbPreview.Text = getTypescript(details);
                txbFilename.Text = details.Name.ToLower().Replace(" ", "-");
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

                var details = dgTypeLibs.SelectedItem<TypeLibDetails>();
                var ts = getTypescript(details);

                var filepath = Combine(txbOutput.Text, $"{txbFilename.Text}.d.ts");
                if (Exists(filepath) || MessageBox.Show("Overwrite existing?", "", YesNo) == Yes) {
                    WriteAllText(filepath, ts);
                    if (chkOutputTests.IsChecked == true) {
                        var testsFilePath = Combine(txbOutput.Text, $"{txbFilename.Text}-tests.ts");
                        if (Exists(testsFilePath) || MessageBox.Show("Overwrite existing?", "", YesNo) == Yes) {
                            WriteAllLines(testsFilePath, new[] {
                                $"/// <reference path=\"{filepath}\" />",""
                            });
                        }
                    }
                }

                if (cmbVarDateHandling.SelectedIndex == 2) {
                    WriteAllText(Combine(txbOutput.Text, "jscript-extensions.d.ts"), varDateDefinition);
                }

                var psi = new ProcessStartInfo("explorer.exe", "/n /e,/select,\"" + filepath + "\"");
                Process.Start(psi);
            };
        }

        private bool fillFolder() {
            var dlg = new VistaFolderBrowserDialog();
            dlg.ShowNewFolderButton = true;
            if (!txbOutput.Text.IsNullOrEmpty()) { dlg.SelectedPath = txbOutput.Text; }
            var result = dlg.ShowDialog();
            if (result == Forms.DialogResult.Cancel) { return false; }
            txbOutput.Text = dlg.SelectedPath;
            return true;
        }

        private const string varDateDefinition = @"
interface VarDate { }

interface DateConstructor {
    new (vd: VarDate): Date;
}

interface Date {
    getVarDate: () => VarDate;
}
";

        private string getTypescript(TypeLibDetails details) {
            //TODO needs to be changed to work with WMI details also
            var headers = new[] {
                $"// Type definitions for {details.Name}",
                "// Project: ",
                "// Definitions by: Zev Spitz <https://github.com/zspitz>",
                "// Definitions: https://github.com/DefinitelyTyped/DefinitelyTyped"
            }.ToList();
            if (cmbVarDateHandling.SelectedIndex == 1) {
                headers.Add(varDateDefinition);
            }
            var ns = new TlbInf32Generator(details.TypeLibID, details.MajorVersion, details.MinorVersion, details.LCID).Generate();
            var builder = new TSBuilder();
            return new TSBuilder().GetTypescript(ns, headers);
        }

        private void ReloadDatagrid() {
            IEnumerable<TypeLibDetails> lst = typelibs.OrderBy(x => x.Name);
            var filterText = txbFilter.Text.Trim();
            if (!filterText.IsNullOrEmpty()) {
                lst = lst.Where(x => (x.Name ?? "").Contains(filterText, StringComparison.OrdinalIgnoreCase));
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


//TODO enable loading from file