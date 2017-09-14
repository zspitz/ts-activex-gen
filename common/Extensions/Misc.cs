using System;
using System.Collections.Generic;
using System.Linq;
using static TsActivexGen.Functions;

namespace TsActivexGen {
    public static class MiscExtensions {
        public static bool In<T>(this T val, IEnumerable<T> vals) => vals.Contains(val);
        public static bool In<T>(this T val, params T[] vals) => vals.Contains(val);
        public static bool In(this char c, string s) => s.IndexOf(c) > -1;
        public static bool NotIn<T>(this T val, IEnumerable<T> vals) => !vals.Contains(val);
        public static bool NotIn<T>(this T val, params T[] vals) => !vals.Contains(val);
        public static bool NotIn(this char c, string s) => s.IndexOf(c) == -1;

        public static void Add<TKey, TValue>(this ICollection<KeyValuePair<TKey, TValue>> col, TKey key, TValue value) => col.Add(KVP(key, value));
        public static void Add(this Dictionary<string, TSAliasDescription> dict, string key, TSSimpleType type, IEnumerable<KeyValuePair<string, string>> jsDoc = null) {
            var alias = new TSAliasDescription { TargetType = type };
            jsDoc?.AddRangeTo(alias.JsDoc);
            dict.Add(key, alias);
        }

        public static void RemoveMultipleAt<T>(this List<T> lst, IEnumerable<int> positions) => positions.Distinct().OrderedDescending().ForEach(x => lst.RemoveAt(x));

        public static readonly string[] builtins = new[] { "any", "void", "boolean", "string", "number", "undefined", "null", "never", "VarDate", "Array<>" };
        public static bool IsLiteralType(this ITSType type) => type is TSSimpleType x && x.IsLiteralType;
        public static bool IsBuiltIn(this ITSType type) => type is TSSimpleType x && x.FullName.In(builtins);

        /// empty interfaces are added as aliases
        public static void AddInterfaceTo(this KeyValuePair<string, TSInterfaceDescription> x, TSNamespaceDescription ns) {
            if (x.Value.Members.Any() || x.Value.Extends.Count > 1) {
                ns.Interfaces.Add(x);
            } else if (x.Value.Extends.Any()) {
                ns.Aliases.Add(x.Key, x.Value.Extends.First(), x.Value.JsDoc);
            } else {
                ns.Aliases.Add(x.Key, TSSimpleType.Any, x.Value.JsDoc);
            }
        }
        public static void AddInterfacesTo(this IEnumerable<KeyValuePair<string, TSInterfaceDescription>> src, TSRootNamespaceDescription ns) => src.ForEach(x => x.AddInterfaceTo(ns));

        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, out TKey key, out TValue value) {
            key = kvp.Key;
            value = kvp.Value;
        }

        public static void AddRangeTo(this IEnumerable<KeyValuePair<string, string>> toAdd, Dictionary<string, TSEnumValueDescription> dest) =>
            toAdd.SelectKVP((key, value) => KVP(key, new TSEnumValueDescription() { Value = value })).AddRangeTo(dest);

        public static IEnumerable<(string interfaceName, string memberName, TSMemberDescription descr)> AllMembers(this KeyValuePair<string, TSInterfaceDescription> kvp, TSNamespaceSet nsset) =>
            kvp.Value.Members.Select(kvp1 => (kvp.Key, kvp1.Key, kvp1.Value))
            .Concat(kvp.Value.InheritedMembers(nsset))
            .ToList();

        public static KeyValuePair<string, TSMemberDescription> Clone(this KeyValuePair<string, TSMemberDescription> kvp) {
            var original = kvp.Value;
            return KVP(kvp.Key, original.Clone());
        }

        public static KeyValuePair<string, TSParameterDescription> Clone(this KeyValuePair<string, TSParameterDescription> kvp) {
            var original = kvp.Value;
            var ret = new TSParameterDescription();
            ret.ParameterType = original.ParameterType;
            ret.Type = original.Type;
            return KVP(kvp.Key, ret);
        }

        public static void MakeNominal(this KeyValuePair<string, TSInterfaceDescription> kvp) {
            var (name, descr) = kvp;
            if (!descr.IsClass) { throw new InvalidOperationException("Unable to make interface nominal"); }
            var typekey = $"{name}_typekey";
            var current = descr.Members.Get(typekey);
            if (current != null) {
                if (current.Private) { return; }
                throw new InvalidOperationException("Already existing public member with typekey");
            }
            descr.Members.Add(typekey, new TSMemberDescription() { ReturnType = (TSSimpleType)name, Private = true });
        }
    }
}
