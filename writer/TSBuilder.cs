using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static System.Environment;
using System.Text.RegularExpressions;
using static TsActivexGen.Functions;

namespace TsActivexGen {
    public class TSBuilder {
        private static string[] jsKeywords = new[] { "var" };

        private StringBuilder sb;

        private string jsDocLine(KeyValuePair<string, string> entry) {
            var key = entry.Key;
            if (key != "") { key = $"@{key} "; }
            return $" {key}{entry.Value}";
        }

        private Regex spaceBreaker = new Regex(@".{0,150}(?:\s|$)");
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

        private void writeEnum(KeyValuePair<string, TSEnumDescription> x, int indentationLevel) {
            var name = SplitName(x.Key).name;
            var @enum = x.Value;
            var members = @enum.Members.OrderBy(y => y.Key);

            writeJsDoc(@enum.JsDoc, 1);

            $"const enum {name} {{".AppendLineTo(sb, indentationLevel);
            members.AppendLinesTo(sb, (memberName, value) => $"{memberName} = {value}", indentationLevel + 1, ",");
            "}".AppendWithNewSection(sb, indentationLevel);
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

        private void writeMember(KeyValuePair<string, TSMemberDescription> x, string ns, int indentationLevel) {
            var memberDescription = x.Value;
            string @readonly = memberDescription.ReadOnly.GetValueOrDefault() ? "readonly " : "";
            writeMemberBase(memberDescription, ns, $"{@readonly}{x.Key}", indentationLevel);
        }

        private void writeConstructor(TSMemberDescription m, string ns, int indentationLevel) => writeMemberBase(m, ns, "new", indentationLevel);

        /// <summary>Provides a simple way to order members by the set of parameters</summary>
        private string parametersString(TSMemberDescription m) => m.Parameters?.JoinedKVP((name, prm) => $"{name}: {GetTypeString(prm.Type, "")}");

        private void writeInterface(KeyValuePair<string, TSInterfaceDescription> x, string ns, int indentationLevel) {
            var name = SplitName(x.Key).name;
            var @interface = x.Value;
            writeJsDoc(@interface.JsDoc, indentationLevel);

            var extends = "";
            if (@interface.Extends.Any()) { extends = " extends " + @interface.Extends.Joined(", "); }
            $"interface {name} {extends}{{".AppendLineTo(sb, indentationLevel);
            @interface.Members.OrderBy(y => y.Key).ThenBy(y => parametersString(y.Value)).ForEach(y => writeMember(y, ns, indentationLevel + 1));
            @interface.Constructors.OrderBy(parametersString).ForEach(y => writeConstructor(y, ns, indentationLevel + 1));
            "}".AppendWithNewSection(sb, indentationLevel);
        }

        private void writeAlias(KeyValuePair<string, TSAliasDescription> x, string ns, int indentationLevel) {
            writeJsDoc(x.Value.JsDoc, indentationLevel);
            $"type {SplitName(x.Key).name} = {GetTypeString(x.Value.TargetType, ns)};".AppendWithNewSection(sb, indentationLevel);
        }

        private void writeNominalType(TSSimpleType x) {
            string classDeclaration = x;
            if (x=="SafeArray<T>") { classDeclaration = "SafeArray<T=any>"; } // HACK we have no generic type parsing, and we want to provide the default
            $"declare class {classDeclaration} {{".AppendWithNewSection(sb, 1);
            $"private as: {x.FullName};".AppendLineTo(sb, 2);
            "}".AppendLineTo(sb, 1);
        }

        private void writeNamespace(KeyValuePair<string, TSNamespaceDescription> x, string ns, int indentationLevel) {
            //TODO if there are no members, write the nested namespace, and include the entire chain -- com.sun.star etc. -- as the namespace name
            var nsDescription = x.Value;
            var isRootNamespace = nsDescription is TSRootNamespaceDescription;

            writeJsDoc(nsDescription.JsDoc, 0);
            $"{(isRootNamespace ? "declare " : "")}namespace {RelativeName(x.Key,ns)} {{".AppendLineTo(sb, indentationLevel);

            nsDescription.Aliases.OrderBy(y => y.Key).ForEach(y => writeAlias(y, x.Key, indentationLevel + 1));

            nsDescription.Enums.OrderBy(y => y.Key).ForEach(y => writeEnum(y, indentationLevel + 1));

            nsDescription.Interfaces.OrderBy(y => y.Key).ForEach(y => writeInterface(y, x.Key, indentationLevel + 1));

            nsDescription.Namespaces.OrderBy(y => y.Key).ForEach(y => writeNamespace(y, MakeNamespace(ns,y.Key), indentationLevel + 1));

            "}".AppendWithNewSection(sb, indentationLevel);
        }

        private static Regex blankLineAtBlockEnd = new Regex(@"}(" + NewLine + @"){2}(?=\s*})");
        public NamespaceOutput GetTypescript(KeyValuePair<string, TSRootNamespaceDescription> x) {
            var ns = x.Value;

            sb = new StringBuilder();

            x.Value.ConsolidateMembers();

            ns.NominalTypes.ForEach(writeNominalType);

            writeNamespace(KVP<string, TSNamespaceDescription>(x.Key, x.Value),"", 0);

            ns.GlobalInterfaces.OrderBy(y => y.Key).ForEach(y => writeInterface(y, "", 0));

            var mainFile = sb.ToString()
                .Replace("{" + NewLine + NewLine, "{" + NewLine) //writeJsdoc inserts a blank line before the jsdoc; if the member is the first after an opening brace, tslint doesn't like it
                .RegexReplace(blankLineAtBlockEnd, "}" + NewLine) //removes the blank line after the last interface in the namespace; including nested namespaces
                .Trim() + NewLine;

            var ret = new NamespaceOutput() {
                MainFile = mainFile,
                Description = ns.Description,
                MajorVersion = ns.MajorVersion,
                MinorVersion = ns.MinorVersion,
                Dependencies = ns.Dependencies
            };

            //Build the tests file
            ns.GlobalInterfaces.IfContainsKey("ActiveXObject", y => {
                ret.TestsFile = y.Constructors.Joined(NewLine + NewLine, (z, index) => $"let obj{index} = new ActiveXObject({GetTypeString(z.Parameters[0].Value.Type, "")});") + NewLine;
            });

            return ret;
        }

        public List<KeyValuePair<string, NamespaceOutput>> GetTypescript(TSNamespaceSet namespaceSet) => namespaceSet.Namespaces.Select(kvp => KVP(kvp.Key, GetTypescript(kvp))).ToList();
    }
}