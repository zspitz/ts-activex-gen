using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TsActivexGen.Util;
using Microsoft.Win32;
using mshtml;
using static System.IO.Path;
using SHDocVw;

namespace TsActivexGen.wpf {
    public partial class MainWindow : Window {
        List<TypeLibDetails> typelibs = GetTypeLibs().ToList();
        public MainWindow() {
            InitializeComponent();

            dgTypeLibs.ItemsSource = typelibs;

            dgTypeLibs.Loaded += (s, e) => {
                ReloadDatagrid();
            };
            txbFilter.TextChanged += (s, e) => {
                if (txbFilter.Text.EndsWith(" ")) {
                    ReloadDatagrid();
                }
            };
            dgTypeLibs.SelectionChanged += (s, e) => {
                if (e.AddedItems.Count == 0) { return; }
                var added = (TypeLibDetails)e.AddedItems[0];
                var ns = new TlbInf32Generator(added.TypeLibID, added.MajorVersion, added.MinorVersion, added.LCID).Generate();
                var builder = new TSBuilder();
                var headers = new[] {
                    $"// Type definitions for {added.Name}",
                    "// Project: ",
                    "// Definitions by: Zev Spitz <https://github.com/zspitz>",
                    "// Definitions: https://github.com/DefinitelyTyped/DefinitelyTyped"
                };
                tbPreview.Text = builder.GetTypescript(ns, headers);
            };
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
