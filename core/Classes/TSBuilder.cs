using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TsActivexGen.Util;
using static TsActivexGen.TSParameterType;
using static TsActivexGen.Util.Functions;

namespace TsActivexGen {
    public class TSBuilder {
        private static string[] jsKeywords = new[] { "var" };

        private StringBuilder sb;

        private void WriteEnum(KeyValuePair<string, TSEnumDescription> x) {
            var name = NameOnly(x.Key);
            var @enum = x.Value;
            var members = @enum.Members.OrderBy(y => y.Key);

            //https://github.com/zspitz/ts-activex-gen/issues/25
            if (@enum.Typename.FullName == "number") {
                $"const enum {name} {{".AppendLineTo(sb, 1);
                members.AppendLinesTo(sb, (memberName, value) => $"{memberName} = {value}", 2, ",");
                "}".AppendWithNewSection(sb, 1);
            } else {
                $"type {name} = ".AppendLineTo(sb, 1);
                members.AppendLinesTo(sb, (memberName, value) => $"\"{value}\" //{memberName}", 2, null, "| ");
                sb.AppendLine();
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
                var parameters = memberDescription.Parameters.Select((kvp, index) => {
                    //this is an issue in ShDocVw
                    var parameterName = kvp.Key;
                    if (parameterName.In(jsKeywords)) { parameterName = $"{parameterName}_{index}"; } 
                    return KVP(parameterName, kvp.Value);
                }).ToList();
                parameterList = "(" + parameters.Joined(", ", y => GetParameter(y, ns)) + ") => ";
            }

            string @readonly = memberDescription.ReadOnly.GetValueOrDefault() ? "readonly " : "";

            $"{@readonly}{name}: {parameterList}{returnType};{comment}".AppendLineTo(sb, 2);
        }

        private void WriteInterface(KeyValuePair<string, TSInterfaceDescription> x, string ns) {
            var name = NameOnly(x.Key);
            var @interface = x.Value;
            $"interface {name} {{".AppendLineTo(sb, 1);
            @interface.Members.OrderBy(y => y.Key).ForEach(y => WriteMember(y, ns));
            "}".AppendWithNewSection(sb, 1);
        }

        private void WriteAlias(KeyValuePair<string, TSTypeName> x, string ns) {
            $"type {NameOnly(x.Key)} = {x.Value.RelativeName(ns)};".AppendWithNewSection(sb, 1);
        }

        public string GetTypescript(TSNamespace ns) {
            sb = new StringBuilder();

            $"declare namespace {ns.Name} {{".AppendWithNewSection(sb);

            if (ns.Aliases.Any()) {
                "//Type aliases".AppendLineTo(sb, 1);
                ns.Aliases.OrderBy(x => x.Key).ForEach(x => WriteAlias(x, ns.Name));
            }

            var numericEnums = ns.Enums.Where(x => x.Value.Typename.FullName == "number");
            if (numericEnums.Any()) {
                "//Numeric enums".AppendLineTo(sb, 1);
                numericEnums.OrderBy(x => x.Key).ForEach(WriteEnum);
            }

            var nonnumericEnums = ns.Enums.Where(x => x.Value.Typename.FullName != "number");
            if (nonnumericEnums.Any()) {
                "//Nonnumeric enums".AppendLineTo(sb, 1);
                numericEnums.OrderBy(x => x.Key).ForEach(WriteEnum);

                //TODO add these to runtime file https://github.com/zspitz/ts-activex-gen/issues/25
            }

            if (ns.Namespaces.Any() && Debugger.IsAttached) {
                //TODO add these to runtime file, not .d.ts -- https://github.com/zspitz/ts-activex-gen/issues/25
                //use the WriteNamespace method
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
                creatables.SelectKVP((interfaceName, @interface) => $"new (progID: '{interfaceName}'): {interfaceName};").AppendLinesTo(sb, 1);
                "}".AppendWithNewSection(sb);
            }

            var enumerables = ns.Interfaces.WhereKVP((name, interfaceDescription) => !interfaceDescription.EnumerableType.IsNullOrEmpty()).ToList();
            if (enumerables.Any()) {
                "interface EnumeratorConstructor {".AppendLineTo(sb);
                enumerables.SelectKVP((interfaceName, @interface) => {
                    return $"new (col: {interfaceName}): Enumerator<{@interface.EnumerableType}>;";
                }).AppendLinesTo(sb, 1);
                "}".AppendWithNewSection(sb);
            }
            //end

            return sb.ToString();
        }

        public List<KeyValuePair<string, string>> GetTypescript(TSNamespaceSet namespaceSet) => namespaceSet.Namespaces.SelectKVP((name, ns) => KVP(name, GetTypescript(ns))).ToList();
    }
}