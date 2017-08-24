using TsActivexGen.idlbuilder;
using static TsActivexGen.idlbuilder.Context;

namespace tests {
    class Program {
        static void Main(string[] args) {
            var builder = new DoxygenIDLBuilder(@"D:\Zev\Projects\IDLParser_Doxygen\output\xml", Automation);
            builder.Generate();
        }
    }
}
