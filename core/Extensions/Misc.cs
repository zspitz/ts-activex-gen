using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsActivexGen.Util {
    public static class MiscExtensions {
        public static bool In<T>(this T val, IEnumerable<T> vals) {
            return vals.Contains(val);
        }
        public static bool In<T>(this T val, params T[] vals) {
            return vals.Contains(val);
        }
        public static bool NotIn<T>(this T val, IEnumerable<T> vals) {
            return !vals.Contains(val);
        }
        public static bool NotIn<T>(this T val, params T[] vals) {
            return !vals.Contains(val);
        }

    }
}
