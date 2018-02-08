using System;
using MoreLinq;
using System.Collections.Generic;
using System.Linq;

namespace TsActivexGen {
    public static class IEnumerableTExtensions {
        public static bool None<T>(this IEnumerable<T> src, Func<T, bool> predicate = null) {
            if (predicate==null) { return !src.Any(); }
            return !src.Any(predicate);
        }

        public static string Joined<T>(this IEnumerable<T> source, string delimiter = ",", Func<T, string> selector = null) {
            if (source == null) { return ""; }
            if (selector == null) { return string.Join(delimiter, source); }
            return string.Join(delimiter, source.Select(selector));
        }
        public static string Joined<T>(this IEnumerable<T> source, string delimiter, Func<T, int, string> selector) {
            if (source == null) { return ""; }
            if (selector == null) { return string.Join(delimiter, source); }
            return string.Join(delimiter, source.Select(selector));
        }

        public static void AddRangeTo<T>(this IEnumerable<T> src, ICollection<T> dest) => dest.AddRange(src);

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> src) => new HashSet<T>(src);

        public static IEnumerable<T> Ordered<T>(this IEnumerable<T> src) => src.OrderBy(x => x);
        public static IEnumerable<T> OrderedDescending<T>(this IEnumerable<T> src) => src.OrderByDescending(x => x);

        public static IEnumerable<T> SelectMany<T>(this IEnumerable<IEnumerable<T>> src) => src.SelectMany(x => x);

        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> src, Action<T> action) {
            foreach (var item in src) {
                action(item);
            }
            return src;
        }
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> src, Action<T, int> action) {
            var current = 0;
            foreach (var item in src) {
                action(item, current);
                current += 1;
            }
            return src;
        }

        public static IEnumerable<T> DefaultIfNull<T>(this IEnumerable<T> src) => src ?? Enumerable.Empty<T>();

        /// <summary>The selector takes a boolean, indicating the last value</summary>
        public static IEnumerable<TResult> Select<T, TResult>(this IEnumerable<T> src, Func<T, int, bool, TResult> selector) {
            int counter = 0;
            T previous = default(T);
            foreach (var x in src) {
                if (counter > 0) {
                    yield return selector(previous, counter - 1, false);
                }
                counter += 1;
                previous = x;
            }
            if (counter > 0) { yield return selector(previous, counter - 1, true); }
        }

        public static IEnumerable<TResult> Zip<TFirst, TSecond, TResult>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second, Func<TFirst, TSecond, int, TResult> resultSelector) {
            var counter = 0;
            return Enumerable.Zip(first,second, (x, y) => {
                var ret = resultSelector(x, y, counter);
                counter += 1;
                return ret;
            });
        }

        public static int IndexOf<T>(this IEnumerable<T> src, Func<T, bool> predicate) => src.Select((x, i) => new { result = predicate(x), i }).FirstOrDefault(x => x.result)?.i ?? -1;

        public static void InsertRangeTo<T>(this IEnumerable<T> src, int index, List<T> destination) => destination.InsertRange(index, src);

        public static long Product<T>(this IEnumerable<T> src, Func<T, long> selector) {
            unchecked {
                return src.Aggregate((long)1, (prev, x) => prev * selector(x));
            };
        }

        public static IEnumerable<(T1, T2)> Zip<T1, T2>(this IEnumerable<T1> first, IEnumerable<T2> second) => first.Zip(second, (a, b) => (a, b));

        public static bool All<T1, T2>(this IEnumerable<(T1, T2)> src, Func<T1, T2, bool> predicate) => src.All(x => predicate(x.Item1, x.Item2));

        public static T OnlyOrDefault<T>(this IEnumerable<T> src, Func<T, bool> predicate=null) {
            if (predicate != null) { src = src.Where(predicate); }
            var firstTwo = src.Take(2).ToList();
            if (firstTwo.Count == 1) { return firstTwo[0]; }
            return default(T);
        }
    }
}
