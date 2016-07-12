using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsActivexGen.Util {
    public static class HashSetTExtensions {
        public static bool ContainsAny<T>(this HashSet<T> src, params T[] toFind) {
            return toFind.Any(x => src.Contains(x));
        }
    }
}
