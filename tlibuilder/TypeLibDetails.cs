using System;
using System.Collections.Generic;
using System.Linq;
using TLI;
using Microsoft.Win32;

namespace TsActivexGen.tlibuilder {
    public class TypeLibDetails {
        private static List<TypeLibDetails> initializer() {
            var tliapp = new TLIApplication();
            var ret = new List<TypeLibDetails>();

            using (var key = Registry.ClassesRoot.OpenSubKey("TypeLib")) {
                foreach (var tlbid in key.GetSubKeyNames()) {
                    using (var tlbkey = key.OpenSubKey(tlbid)) {
                        foreach (var version in tlbkey.GetSubKeyNames()) {
                            var indexOf = version.IndexOf(".");
                            version.Substring(0, indexOf).TryParse(out short? majorVersion);
                            version.Substring(indexOf + 1).TryParse(out short? minorVersion);
                            using (var versionKey = tlbkey.OpenSubKey(version)) {
                                var libraryName = (string)versionKey.GetValue("");
                                foreach (var lcid in versionKey.GetSubKeyNames()) {
                                    if (!short.TryParse(lcid, out short lcidParsed)) { continue; } //exclude non-numeric keys such as FLAGS and HELPDIR
                                    using (var lcidKey = versionKey.OpenSubKey(lcid)) {
                                        var names = lcidKey.GetSubKeyNames();
                                        var td = new TypeLibDetails() {
                                            TypeLibID = tlbid,
                                            Name = libraryName,
                                            Version = version,
                                            MajorVersion = majorVersion ?? 0,
                                            MinorVersion = minorVersion ?? 0,
                                            LCID = lcidParsed,
                                            Is32bit = names.Contains("win32"),
                                            Is64bit = names.Contains("win64"),
                                            RegistryKey = lcidKey.ToString()
                                        };
                                        if (!char.IsDigit(td.Version[0])) {
                                            var paths = new HashSet<string>();
                                            if (td.Is32bit) {
                                                paths.Add((string)lcidKey.OpenSubKey("win32").GetValue(""));
                                            }
                                            if (td.Is64bit) {
                                                paths.Add((string)lcidKey.OpenSubKey("win64").GetValue(""));
                                            }
                                            if (paths.Count > 1) {
                                                continue;
                                            }
                                            var tli = tliapp.TypeLibInfoFromFile(paths.First());
                                            td.MajorVersion = tli.MajorVersion;
                                            td.MinorVersion = tli.MinorVersion;
                                        }
                                        ret.Add(td);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return ret.OrderBy(x => x.Name).ToList();
        }

        public static Lazy<List<TypeLibDetails>> FromRegistry = new Lazy<List<TypeLibDetails>>(initializer);

        public string TypeLibID { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public short MajorVersion { get; set; }
        public short MinorVersion { get; set; }
        public short LCID { get; set; }
        public bool Is32bit { get; set; }
        public bool Is64bit { get; set; }
        public string RegistryKey { get; set; }
        public bool Selected { get; set; }

        public override string ToString() {
            var bittedness = "AnyCPU";
            if (!Is32bit && !Is64bit) {
                bittedness = "None";
            } else if (!Is32bit) {
                bittedness = "32bit";
            } else if (!Is64bit) {
                bittedness = "64bit";
            }
            return $"Name={Name}, Version={Version} ({MajorVersion}.{MinorVersion}), {bittedness}";
        }
    }
}