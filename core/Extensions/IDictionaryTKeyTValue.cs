using System;
using System.Collections.Generic;

namespace TsActivexGen.Util {
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
        public static void Add<TKey,TValue>(this IDictionary<TKey,TValue> dict, KeyValuePair<TKey,TValue> kvp) {
            dict.Add(kvp.Key, kvp.Value);
        }
    }
}
