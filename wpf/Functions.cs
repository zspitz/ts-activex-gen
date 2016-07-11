using static System.Reflection.Assembly;
using static System.IO.Path;
using static System.StringComparison;

namespace TsActivexGen.Wpf {
    public static class Functions {
        public static string ResolvePath(string relativePathFromEntry) {
            var entryPath = GetDirectoryName(GetEntryAssembly().Location);
            var indexOfBin = entryPath.IndexOf("\\ts-activex-gen\\wpf\\bin\\", OrdinalIgnoreCase);
            if (indexOfBin != -1) {
                entryPath = Combine(entryPath.Substring(0, indexOfBin), "ts-activex-gen\\wpf");
            }
            return GetFullPath(Combine(entryPath, relativePathFromEntry));
        }
    }
}
