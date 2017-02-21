using System;
using System.Collections.Generic;
using System.Linq;
using static TsActivexGen.Util.Functions;

namespace TsActivexGen.Util {
    public static class MiscExtensions {
        public static bool In<T>(this T val, IEnumerable<T> vals)  =>vals.Contains(val);
        public static bool In<T>(this T val, params T[] vals) => vals.Contains(val);
        public static bool NotIn<T>(this T val, IEnumerable<T> vals) => !vals.Contains(val);
        public static bool NotIn<T>(this T val, params T[] vals) => !vals.Contains(val);

        public static void Add<TKey,TValue>(this ICollection<KeyValuePair<TKey,TValue>> col, TKey key, TValue value) => col.Add(KVP(key, value));

        static string[] builtins = new[] { "any", "void", "boolean", "string", "number", "undefined", "null", "never", "VarDate" };
        public static string[] NamedTypes(this ITSType type) => type.TypeParts().Except(builtins).Where(x => !IsLiteralTypeName(x)).ToArray();
        public static HashSet<string> NamedTypes(this IEnumerable<ITSType> types) => types.SelectMany(x => x.NamedTypes()).ToHashSet();
        public static bool IsLiteralType(this ITSType type) => IsLiteralTypeName((type as TSSimpleType)?.FullName);
    }
}
