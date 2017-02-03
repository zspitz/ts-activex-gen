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
    }
}
