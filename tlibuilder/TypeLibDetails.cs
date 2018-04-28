using System;
using System.Collections.Generic;
using System.Linq;
using TLI;
using Microsoft.Win32;
using System.ComponentModel;

namespace TsActivexGen.tlibuilder {
    public class TypeLibDetails : INotifyPropertyChanged {
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
                                        if (td.Is32bit) {
                                            td.Path32bit = (string)lcidKey.OpenSubKey("win32").GetValue("");
                                        }
                                        if (td.Is64bit) {
                                            td.Path64bit = (string)lcidKey.OpenSubKey("win64").GetValue("");
                                        }
                                        if (!char.IsDigit(td.Version[0])) {
                                            var versions = new[] { td.Path32bit, td.Path64bit }
                                                .Where(x => !x.IsNullOrEmpty())
                                                .Select(x => {
                                                    var tli = tliapp.TypeLibInfoFromFile(x);
                                                    return (tli.MajorVersion, tli.MinorVersion);
                                                })
                                                .Distinct()
                                                .ToList();
                                            if (versions.Count>1) { continue; } // we can't resolve an absolute major / minor version, because the different paths have two different versions
                                            td.MajorVersion = versions[0].MajorVersion;
                                            td.MinorVersion = versions[0].MinorVersion;
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
        public string Path32bit { get; set; }
        public bool Is64bit { get; set; }
        public string Path64bit { get; set; }
        public string RegistryKey { get; set; }
        public bool Selected { get; set; }

        public override string ToString() {
            var bittedness = "AnyCPU";
            if (Is32bit && Is64bit) {
                bittedness = "32/64bit";
            } else if (Is32bit) {
                bittedness = "32bit";
            } else if (Is64bit) {
                bittedness = "64bit";
            } else {
                bittedness = "Unknown";
            }
            return $"Name={Name}, Version={Version} ({MajorVersion}.{MinorVersion}), {bittedness}";
        }

        public string Tooltip {
            get {
                var parts = new List<string>();
                parts.Add(ToString());
                if (!Path32bit.IsNullOrEmpty()) { parts.Add($"32-bit path: {Path32bit}"); }
                if (!Path64bit.IsNullOrEmpty()) { parts.Add($"64-bit path: {Path64bit}"); }
                parts.Add(RegistryKey);
                return parts.Joined("\n");
            }
        }

#pragma warning disable 0067
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 0067

        public TypeLibInfo GetTypeLibInfo(TLIApplication tliapp = null) {
            tliapp = tliapp ?? tliappInstance.Value;
            try {
                return tliapp.TypeLibInfoFromRegistry(TypeLibID, MajorVersion, MinorVersion, LCID);
            } catch (Exception) {
                // sometimes the version includes an alphabetical component -- e.g. c.0
                // since TypeLibInfoFromRegistry only takes numeric arguments for major / minor versions, it throws an exception when 0 is passed in
                // so we use the path stored in the registry; preferring 64-bit if available
                var path = Path64bit.IsNullOrEmpty() ? Path32bit : Path64bit;
                return tliapp.TypeLibInfoFromFile(path);
            }
        }

        private static Lazy<TLIApplication> tliappInstance = new Lazy<TLIApplication>(() => new TLIApplication());
    }
}