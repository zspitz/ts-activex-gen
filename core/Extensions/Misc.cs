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
        public static void Add(this Dictionary<string, TSAliasDescription> dict, string key, TSSimpleType type) => dict.Add(key, new TSAliasDescription { TargetType = type });

        public static void RemoveMultipleAt<T>(this List<T> lst, IEnumerable<int> positions) => positions.Distinct().OrderedDescending().ForEach(x => lst.RemoveAt(x));

        static string[] builtins = new[] { "any", "void", "boolean", "string", "number", "undefined", "null", "never", "VarDate" };
        public static string[] NamedTypes(this ITSType type) => type.TypeParts().Select(x=>x.FullName).Except(builtins).Where(x => !IsLiteralTypeName(x)).ToArray();
        public static HashSet<string> NamedTypes(this IEnumerable<ITSType> types) => types.SelectMany(x => x.NamedTypes()).ToHashSet();
        public static string[] NamedTypes(this IEnumerable<string> types) => types.Except(builtins).Where(x => !IsLiteralTypeName(x)).ToArray();
        public static bool IsLiteralType(this ITSType type) => type is TSSimpleType x && x.IsLiteralType;

        //https://github.com/zspitz/ts-activex-gen/issues/32
        public static void AddInterfaceTo(this KeyValuePair<string, TSInterfaceDescription> x, TSNamespace ns) {
            if (x.Value.Members.Any()) {
                ns.Interfaces.Add(x);
            } else {
                ns.Aliases.Add(x.Key, TSSimpleType.Any);
            }
        }
        public static void AddInterfacesTo(this IEnumerable<KeyValuePair<string, TSInterfaceDescription>> src, TSNamespace ns) => src.ForEach(x => x.AddInterfaceTo(ns));
    }
}
