using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using static TsActivexGen.TSParameterType;
using static System.Environment;
using static System.Linq.Enumerable;

namespace TsActivexGen {
    public static class Functions {
        public static KeyValuePair<TKey, TValue> KVP<TKey, TValue>(TKey key, TValue value) => new KeyValuePair<TKey, TValue>(key, value);

        public static string RelativeName(string absoluteType, string withinNamespace) {
            if (IsLiteralTypeName(absoluteType)) { return absoluteType; }

            //HACK this doesn't handle generic parameters; we'd need to consider both the type and each of the type parameters
            //  but for the current purposes of the code, this is enough, because the only generic type we are using is Enumerator<T>,
            //  which is in the global namespace
            if (IsGenericTypeName(absoluteType)) { return absoluteType; }

            var (typeNamespace, typeOnly) = SplitName(absoluteType);
            if (typeNamespace.Length==0) { return absoluteType; }

            var typeNamespaceParts = typeNamespace.Split('.');
            var partsOfWithinNamespace = withinNamespace.Split('.');
            var retParts = new List<string>();
            var pathMismatch = false;
            for (int i = 0; i < typeNamespaceParts.Length; i++) {
                if (partsOfWithinNamespace.Length <= i || typeNamespaceParts[i] != partsOfWithinNamespace[i]) {
                    pathMismatch = true;
                }
                if (pathMismatch) {
                    retParts.Add(typeNamespaceParts[i]);
                }
            }
            retParts.Add(typeOnly);
            return retParts.Joined(".");
        }
        public static (string @namespace, string name) SplitName(string typename) {
            if (IsLiteralTypeName(typename)) { return ("", typename); }
            switch (typename.LastIndexOf('.')) {
                case int x when x < 0:
                    return ("", typename);
                case int x when x == 0:
                    return ("", typename.Substring(1));
                case int x:
                    return (typename.Substring(0, x), typename.Substring(x + 1));
            }
        }
        public static bool IsLiteralTypeName(string typename) {
            if (typename.IsNullOrEmpty()) { return false; }
            if (typename.In(new[] { "true", "false", "null", "undefined" }, StringComparison.Ordinal)) { return true; }
            var firstChar = typename[0];
            if (char.IsLetter(firstChar) || firstChar == '_') { return false; }
            return true;
        }
        public static string MakeNamespace(string part1, string part2) {
            if (part1.IsNullOrEmpty() || part2.IsNullOrEmpty()) {
                return part1 + part2;
            } else {
                return $"{part1}.{part2}";
            }
        }

        public static bool IsGenericTypeName(string fullName) => !IsLiteralTypeName(fullName) && fullName.Contains("<");

        public static string GetProgIDFromCLSID(string clsid) {
            using (var key = Registry.ClassesRoot.OpenSubKey($"CLSID\\{clsid}")) {
                using (var subkey = key?.OpenSubKey("VersionIndependentProgID") ?? key?.OpenSubKey("ProgID")) {
                    return (string)subkey?.GetValue("");
                }
            }
        }

        public static string GetTypeString(ITSType type, string ns) {
            string ret = null;

            switch (type) {
                case TSSimpleType x:
                    ret = RelativeName(x.GenericParameter ?? x.FullName, ns);
                    break;
                case TSTupleType x:
                    ret = $"[{x.Members.Joined(", ", y => GetTypeString(y, ns))}]";
                    break;
                case TSObjectType x:
                    ret = $"{{{x.Members.JoinedKVP((key, val) => $"{(val.@readonly ? "readonly " : "")}{key}: {GetTypeString(val.type, ns)}", ", ")}}}";
                    break;
                case TSFunctionType x:
                    ret = $"({x.FunctionDescription.Parameters.Joined(", ", y => GetParameterString(y, ns))}) => {GetTypeString(x.FunctionDescription.ReturnType, ns)}";
                    break;
                case TSUnionType x:
                    ret = x.Parts.Select(y => GetTypeString(y, ns)).OrderBy(y => y).Joined(" | ");
                    break;
                default:
                    if (type != null) { throw new NotImplementedException(); }
                    break;
            }

            return ret;
        }

        public static string GetParameterString(KeyValuePair<string, TSParameterDescription> x, string ns) {
            var name = x.Key;
            var parameterDescription = x.Value;
            if (parameterDescription.ParameterType == Rest) {
                name = "..." + name;
            } else if (parameterDescription.ParameterType == Optional) {
                name += "?";
            }
            return $"{name}: {GetTypeString(parameterDescription.Type, ns)}";
        }

        public static string NewLines(int count) => Repeat(NewLine, count).Joined("");
    }
}
