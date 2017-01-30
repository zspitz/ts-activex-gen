using System;
using System.Collections.Generic;
using TsActivexGen.Util;
using System.Linq;
using Microsoft.Win32;
using static TsActivexGen.Util.Functions;

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

    public class TSTypeName : EqualityBase<TSTypeName> {
        public static TSTypeName Any = new TSTypeName { FullName = "any" };
        public string FullName { get; set; }
        public string Namespace {
            get {
                var parts = FullName.Split('.');
                if (parts.Length == 1) { return ""; }
                return parts[0];
            }
        }
        public string RelativeName(string currentNamespace) => Functions.RelativeName(FullName, currentNamespace);
        public string NameOnly => FullName.Split('.').Last();
        public string Comment { get; set; }

        public override bool Equals(TSTypeName other) {
            return FullName == other?.FullName;
        }
        public override int GetHashCode() {
            return FullName.GetHashCode();
        }
        public override bool Equals(object other) {
            return base.Equals(other);
        }

        public static bool operator ==(TSTypeName x, TSTypeName y) {
            return OperatorEquals(x, y);
        }
        public static bool operator !=(TSTypeName x, TSTypeName y) {
            return !OperatorEquals(x, y);
        }

        public override string ToString() {
            var comment = Comment;
            if (!comment.IsNullOrEmpty()) { comment = $" /*{comment}*/"; }
            return $"{FullName}{comment}";
        }
    }

    public class TSEnumDescription {
        public TSTypeName Typename { get; set; }
        public Dictionary<string, string> Members { get; } = new Dictionary<string, string>(); //values -> string representation of value
    }

    public class TSParameterDescription : EqualityBase<TSParameterDescription> {
        public TSTypeName Typename { get; set; }
        public TSParameterType ParameterType { get; set; }

        public override bool Equals(TSParameterDescription other) {
            return Typename == other.Typename
                && ParameterType == other.ParameterType;
        }
        public override int GetHashCode() {
            unchecked {
                int hash = 17;
                hash = hash * 486187739 + Typename.GetHashCode();
                hash = hash * 486187739 + ParameterType.GetHashCode();
                return hash;
            }
        }
        public override bool Equals(object other) {
            return base.Equals(other);
        }

        public static bool operator ==(TSParameterDescription x, TSParameterDescription y) {
            return OperatorEquals(x, y);
        }
        public static bool operator !=(TSParameterDescription x, TSParameterDescription y) {
            return !OperatorEquals(x, y);
        }
    }

    public class TSMemberDescription {
        public List<KeyValuePair<string, TSParameterDescription>> Parameters { get; set; } //(null means a property, empty means empty parameter list); this mut be a list, becaus parameter order is important
        public TSTypeName ReturnTypename { get; set; }
        public string Comment { get; set; }
        public bool? ReadOnly { get; set; }
    }

    public class TSInterfaceDescription {
        //TODO This functionality is specific to ActiveX definition creation, and should really be in the TlbInf32Generator class
        public bool IsActiveXCreateable { get; set; }
        public string EnumerableType { get; set; }
        public Dictionary<string, TSMemberDescription> Members { get; } = new Dictionary<string, TSMemberDescription>();
    }

    public class TSNamespaceDescription {
        public Dictionary<string, string> Members { get; } = new Dictionary<string, string>();
    }

    public class TSNamespace {
        public string Name { get; set; }
        public Dictionary<string, TSEnumDescription> Enums { get; } = new Dictionary<string, TSEnumDescription>();
        public Dictionary<string, TSInterfaceDescription> Interfaces { get; } = new Dictionary<string, TSInterfaceDescription>();
        public Dictionary<string, TSNamespaceDescription> Namespaces { get; } = new Dictionary<string, TSNamespaceDescription>();
        public Dictionary<string, TSTypeName> Aliases { get; } = new Dictionary<string, TSTypeName>();
        public HashSet<string> Depndencies { get; } = new HashSet<string>();

        public HashSet<string> GetUsedTypes() {
            //parameters, return type, alias mapping
            var ret = new HashSet<string>();
            var members = Interfaces.SelectMany(i => i.Value.Members);
            members.SelectMany(kvp => kvp.Value.Parameters.DefaultIfNull()).SelectKVP((parameterName, p) => p.Typename.FullName).AddRangeTo(ret);
            members.SelectKVP((memberName, m) => m.ReturnTypename.FullName).AddRangeTo(ret);
            Aliases.SelectKVP((aliasName, a) => a.FullName).AddRangeTo(ret);
            return ret;
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
    }

    public class TSNamespaceSet {
        public Dictionary<string, TSNamespace> Namespaces { get; } = new Dictionary<string, TSNamespace>();
        public HashSet<string> GetUsedTypes() {
            return Namespaces.SelectMany(x => x.Value.GetUsedTypes()).ToHashSet();
        }
        public HashSet<string> GetKnownTypes() {
            return Namespaces.SelectMany(x => x.Value.GetKnownTypes()).ToHashSet();
        }
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

    namespace ActiveX {
        public class TypeLibDetails {
            public static Lazy<List<TypeLibDetails>> FromRegistry = new Lazy<List<TypeLibDetails>>(() => {
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
                                            ret.Add(new TypeLibDetails() {
                                                TypeLibID = tlbid,
                                                Name = libraryName,
                                                Version = version,
                                                MajorVersion = majorVersion,
                                                MinorVersion = minorVersion,
                                                LCID = short.Parse(lcid),
                                                Is32bit = names.Contains("win32"),
                                                Is64bit = names.Contains("win64"),
                                                RegistryKey = lcidKey.ToString()
                                            });
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