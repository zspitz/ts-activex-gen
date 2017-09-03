using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsActivexGen {
    public static partial class ICollectionTExtensions {
        public static void Add<T1, T2>(this ICollection<(T1, T2)> collection, T1 item1, T2 item2) => collection.Add((item1, item2));
        public static void AddTo<T>(this T item, ICollection<T> collection) => collection.Add(item);

        public static void AddRange<T>(this ICollection<T> dest, IEnumerable<T> toAdd) => toAdd.ForEach(x => dest.Add(x));
    }
}
