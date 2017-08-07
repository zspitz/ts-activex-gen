using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsActivexGen.idlbuilder {
    public static class IDLBuilderExtensions {
        public static string DeJavaName(this string s) => s.Replace("::", ".");
    }
}
