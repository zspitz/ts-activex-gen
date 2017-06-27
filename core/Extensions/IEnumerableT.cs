using System;
using MoreLinq;
using System.Collections.Generic;
using System.Linq;

namespace TsActivexGen.Util {
    public static class IEnumerableTExtensions {
        public static bool None<T>(this IEnumerable<T> src) {
            return !src.Any();
        }

        public static string Joined<T>(this IEnumerable<T> source, string delimiter = ",", Func<T, string> selector = null) {
            if (source == null) { return ""; }
            if (selector == null) { return string.Join(delimiter, source); }
            return string.Join(delimiter, source.Select(selector));
        }
        public static string Joined<T>(this IEnumerable<T> source, string delimiter , Func<T, int, string> selector ) {
            if (source == null) { return ""; }
            if (selector == null) { return string.Join(delimiter, source); }
            return string.Join(delimiter, source.Select(selector));
        }

        public static void AddRangeTo<T>(this IEnumerable<T> src, IList<T> lst) {
            lst.AddRange(src);
        }
        public static void AddRangeTo<T>(this IEnumerable<T> src, ICollection<T> dest) {
            dest.AddRange(src);
        }
        public static void AddRange<T>(this IList<T> lst, IEnumerable<T> toAdd) {
            toAdd.ForEach(x => lst.Add(x));
        }
        public static void AddRange<T>(this ICollection<T> dest, IEnumerable<T> toAdd) {
            toAdd.ForEach(x => dest.Add(x));
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> src) {
            return new HashSet<T>(src);
        }
        public static IEnumerable<T> Ordered<T>(this IEnumerable<T> src) {
            return src.OrderBy(x => x);
        }
        public static IEnumerable<T> OrderedDescending<T>(this IEnumerable<T> src) {
            return src.OrderByDescending(x => x);
        }
        public static IEnumerable<T> SelectMany<T>(this IEnumerable<IEnumerable<T>> src) {
            return src.SelectMany(x => x);
        }
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> src, Action<T> action) {
            foreach (var item in src) {
                action(item);
            }
            return src;
        }
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> src, Action<T,int> action) {
            var current = 0;
            foreach (var item in src) {
                action(item, current);
                current += 1;
            }
            return src;
        }
        public static IEnumerable<T> DefaultIfNull<T>(this IEnumerable<T> src) => src ?? Enumerable.Empty<T>();

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
    }
}
