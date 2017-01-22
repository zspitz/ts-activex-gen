using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsActivexGen.Util;
using static TsActivexGen.TSParameterType;
using static TsActivexGen.Util.Functions;

namespace TsActivexGen {
    public class TSBuilder {
        private StringBuilder sb;

        [Obsolete("Depends on literal types -- https://github.com/Microsoft/TypeScript/pull/9407")]
        public bool WriteValueOnlyNamespaces { get; set; } = false;

        private void WriteEnum(KeyValuePair<string, TSEnumDescription> x) {
            var name = NameOnly(x.Key);
            var @enum = x.Value;
            var members = @enum.Members.OrderBy(y => y.Key);
            switch (@enum.Typename.FullName) {
                case "number":
                    $"const enum {name} {{".AppendLineTo(sb, 1);
                    members.AppendLinesTo(sb, (memberName, value) => $"{memberName} = {value}", 2, ",");
                    "}".AppendWithNewSection(sb, 1);
                    break;
                case "string":
                    //TODO this works for all values, not just for string
                    //TODO all types can be better represented using literal types, pending https://github.com/Microsoft/TypeScript/pull/9407
                    $"type {name} = ".AppendLineTo(sb, 1);
                    members.AppendLinesTo(sb, (memberName, value) => $"\"{value}\" //{memberName}", 2, null, "| ");
                    sb.AppendLine();
                    break;
                default:
                    throw new InvalidOperationException("Unable to emit declarations for enum type which is not numeric or string");
            }
        }

        private void WriteNamespace(KeyValuePair<string, TSNamespaceDescription> x) {
            var name = NameOnly(x.Key);
            var members = x.Value.Members.OrderBy(y => y.Key);
            $"namespace {name} {{".AppendLineTo(sb, 1);
            members.AppendLinesTo(sb, (memberName, value) => $"var {memberName}: {value};", 2);
            "}".AppendWithNewSection(sb, 1);
        }

        private string GetParameter(KeyValuePair<string, TSParameterDescription> x, string ns) {
            var name = x.Key;
            var parameterDescription = x.Value;
            if (parameterDescription.ParameterType == Rest) {
                name = "..." + name;
            } else if (parameterDescription.ParameterType == Optional) {
                name += "?";
            }
            return $"{name}: {parameterDescription.Typename.RelativeName(ns)}";
        }

        private void WriteMember(KeyValuePair<string, TSMemberDescription> x, string ns) {
            var name = x.Key;
            var memberDescription = x.Value;
            var returnType = memberDescription.ReturnTypename.RelativeName(ns);

            var comment = memberDescription.Comment;
            if (!comment.IsNullOrEmpty()) { comment = $"   //{comment}"; }

            string parameterList = "";
            if (memberDescription.Parameters != null) {
                parameterList = "(" + memberDescription.Parameters.Joined(", ", y => GetParameter(y, ns)) + ") => ";
            }

            string @readonly = "";
            //if (memberDescription.ReadOnly) {
            //    @readonly = "readonly ";
            //}

            $"{@readonly}{name}: {parameterList}{returnType};{comment}".AppendLineTo(sb, 2);
        }

        private void WriteInterface(KeyValuePair<string, TSInterfaceDescription> x, string ns) {
            var name = NameOnly(x.Key);
            var @interface = x.Value;
            $"interface {name} {{".AppendLineTo(sb, 1);
            @interface.Members.OrderBy(y => y.Key).ForEach(y=> WriteMember(y,ns));
            "}".AppendWithNewSection(sb, 1);
        }

        private void WriteAlias(KeyValuePair<string, TSTypeName> x, string ns) {
            $"type {NameOnly(x.Key)} = {x.Value.RelativeName(ns)};".AppendWithNewSection(sb, 1);
        }

        public string GetTypescript(TSNamespace ns, IEnumerable<string> headers) {
            //TODO handle multiple root declarations (namespace without a name)

            sb = new StringBuilder();

            if (headers != null) {
                headers.AppendLinesTo(sb);
                sb.AppendLine();
            }

            $"declare namespace {ns.Name} {{".AppendWithNewSection(sb);

            if (ns.Aliases.Any()) {
                "//Type aliases".AppendLineTo(sb, 1);
                ns.Aliases.OrderBy(x => x.Key).ForEach(x=>WriteAlias(x,ns.Name));
            }

            if (ns.Enums.Any()) {
                "//Enums".AppendLineTo(sb, 1);
                ns.Enums.OrderBy(x => x.Key).ForEach(WriteEnum);
            }

            if (WriteValueOnlyNamespaces && ns.Namespaces.Any()) {
                "//Modules with constant values".AppendLineTo(sb, 1);
                ns.Namespaces.OrderBy(x => x.Key).ForEach(WriteNamespace);
            }

            if (ns.Interfaces.Any()) {
                "//Interfaces".AppendLineTo(sb, 1);
                ns.Interfaces.OrderBy(x => x.Key).ForEach(x => WriteInterface(x, ns.Name));
            }

            "}".AppendWithNewSection(sb);

            //This functionality is specific to ActiveX definition creation
            var creatables = ns.Interfaces.WhereKVP((name, interfaceDescription) => interfaceDescription.IsActiveXCreateable).ToList();
            if (creatables.Any()) {
                "interface ActiveXObject {".AppendLineTo(sb);
                creatables.SelectKVP((interfaceName, @interface) => {
                    return $"new (progID: '{interfaceName}'): {interfaceName};";
                }).AppendLinesTo(sb, 1);
                "}".AppendWithNewSection(sb);
            }
            //end

            return sb.ToString();
        }
    }
}

//TODO use const keyword instead of readonly keyword?