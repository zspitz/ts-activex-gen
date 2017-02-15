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

        private void WriteMemberBase(TSMemberDescription m, string ns, string memberIdentifier, string returnPrefix, int indentationLevel) {
            var returnType = m.ReturnTypename.RelativeName(ns);

            var comment = m.Comment;
            if (!comment.IsNullOrEmpty()) { comment = $"   //{comment}"; }

            string parameterList = "";
            if (m.Parameters != null) {
                var parameters = m.Parameters.Select((kvp, index) => {
                    //ShDocVw has a Javascript keyword as one of the parameters
                    var parameterName = kvp.Key;
                    if (parameterName.In(jsKeywords)) { parameterName = $"{parameterName}_{index}"; }
                    return KVP(parameterName, kvp.Value);
                }).ToList();
                parameterList = "(" + parameters.Joined(", ", y => GetParameter(y, ns)) + ")" + returnPrefix + " ";
            }

            $"{memberIdentifier}{parameterList}{returnType};{comment}".AppendLineTo(sb, indentationLevel);
        }

        private void WriteMember(KeyValuePair<string, TSMemberDescription> x, string ns, int indentationLevel) {
            var memberDescription = x.Value;
            string @readonly = memberDescription.ReadOnly.GetValueOrDefault() ? "readonly " : "";
            WriteMemberBase(memberDescription, ns, $"{@readonly}{x.Key}: ", " =>", indentationLevel);
        }

        private void WriteConstructor(TSMemberDescription m, string ns, int indentationLevel) {
            WriteMemberBase(m, ns, "new ", ":", indentationLevel);
        }

        private string ParametersString(TSMemberDescription m) => m.Parameters?.JoinedKVP((name, prm) => $"{name}: {prm.Typename.FullName}");

        private void WriteInterface(KeyValuePair<string, TSInterfaceDescription> x, string ns, int indentationLevel) {
            var name = NameOnly(x.Key);
            var @interface = x.Value;
            $"interface {name} {{".AppendLineTo(sb, indentationLevel);
            //TODO sort members and constructors by parameter lists
            @interface.Members.OrderBy(y => y.Key).ThenBy(y=>ParametersString(y.Value)).ForEach(y => WriteMember(y, ns, indentationLevel + 1));
            @interface.Constructors.OrderBy(ParametersString).ForEach(y => WriteConstructor(y, ns, indentationLevel + 1));
            "}".AppendWithNewSection(sb, indentationLevel);
        }

        private void WriteAlias(KeyValuePair<string, TSSimpleType> x, string ns) {
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
                ns.Interfaces.OrderBy(x => x.Key).ForEach(x => WriteInterface(x, ns.Name, 1));
            }

            "}".AppendWithNewSection(sb);

            if (ns.GlobalInterfaces.Any()) {
                "//Global interfaces".AppendLineTo(sb, 0);
                ns.GlobalInterfaces.OrderBy(x => x.Key).ForEach(x => WriteInterface(x, "", 0));
            }

            return sb.ToString();
        }

        public List<KeyValuePair<string, string>> GetTypescript(TSNamespaceSet namespaceSet) => namespaceSet.Namespaces.SelectKVP((name, ns) => KVP(name, GetTypescript(ns))).ToList();
    }
}