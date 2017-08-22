using Microsoft.Win32;

namespace TsActivexGen.tlibuilder {
    public static class Functions {
        public static string GetProgIDFromCLSID(string clsid) {
            using (var key = Registry.ClassesRoot.OpenSubKey($"CLSID\\{clsid}")) {
                using (var subkey = key?.OpenSubKey("VersionIndependentProgID") ?? key?.OpenSubKey("ProgID")) {
                    return (string)subkey?.GetValue("");
                }
            }
        }
    }
}
