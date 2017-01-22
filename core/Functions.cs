using System.Collections.Generic;
using System.Linq;

namespace TsActivexGen.Util {
    public static class Functions {
        public static KeyValuePair<TKey, TValue> KVP<TKey, TValue>(TKey key, TValue value) {
            return new KeyValuePair<TKey, TValue>(key, value);
        }
        public static string RelativeName(string typename, string @namespace) {
            if (typename.StartsWith(@namespace + ".")) { return typename.Substring(@namespace.Length + 1); }
            return typename;
        }
        public static string NameOnly(string typename) => typename.Split('.').Last();
    }
}
