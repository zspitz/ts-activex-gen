using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;

namespace TsActivexGen {
    public static class IEnumerableKVPExtensions {
        public static void ForEachKVP<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> src, Action<TKey, TValue> action) =>
            src.ForEach(x => {
                action(x.Key, x.Value);
            });
        public static IEnumerable<KeyValuePair<TKey, TValue>> ForEachKVP<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> src, Action<TKey, TValue, int> action) {
            var current = 0;
            foreach (var kvp in src) {
                action(kvp.Key, kvp.Value, current);
                current += 1;
            }
            return src;
        }
        public static IEnumerable<TResult> SelectKVP<TKey, TValue, TResult>(this IEnumerable<KeyValuePair<TKey, TValue>> src, Func<TKey, TValue, TResult> selector) => src.Select(kvp => selector(kvp.Key, kvp.Value));

        public static string JoinedKVP<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> src, Func<TKey, TValue, string> selector, string delimiter = ",") => src.SelectKVP(selector).Joined(delimiter);
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> src) => src.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        public static IEnumerable<KeyValuePair<TKey, TValue>> WhereKVP<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> src, Func<TKey, TValue, bool> predicate) => src.Where(kvp => predicate(kvp.Key, kvp.Value));
        public static void AddRangeTo<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> toAdd, Dictionary<TKey, TValue> dict) => toAdd.ForEach(kvp => dict.Add(kvp));
        public static IEnumerable<KeyValuePair<TKey, TValue>> OrderByKVP<TKey, TValue, TOrderingKey>(this IEnumerable<KeyValuePair<TKey, TValue>> src, Func<TKey, TValue, TOrderingKey> keySelector) => src.OrderBy(kvp => keySelector(kvp.Key, kvp.Value));
        public static IEnumerable<TValue> Values<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> src) => src.SelectKVP((key, value) => value);
        public static IEnumerable<TKey> Keys<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> src) => src.SelectKVP((key, value) => key);

        public static ILookup<TKey, TValue> ToLookup<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> src) => src.ToLookup(x => x.Key, x => x.Value);

        public static bool AnyKVP<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> src, Func<TKey, TValue, bool> predicate) => src.WhereKVP(predicate).Any();

        public static TValue Get<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> src, TKey key) => src.WhereKVP((k, v) =>  key.Equals(k)).SingleOrDefault().Value;
    }
}
