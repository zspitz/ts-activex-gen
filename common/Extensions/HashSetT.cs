using System.Collections.Generic;
using System.Linq;

namespace TsActivexGen {
    public static class HashSetTExtensions {
        public static bool ContainsAny<T>(this HashSet<T> src, params T[] toFind)  => toFind.Any(x => src.Contains(x));
    }
}
