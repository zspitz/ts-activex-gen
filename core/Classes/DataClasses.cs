using System;
using System.Collections.Generic;
using TsActivexGen.Util;
using System.Linq;
using Microsoft.Win32;
using static TsActivexGen.TSParameterType;
using TLI;
using System.Diagnostics;

namespace TsActivexGen {
    public abstract class EqualityBase<T> : IEquatable<T> where T : class {
        public abstract bool Equals(T other);
        public override bool Equals(object other) {
            if (ReferenceEquals(null, other)) { return false; }
            if (ReferenceEquals(this, other)) { return true; }
            if (GetType() != other.GetType()) { return false; }
            return Equals(other as T);
        }
        public abstract override int GetHashCode();

        public static bool OperatorEquals(T x, T y) {
            if (ReferenceEquals(x, null)) { return ReferenceEquals(y, null); }
            return x.Equals(y);
        }
    }

    public class TSEnumDescription {
        public TSSimpleType Typename { get; set; }
        public Dictionary<string, string> Members { get; } = new Dictionary<string, string>(); //values -> string representation of value
        public List<KeyValuePair<string, string>> JsDoc { get; } = new List<KeyValuePair<string, string>>();
    }

    public class TSParameterDescription : EqualityBase<TSParameterDescription> {
        public ITSType Type { get; set; }
        public TSParameterType ParameterType { get; set; } = Standard;

        public override bool Equals(TSParameterDescription other) {
            return Type.Equals(other.Type)
                && ParameterType == other.ParameterType;
        }
        public override int GetHashCode() {
            unchecked {
                int hash = 17;
                hash = hash * 486187739 + Type.GetHashCode();
                hash = hash * 486187739 + ParameterType.GetHashCode();
                return hash;
            }
        }
        public override bool Equals(object other) => base.Equals(other);

        public static bool operator ==(TSParameterDescription x, TSParameterDescription y) => OperatorEquals(x, y);
        public static bool operator !=(TSParameterDescription x, TSParameterDescription y) => !OperatorEquals(x, y);
    }

    public class TSMemberDescription {
        public List<KeyValuePair<string, TSParameterDescription>> Parameters { get; set; } //(null means a property, empty means empty parameter list); this mut be a list, becaus parameter order is important
        public ITSType ReturnType { get; set; }
        public string Comment { get; set; }
        public bool? ReadOnly { get; set; }
        public List<KeyValuePair<string, string>> JsDoc { get; } = new List<KeyValuePair<string, string>>();

        public void AddParameter(string name, ITSType type) {
            if (Parameters == null) { Parameters = new List<KeyValuePair<string, TSParameterDescription>>(); }
            Parameters.Add(name, new TSParameterDescription() { Type = type });
        }
        public void AddParameter(string name, string type) => AddParameter(name, new TSSimpleType(type));
        public string[] TypeParts() {
            var ret = new List<string>();
            Parameters.DefaultIfNull().Values().SelectMany(x => x.Type.TypeParts()).AddRangeTo(ret);
            ReturnType.TypeParts().AddRangeTo(ret);
            return ret.ToArray();
        }
    }

    public class TSInterfaceDescription {
        public List<KeyValuePair<string, TSMemberDescription>> Members { get; } = new List<KeyValuePair<string, TSMemberDescription>>();
        public List<TSMemberDescription> Constructors { get; } = new List<TSMemberDescription>();
        public List<KeyValuePair<string, string>> JsDoc { get; } = new List<KeyValuePair<string, string>>();
    }

    public class TSNamespaceDescription {
        public Dictionary<string, string> Members { get; } = new Dictionary<string, string>();
        public List<KeyValuePair<string, string>> JsDoc { get; } = new List<KeyValuePair<string, string>>();
    }

    public class TSNamespace {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, TSEnumDescription> Enums { get; } = new Dictionary<string, TSEnumDescription>();
        public Dictionary<string, TSInterfaceDescription> Interfaces { get; } = new Dictionary<string, TSInterfaceDescription>();
        public Dictionary<string, TSNamespaceDescription> Namespaces { get; } = new Dictionary<string, TSNamespaceDescription>();
        public Dictionary<string, TSSimpleType> Aliases { get; } = new Dictionary<string, TSSimpleType>();
        public HashSet<string> Dependencies { get; } = new HashSet<string>();
        public Dictionary<string, TSInterfaceDescription> GlobalInterfaces { get; } = new Dictionary<string, TSInterfaceDescription>();

        public HashSet<string> GetUsedTypes() {
            var types = new List<string>();
            Interfaces.Values.Concat(GlobalInterfaces.Values).SelectMany(i => i.Members).Values().SelectMany(x => x.TypeParts()).NamedTypes().AddRangeTo(types);
            Aliases.Values().NamedTypes().AddRangeTo(types);
            return types.ToHashSet();
        }
        public HashSet<string> GetKnownTypes() {
            var ret = new[] { "any", "void", "boolean", "string", "number", "undefined", "null", "never", "VarDate" }.ToHashSet();
            Enums.Keys.AddRangeTo(ret);
            Interfaces.Keys.AddRangeTo(ret);
            Aliases.Keys.AddRangeTo(ret);
            ret.ToList().Select(x => x + "[]").AddRangeTo(ret);
            return ret;
        }
        public HashSet<string> GetUndefinedTypes() {
            var ret = GetUsedTypes();
            ret.ExceptWith(GetKnownTypes());
            return ret;
        }
        public List<KeyValuePair<string, string>> JsDoc { get; } = new List<KeyValuePair<string, string>>();
    }

    public class TSNamespaceSet {
        public Dictionary<string, TSNamespace> Namespaces { get; } = new Dictionary<string, TSNamespace>();
        public HashSet<string> GetUsedTypes() => Namespaces.SelectMany(x => x.Value.GetUsedTypes()).ToHashSet();
        public HashSet<string> GetKnownTypes() => Namespaces.SelectMany(x => x.Value.GetKnownTypes()).ToHashSet();
        public HashSet<string> GetUndefinedTypes() {
            var ret = GetUsedTypes();
            ret.ExceptWith(GetKnownTypes());
            return ret;
        }
    }

    public class TSParameterListComparer : IEqualityComparer<List<KeyValuePair<string, TSParameterDescription>>> {
        public bool Equals(List<KeyValuePair<string, TSParameterDescription>> x, List<KeyValuePair<string, TSParameterDescription>> y) {
            if (x == null) { return y == null; }
            return x.SequenceEqual(y);
        }
        public int GetHashCode(List<KeyValuePair<string, TSParameterDescription>> obj) {
            unchecked {
                return obj.Aggregate(17, (hash, x) => hash * 486187739 + x.GetHashCode());
            }
        }
    }

    public class NamespaceOutput {
        public string Description { get; set; }
        public string MainFile { get; set; }
        public HashSet<string> Dependencies { get; set; }
        public string RuntimeFile { get; set; }
        public string TestsFile { get; set; }
    }

    namespace ActiveX {
        public class TypeLibDetails {
            public static Lazy<List<TypeLibDetails>> FromRegistry = new Lazy<List<TypeLibDetails>>(() => {
                var tliapp = new TLIApplication();
                var ret = new List<TypeLibDetails>();

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
                                            var td = new TypeLibDetails() {
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
            });

            public string TypeLibID { get; set; }
            public string Name { get; set; }
            public string Version { get; set; }
            public short MajorVersion { get; set; }
            public short MinorVersion { get; set; }
            public short LCID { get; set; }
            public bool Is32bit { get; set; }
            public bool Is64bit { get; set; }
            public string RegistryKey { get; set; }
        }
    }
}