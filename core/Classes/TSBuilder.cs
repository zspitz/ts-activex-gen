using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsActivexGen.Util;
using static TsActivexGen.Util.Functions;
using static System.Environment;
using System.Text.RegularExpressions;

namespace TsActivexGen {
    public class TSBuilder {
        private static string[] jsKeywords = new[] { "var" };

        private StringBuilder sb;

        private string jsDocLine(KeyValuePair<string, string> entry) {
            var key = entry.Key;
            if (key != "") { key = $"@{key} "; }
            return $" {key}{entry.Value}";
        }

        private Regex spaceBreaker = new Regex(@".{0,150}(?:\S|$)");
        private void writeJsDoc(List<KeyValuePair<string, string>> JsDoc, int indentationLevel, bool newLine = false) {
            JsDoc = JsDoc.WhereKVP((key, value) => !key.IsNullOrEmpty() || !value.IsNullOrEmpty()).SelectMany(kvp => {
                if (kvp.Value.Length <= 150) { return new[] { kvp }; }
                var lines = new List<KeyValuePair<string, string>>();
                if (!kvp.Key.IsNullOrEmpty()) { throw new Exception("Unhandled long line in JSDoc parameter defaults"); }
                var matches = spaceBreaker.Matches(kvp.Value);
                if (matches.Count == 0) { throw new Exception("Unhandled long line in JSDoc"); }
                foreach (Match match in matches) {
                    if (match.Length == 0) { continue; }
                    lines.Add("", match.Value);
                }
                return lines.ToArray();
            }).ToList();
            if (JsDoc.Count == 0) { return; }
            if (newLine) { sb.AppendLine(); }
            if (JsDoc.Count == 1) {
                $"/**{jsDocLine(JsDoc[0])} */".AppendLineTo(sb, indentationLevel);
            } else {
                "/**".AppendLineTo(sb, indentationLevel);
                JsDoc.OrderByKVP((key, value) => key).Select(x => " *" + jsDocLine(x)).AppendLinesTo(sb, indentationLevel);
                " */".AppendLineTo(sb, indentationLevel);
            }
        }

        //https://github.com/zspitz/ts-activex-gen/issues/25#issue-204161318
        private void writeEnumDeclaration(KeyValuePair<string, TSEnumDescription> x) {
            var name = NameOnly(x.Key);
            var @enum = x.Value;
            var members = @enum.Members.OrderBy(y => y.Key);

            writeJsDoc(@enum.JsDoc, 1);

            $"const enum {name} {{".AppendLineTo(sb, 1);
            members.AppendLinesTo(sb, (memberName, value) => $"{memberName} = {value}", 2, ",");
            "}".AppendWithNewSection(sb, 1);
        }

        private void writeMemberBase(TSMemberDescription m, string ns, string memberIdentifier, int indentationLevel) {
            var returnType = GetTypeString(m.ReturnType, ns);

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

            writeJsDoc(m.JsDoc, indentationLevel, true);

            $"{memberIdentifier}{parameterList}: {returnType};".AppendLineTo(sb, indentationLevel);
        }

        private void WriteMember(KeyValuePair<string, TSMemberDescription> x, string ns, int indentationLevel) {
            var memberDescription = x.Value;
            string @readonly = memberDescription.ReadOnly.GetValueOrDefault() ? "readonly " : "";
            writeMemberBase(memberDescription, ns, $"{@readonly}{x.Key}", indentationLevel);
        }

        private void WriteConstructor(TSMemberDescription m, string ns, int indentationLevel) => writeMemberBase(m, ns, "new", indentationLevel);

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
            $"declare namespace {ns.Name} {{".AppendLineTo(sb);

            ns.Aliases.OrderBy(x => x.Key).ForEach(x => WriteAlias(x, ns.Name));

            ns.Enums.OrderBy(x => x.Key).ForEach(writeEnumDeclaration);

            ns.Interfaces.OrderBy(x => x.Key).ForEach(x => WriteInterface(x, ns.Name, 1));

            "}".AppendWithNewSection(sb);

            ns.GlobalInterfaces.OrderBy(x => x.Key).ForEach(x => WriteInterface(x, "", 0));

            var mainFile = sb.ToString()
                .Replace("{" + NewLine + NewLine, "{" + NewLine) //writeJsdoc inserts a blank line before the jsdoc; if the member is the first after an opening brace, tslint doesn't like it
                .Replace("}" + NewLine + NewLine + "}", "}" + NewLine + "}") //removes the blank line after the last interface in the namespace
                .Trim() + NewLine;

            var ret = new NamespaceOutput() {
                MainFile = mainFile,
                Description = ns.Description,
                MajorVersion=ns.MajorVersion,
                MinorVersion=ns.MinorVersion,
                Dependencies = ns.Dependencies
            };

            //Build the tests file
            ns.GlobalInterfaces.IfContainsKey("ActiveXObject", x => {
                ret.TestsFile = x.Constructors.Joined(NewLine + NewLine, (y, index) => $"let obj{index} = new ActiveXObject({GetTypeString(y.Parameters[0].Value.Type, "")});") + NewLine;
            });

            return ret;
        }

        public List<KeyValuePair<string, NamespaceOutput>> GetTypescript(TSNamespaceSet namespaceSet) => namespaceSet.Namespaces.SelectKVP((name, ns) => KVP(name, GetTypescript(ns))).ToList();
    }
}