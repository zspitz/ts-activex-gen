using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Linq;

namespace TsActivexGen.tlibuilder {
    public static class Functions {
        public static string GetProgIDFromCLSID(string clsid) {
            string ret;
            using (var key = Registry.ClassesRoot.OpenSubKey($"CLSID\\{clsid}")) {
                using (var subkey = key?.OpenSubKey("VersionIndependentProgID") ?? key?.OpenSubKey("ProgID")) {
                    ret = (string)subkey?.GetValue("");
                }
            }

            if (ret != null) {
                var parts = ret.Split('.').ToList();
                for (int i = parts.Count - 1; i >= 1; i--) {
                    if (!Regex.IsMatch(parts[i], @"\d+")) { break; }
                    parts.RemoveAt(i);
                }
                ret = parts.Joined(".");
            }

            return ret;
        }
    }
}
