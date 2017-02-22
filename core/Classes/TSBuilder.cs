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

        private string JsDocLine(KeyValuePair<string, string> entry) {
            var key = entry.Key;
            if (key != "") { key = $"@{key} "; }
            return $" {key}{entry.Value}";
        }

        private void WriteJsDoc(List<KeyValuePair<string, string>> JsDoc, int indentationLevel, bool newLine = false) {
            JsDoc = JsDoc.WhereKVP((key, value) => !key.IsNullOrEmpty() || !value.IsNullOrEmpty()).ToList();
            if (JsDoc.Count == 0) { return; }
            if (newLine) { "".AppendLineTo(sb, indentationLevel); }
            if (JsDoc.Count == 1) {
                $"/**{JsDocLine(JsDoc[0])} */".AppendLineTo(sb, indentationLevel);
            } else {
                "/**".AppendLineTo(sb, indentationLevel);
                JsDoc.OrderByKVP((key, value) => key).Select(x => "*" + JsDocLine(x)).AppendLinesTo(sb, indentationLevel);
                "*/".AppendLineTo(sb, indentationLevel);
            }
        }

        private void WriteEnum(KeyValuePair<string, TSEnumDescription> x) {
            var name = NameOnly(x.Key);
            var @enum = x.Value;
            var members = @enum.Members.OrderBy(y => y.Key);

            WriteJsDoc(@enum.JsDoc, 2);

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

            WriteJsDoc(x.Value.JsDoc, 2);

            $"namespace {name} {{".AppendLineTo(sb, 1);
            members.AppendLinesTo(sb, (memberName, value) => $"var {memberName}: {value};", 2);
            "}".AppendWithNewSection(sb, 1);
        }

        private string GetParameterString(KeyValuePair<string, TSParameterDescription> x, string ns) {
            var name = x.Key;
            var parameterDescription = x.Value;
            if (parameterDescription.ParameterType == Rest) {
                name = "..." + name;
            } else if (parameterDescription.ParameterType == Optional) {
                name += "?";
            }
            return $"{name}: {GetTypeString(parameterDescription.Type, ns)}";
        }

        private void WriteMemberBase(TSMemberDescription m, string ns, string memberIdentifier, int indentationLevel) {
            var returnType = GetTypeString(m.ReturnType, ns);

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
                parameterList = "(" + parameters.Joined(", ", y => GetParameterString(y, ns)) + ")";
            }

            WriteJsDoc(m.JsDoc, indentationLevel, true);

            $"{memberIdentifier}{parameterList}: {returnType}{comment}".AppendLineTo(sb, indentationLevel);
        }

        private void WriteMember(KeyValuePair<string, TSMemberDescription> x, string ns, int indentationLevel) {
            var memberDescription = x.Value;
            string @readonly = memberDescription.ReadOnly.GetValueOrDefault() ? "readonly " : "";
            WriteMemberBase(memberDescription, ns, $"{@readonly}{x.Key}", indentationLevel);
        }

        private void WriteConstructor(TSMemberDescription m, string ns, int indentationLevel) => WriteMemberBase(m, ns, "new", indentationLevel);

        private string GetTypeString(ITSType type, string ns) { //this is not in each individual class, because the only purpose is for emitting
            string ret = null;

            //go pattern matching!!!
            if (ExecIfType<TSSimpleType>(type, x => ret = RelativeName(x.GenericParameter ?? x.FullName, ns))) {
            } else if (ExecIfType<TSTupleType>(type, x => ret = $"[{x.Members.Joined(",", y => GetTypeString(y, ns))}]")) {
            } else if (ExecIfType<TSObjectType>(type, x => ret = $"{x.Members.JoinedKVP((key, val) => $"{key}: {GetTypeString(val, ns)}")}")) {
            } else if (ExecIfType<TSFunctionType>(type, x => ret = $"({x.FunctionDescription.Parameters.Select(y => GetParameterString(y, ns))}: {GetTypeString(x.FunctionDescription.ReturnType, ns)}")) {
            } else {
                throw new NotImplementedException();
            }
            return ret;
        }

        /// <summary>Provides a simple way to order members by the set of parameters</summary>
        private string ParametersString(TSMemberDescription m) => m.Parameters?.JoinedKVP((name, prm) => $"{name}: {GetTypeString(prm.Type, "")}");

        private void WriteInterface(KeyValuePair<string, TSInterfaceDescription> x, string ns, int indentationLevel) {
            var name = NameOnly(x.Key);
            var @interface = x.Value;
            WriteJsDoc(@interface.JsDoc, indentationLevel);
            $"interface {name} {{".AppendLineTo(sb, indentationLevel);
            @interface.Members.OrderBy(y => y.Key).ThenBy(y => ParametersString(y.Value)).ForEach(y => WriteMember(y, ns, indentationLevel + 1));
            @interface.Constructors.OrderBy(ParametersString).ForEach(y => WriteConstructor(y, ns, indentationLevel + 1));
            "}".AppendWithNewSection(sb, indentationLevel);
        }

        private void WriteAlias(KeyValuePair<string, TSSimpleType> x, string ns) {
            $"type {NameOnly(x.Key)} = {GetTypeString(x.Value, ns)};".AppendWithNewSection(sb, 1);
        }

        public NamespaceOutput GetTypescript(TSNamespace ns) {
            sb = new StringBuilder();

            WriteJsDoc(ns.JsDoc, 0);
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

                //TODO add values to runtime file https://github.com/zspitz/ts-activex-gen/issues/25
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

            var ret = new NamespaceOutput() {
                MainFile = sb.ToString(),
                Description = ns.Description
            };

            //TODO build constants file here

            return ret;
        }

        public List<KeyValuePair<string, NamespaceOutput>> GetTypescript(TSNamespaceSet namespaceSet) => namespaceSet.Namespaces.SelectKVP((name, ns) => KVP(name, GetTypescript(ns))).ToList();
    }
}