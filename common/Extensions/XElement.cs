using System.Linq;
using System.Xml.Linq;

namespace TsActivexGen {
    public static class XElementExtensions {
        public static string LocalName(this XElement elem) => elem.Name.LocalName;
        public static string LocalName(this XAttribute attr) => attr.Name.LocalName;

        public static bool AnyChildNot(this XElement elem, params XName[] names) => elem.Nodes().OfType<XText>().Any() || elem.Elements().Any(x => names.All(name => x.Name != name));
    }
}
