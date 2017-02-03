﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TsActivexGen.Util {
    public static class IEnumerableExtensions {
        public static List<object> ToObjectList(this IEnumerable src) => src.Cast<object>().ToList();
    }
}
