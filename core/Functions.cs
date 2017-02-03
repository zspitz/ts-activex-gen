using System;
using System.Collections.Generic;
using System.Linq;

namespace TsActivexGen.Util {
    public static class Functions {
        public static KeyValuePair<TKey, TValue> KVP<TKey, TValue>(TKey key, TValue value) {
            return new KeyValuePair<TKey, TValue>(key, value);
        }
        public static string RelativeName(string typename, string @namespace) {
            if (IsLiteralTypeName(typename)) { return typename; }
            if (typename.StartsWith(@namespace + ".")) { return typename.Substring(@namespace.Length + 1); }
            return typename;
        }
        public static string NameOnly(string typename) {
            if (IsLiteralTypeName(typename)) { return typename; }
            return typename.Split('.').Last();
        }
        public static bool IsLiteralTypeName(string typename) {
            if (typename.In(new[] { "true", "false", "null", "undefined" }, StringComparison.Ordinal)) { return true; }
            var firstChar = typename[0];
            if (char.IsLetter(firstChar) || firstChar == '_') { return false; }
            return true;
        }
    }
}
