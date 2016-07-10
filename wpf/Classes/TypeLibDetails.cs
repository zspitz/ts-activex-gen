using Microsoft.Win32;
using System.Linq;
using System.Text.RegularExpressions;
using MoreLinq;
using TsActivexGen.Util;

namespace TsActivexGen.wpf {
    public class TypeLibDetails {
        public string TypeLibID { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public short MajorVersion { get; set; }
        public short MinorVersion { get; set; }
        public short LCID { get; set; }
        public bool Is32bit { get; set; }
        public bool Is64bit { get; set; }
        public string RegistryKey { get; set; }
    }
}
