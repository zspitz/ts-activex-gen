using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using static TsActivexGen.TSParameterType;

namespace TsActivexGen.Util {
    public static class Functions {
        public static KeyValuePair<TKey, TValue> KVP<TKey, TValue>(TKey key, TValue value) {
            return new KeyValuePair<TKey, TValue>(key, value);
        }
        public static string RelativeName(string typename, string @namespace) {
            if (IsLiteralTypeName(typename)) { return typename; }
            //HACK this doesn't handle generic parameters; we'd need to consider both the type and each of the type parameters
            //  but for the current purposes of the code, this is enough
            if (IsGenericTypeName(typename)) { return typename; }
            if (typename.StartsWith(@namespace + ".")) { return typename.Substring(@namespace.Length + 1); }
            return typename;
        }
        public static string NameOnly(string typename) {
            if (IsLiteralTypeName(typename)) { return typename; }
            return typename.Split('.').Last();
        }
        public static bool IsLiteralTypeName(string typename) {
            if (typename.IsNullOrEmpty()) { return false; }
            if (typename.In(new[] { "true", "false", "null", "undefined" }, StringComparison.Ordinal)) { return true; }
            var firstChar = typename[0];
            if (char.IsLetter(firstChar) || firstChar == '_') { return false; }
            return true;
        }

        static Regex re = new Regex("^.*<(.*)>$");
        public static string GenericParameter(string fullName) {
            //HACK The only generic types used in this code base are Enumerator<T>, which has a single parameter; this code really should do a full parse, but YAGNI
            if (IsLiteralTypeName(fullName)) { return null; }
            var match = re.Match(fullName);
            var ret = match.Groups[1].Value;
            if (ret.IsNullOrEmpty()) { return null; }
            return ret;
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
                    ret = $"{{{x.Members.JoinedKVP((key, val) => $"{key}: {GetTypeString(val, ns)}", ", ")}}}";
                    break;
                case TSFunctionType x:
                    ret = $"({x.FunctionDescription.Parameters.Joined(", ", y => GetParameterString(y, ns))}) => {GetTypeString(x.FunctionDescription.ReturnType, ns)}";
                    break;
                case TSUnionType x:
                    ret = x.Parts.Select(y=>GetTypeString(y,ns)).OrderBy(y=>y).Joined(" | ");
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
    }
}
