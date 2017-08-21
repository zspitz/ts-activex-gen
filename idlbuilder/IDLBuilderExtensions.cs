using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsActivexGen.idlbuilder {
    public static class IDLBuilderExtensions {
        public static string DeJavaName(this string s) {
            var ret = s.Replace("::", ".");
            while (ret.StartsWith(".")) {
                ret = ret.Substring(1);
            }
            return ret.Trim();
        }
    }
}
