using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsActivexGen.Util;
using static TsActivexGen.TSParameterType;

namespace TsActivexGen {
    public class TSBuilder {
        private StringBuilder sb;

        private void WriteEnum(KeyValuePair<string, TSEnumDescription> x) {
            var name = x.Key;
            var @enum = x.Value;
            var members = @enum.Members.OrderBy(y=>y.Key);
            switch (@enum.Typename.Name) {
                case "number":
                    $"const enum {name} {{".AppendLineTo(sb, 1);
                    members.AppendLinesTo(sb, (memberName, value) => $"{memberName} = {value}", 2, ", ");
                    "}".AppendWithNewSection(sb, 1);
                    break;
                case "string":
                    $"type {name} = ".AppendLineTo(sb, 1);
                    members.AppendLinesTo(sb, (memberName, value) => $"\"{value}\" //{memberName}", 2, null, "| ");

                    $"const {name}: {{".AppendLineTo(sb, 1);
                    members.AppendLinesTo(sb, (memberName, value) => $"{memberName}: {name}", 2, ", ");
                    "}".AppendWithNewSection(sb, 1);

                    break;
                default:
                    throw new InvalidOperationException("Unable to emit declarations for enum type which is not numeric or string");
            }
        }

        private string GetParameter(KeyValuePair<string, TSParameterDescription> x) {
            var name = x.Key;
            var parameterDescription = x.Value;
            if (parameterDescription.ParameterType == Rest) {
                name = "..." + name;
            } else if (parameterDescription.ParameterType == Optional) {
                name += "?";
            }
            return $"{name}: {parameterDescription.Typename}";
        }

        private void WriteMember(KeyValuePair<string, TSMemberDescription> x) {
            var name = x.Key;
            var memberDescription = x.Value;
            var returnType = memberDescription.ReturnTypename;

            var comment = memberDescription.Comment;
            if (!comment.IsNullOrEmpty()) { comment = $"   //{comment}"; }

            string parameterList = "";
            if (memberDescription.Parameters != null) {
                parameterList = "(" + memberDescription.Parameters.Joined(", ", GetParameter) + ") => ";
            }

            $"{name}: {parameterList}{returnType};{comment}".AppendLineTo(sb, 2);
        }

        private void WriteInterface(KeyValuePair<string, TSInterfaceDescription> x) {
            var name = x.Key;
            var @interface = x.Value;
            $"interface {name} {{".AppendLineTo(sb, 1);
            @interface.Members.OrderBy(y => y.Key).ForEach(WriteMember);
            "}".AppendWithNewSection(sb, 1);
        }

        public string GetTypescript(TSNamespace ns, IEnumerable<string> headers) {
            //TODO handle multiple root declarations (namespace without a name)

            sb = new StringBuilder();

            if (headers != null) {
                headers.AppendLinesTo(sb);
                sb.AppendLine();
            }

            $"declare namespace {ns.Name} {{".AppendWithNewSection(sb);

            if (ns.Enums.Any()) {
                "//Enums".AppendLineTo(sb, 1);
                ns.Enums.OrderBy(x => x.Key).ForEach(WriteEnum);
            }

            if (ns.Interfaces.Any()) {
                "//Interfaces".AppendLineTo(sb, 1);
                ns.Interfaces.OrderBy(x => x.Key).ForEach(WriteInterface);
            }

            "}".AppendWithNewSection(sb);

            //This functionality is specific to ActiveX definition creation
            var creatables = ns.Interfaces.WhereKVP((name, interfaceDescription) => interfaceDescription.IsActiveXCreateable).ToList();
            if (creatables.Any()) {
                "interface ActiveXObject {".AppendLineTo(sb);
                creatables.SelectKVP((interfaceName, @interface) => {
                    var progid = $"{ns.Name}.{interfaceName}";
                    return $"new (progID: '{progid}'): {progid};";
                }).AppendLinesTo(sb, 1);
                "}".AppendWithNewSection(sb);
            }
            //end

            return sb.ToString();
        }
    }
}
