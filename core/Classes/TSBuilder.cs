﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsActivexGen.Util;
using static TsActivexGen.TSParameterType;
using static TsActivexGen.Util.Functions;

namespace TsActivexGen {
    public class TSBuilder {
        private static string[] jsKeywords = new[] { "var" };

        private StringBuilder sb;

        private string jsDocLine(KeyValuePair<string, string> entry) {
            var key = entry.Key;
            if (key != "") { key = $"@{key} "; }
            return $" {key}{entry.Value}";
        }

        private void writeJsDoc(List<KeyValuePair<string, string>> JsDoc, int indentationLevel, bool newLine = false) {
            JsDoc = JsDoc.WhereKVP((key, value) => !key.IsNullOrEmpty() || !value.IsNullOrEmpty()).ToList();
            if (JsDoc.Count == 0) { return; }
            if (newLine) { "".AppendLineTo(sb, indentationLevel); }
            if (JsDoc.Count == 1) {
                $"/**{jsDocLine(JsDoc[0])} */".AppendLineTo(sb, indentationLevel);
            } else {
                "/**".AppendLineTo(sb, indentationLevel);
                JsDoc.OrderByKVP((key, value) => key).Select(x => "*" + jsDocLine(x)).AppendLinesTo(sb, indentationLevel);
                "*/".AppendLineTo(sb, indentationLevel);
            }
        }

        //https://github.com/zspitz/ts-activex-gen/issues/25#issue-204161318
        private void writeEnumDeclaration(KeyValuePair<string, TSEnumDescription> x) {
            var name = NameOnly(x.Key);
            var @enum = x.Value;
            var members = @enum.Members.OrderBy(y => y.Key);

            writeJsDoc(@enum.JsDoc, 2);

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

        private void writeNamespace(string name, Dictionary<string, string> members) {
            name = NameOnly(name);
            var orderedMembers = members.OrderBy(y => y.Key);

            $"export namespace {name} {{".AppendLineTo(sb, 1);
            orderedMembers.AppendLinesTo(sb, (memberName, value) => $"export const {memberName} = {value};", 2);
            "}".AppendWithNewSection(sb, 1);
        }

        private void writeEnumValues(KeyValuePair<string, TSEnumDescription> x) => writeNamespace(x.Key, x.Value.Members);

        private void writeNamespace(KeyValuePair<string, TSNamespaceDescription> x) => writeNamespace(x.Key, x.Value.Members);

        private string getParameterString(KeyValuePair<string, TSParameterDescription> x, string ns) {
            var name = x.Key;
            var parameterDescription = x.Value;
            if (parameterDescription.ParameterType == Rest) {
                name = "..." + name;
            } else if (parameterDescription.ParameterType == Optional) {
                name += "?";
            }
            return $"{name}: {GetTypeString(parameterDescription.Type, ns)}";
        }

        private void writeMemberBase(TSMemberDescription m, string ns, string memberIdentifier, int indentationLevel) {
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
                parameterList = "(" + parameters.Joined(", ", y => getParameterString(y, ns)) + ")";
            }

            writeJsDoc(m.JsDoc, indentationLevel, true);

            $"{memberIdentifier}{parameterList}: {returnType};{comment}".AppendLineTo(sb, indentationLevel);
        }

        private void WriteMember(KeyValuePair<string, TSMemberDescription> x, string ns, int indentationLevel) {
            var memberDescription = x.Value;
            string @readonly = memberDescription.ReadOnly.GetValueOrDefault() ? "readonly " : "";
            writeMemberBase(memberDescription, ns, $"{@readonly}{x.Key}", indentationLevel);
        }

        private void WriteConstructor(TSMemberDescription m, string ns, int indentationLevel) => writeMemberBase(m, ns, "new", indentationLevel);

        private string GetTypeString(ITSType type, string ns) { //this is not in each individual class, because the only purpose is for emitting
            string ret = null;

            switch (type) {
                case TSSimpleType x:
                    ret = RelativeName(x.GenericParameter ?? x.FullName, ns);
                    break;
                case TSTupleType x:
                    ret = $"[{x.Members.Joined(",", y => GetTypeString(y, ns))}]";
                    break;
                case TSObjectType x:
                    ret = $"{{{x.Members.JoinedKVP((key, val) => $"{key}: {GetTypeString(val, ns)}")}}}";
                    break;
                case TSFunctionType x:
                    ret = $"({x.FunctionDescription.Parameters.Joined(", ", y => getParameterString(y, ns))}) => {GetTypeString(x.FunctionDescription.ReturnType, ns)}";
                    break;
                default:
                    throw new NotImplementedException();
            }

            return ret;
        }

        /// <summary>Provides a simple way to order members by the set of parameters</summary>
        private string ParametersString(TSMemberDescription m) => m.Parameters?.JoinedKVP((name, prm) => $"{name}: {GetTypeString(prm.Type, "")}");

        private void WriteInterface(KeyValuePair<string, TSInterfaceDescription> x, string ns, int indentationLevel) {
            var name = NameOnly(x.Key);
            var @interface = x.Value;
            writeJsDoc(@interface.JsDoc, indentationLevel);
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

            writeJsDoc(ns.JsDoc, 0);
            $"declare namespace {ns.Name} {{".AppendWithNewSection(sb);

            if (ns.Aliases.Any()) {
                "//Type aliases".AppendLineTo(sb, 1);
                ns.Aliases.OrderBy(x => x.Key).ForEach(x => WriteAlias(x, ns.Name));
            }

            var numericEnums = ns.Enums.Where(x => x.Value.Typename.FullName == "number");
            if (numericEnums.Any()) {
                "//Numeric enums".AppendLineTo(sb, 1);
                numericEnums.OrderBy(x => x.Key).ForEach(writeEnumDeclaration);
            }

            var nonnumericEnums = ns.Enums.Where(x => x.Value.Typename.FullName != "number");
            if (nonnumericEnums.Any()) {
                "//Nonnumeric enums".AppendLineTo(sb, 1);
                numericEnums.OrderBy(x => x.Key).ForEach(writeEnumDeclaration);
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
                Description = ns.Description,
                Dependencies = ns.Dependencies
            };


            //Build the runtime constants file

            sb = new StringBuilder();
            if (nonnumericEnums.Any() || ns.Namespaces.Any()) {
                $"namespace {ns.Name} {{".AppendWithNewSection(sb);

                if (nonnumericEnums.Any()) {
                    "//Non-numeric enums".AppendLineTo(sb, 1);
                    nonnumericEnums.OrderBy(x => x.Key).ForEach(writeEnumValues);
                }

                if (ns.Namespaces.Any()) {
                    "//Modules".AppendLineTo(sb, 1);
                    ns.Namespaces.OrderBy(x => x.Key).ForEach(writeNamespace);
                }

                "}".AppendWithNewSection(sb);
            }
            ret.RuntimeFile = sb.ToString();

            //Build the tests file
            ns.GlobalInterfaces.IfContainsKey("ActiveXObject", x => {
                ret.TestsFile = x.Constructors.Joined(Environment.NewLine + Environment.NewLine, (y, index) => $"let obj{index} = new ActiveXObject({GetTypeString(y.Parameters[0].Value.Type, "")})");
            });

            return ret;
        }

        public List<KeyValuePair<string, NamespaceOutput>> GetTypescript(TSNamespaceSet namespaceSet) => namespaceSet.Namespaces.SelectKVP((name, ns) => KVP(name, GetTypescript(ns))).ToList();
    }
}