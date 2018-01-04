using System;
using System.Collections.Generic;
using System.Linq;

namespace TsActivexGen {
    public static class IDictionaryTKeyTValueExtensions {
        public static bool IfContainsKey<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Action<TValue> action = null, Action @else = null) {
            TValue val;
            if (dict != null && dict.TryGetValue(key, out val)) {
                action?.Invoke(val);
                return true;
            }
            @else?.Invoke();
            return false;
        }
        public static bool IfContainsKey<TKey,TValue>(this ILookup<TKey,TValue> lookup, TKey key, Action<IEnumerable<TValue>> action=null, Action @else=null) {
            var exists = false;
            if (lookup != null) {
                var grp = lookup[key];
                if (grp.Any()) {
                    exists = true;
                    action?.Invoke(grp);
                }
            }
            if (!exists) {
                @else?.Invoke();
            }
            return exists;
        }
        public static void Add<TKey, TValue>(this IDictionary<TKey, TValue> dict, KeyValuePair<TKey, TValue> kvp) {
            dict.Add(kvp.Key, kvp.Value);
        }
        public static TValue FindOrNew<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key) where TValue : new() {
            TValue ret;
            if (!dict.TryGetValue(key, out ret)) {
                ret = new TValue();
                dict[key] = ret;
            }
            return ret;
        }
        public static List<TKey> Keys<TKey, TElement>(this ILookup<TKey, TElement> lookup) => lookup.Select(grp => grp.Key).ToList();
        public static void SetIn<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, Dictionary<TKey, TValue> dict) => dict[kvp.Key] = kvp.Value;
    }
}
