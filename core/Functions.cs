using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

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
    }
}
