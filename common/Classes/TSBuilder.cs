using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static System.Environment;
using System.Text.RegularExpressions;
using static TsActivexGen.Functions;
using System.Diagnostics;
using JsDoc = System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>;

namespace TsActivexGen {
    public class TSBuilder {
        private static string[] jsKeywords = new[] { "var" };

        private StringBuilder sb;

        private string jsDocLine(KeyValuePair<string, string> entry) {
            var key = entry.Key;
            if (key == "description") { key = ""; }
            if (key != "") { key = $"@{key} "; }
            return $" {key}{entry.Value}";
        }

        private Regex spaceBreaker = new Regex(@".{0,150}(?:\s|$)");
        private void writeJsDoc(JsDoc JsDoc, int indentationLevel, bool newLine = false) {
            JsDoc = JsDoc.WhereKVP((key, value) => !key.IsNullOrEmpty() || !value.IsNullOrEmpty()).SelectMany(kvp => {
                var valueLines = kvp.Value.Split(new[] { '\n', '\r' });
                var key = kvp.Key;
                if (key == "description") { key = ""; }
                if (!key.IsNullOrEmpty() && valueLines.Length > 1) {
                    valueLines = new[] { valueLines.Joined(" ") };
                }

                return valueLines.SelectMany(line => {
                    if (line.Length <= 150) { return new[] { KVP(key, line) }; }
                    if (!key.IsNullOrEmpty()) {
                        Debug.Print($"Unhandled long line in JSDoc non-description tag {key}");
                        return new[] { KVP(key, line.Substring(0, 145)) };
                    }

                    var returnedLines = new JsDoc();
                    var matches = spaceBreaker.Matches(line);
                    if (matches.Count == 0) { throw new Exception("Unhandled long line in JSDoc"); }
                    foreach (Match match in matches) {
                        if (match.Length == 0) { continue; }
                        returnedLines.Add("", match.Value);
                    }
                    return returnedLines.ToArray();
                }).ToArray();
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

            writeJsDoc(@enum.JsDoc, indentationLevel);

            writeTSLintRuleDisable("no-const-enum", indentationLevel);
            $"const enum {name} {{".AppendLineTo(sb, indentationLevel);
            foreach (var (memberName, memberDescription) in members) {
                writeJsDoc(memberDescription.JsDoc, indentationLevel + 1);
                $"{memberName} = {memberDescription.Value},".AppendLineTo(sb, indentationLevel + 1);
            }
            "}".AppendWithNewSection(sb, indentationLevel);
        }

        private void writeMember(TSMemberDescription m, string ns, int indentationLevel, string memberName, bool isClass) {
            bool isConstructor = memberName.IsNullOrEmpty();

            writeJsDoc(m.JsDoc, indentationLevel, true);

            string accessModifier = "";
            if (m.Private) {
                if (!isClass) { throw new InvalidOperationException("Interface members cannot be private"); }
                accessModifier = "private ";
            }

            var @readonly = m.ReadOnly.GetValueOrDefault() ? "readonly " : "";

            string memberIdentifier;
            if (isConstructor) {
                memberIdentifier = isClass ? "constructor" : "new";
            } else {
                memberIdentifier = memberName;
                if (memberIdentifier.Contains(".") || memberIdentifier.In("constructor")) { memberIdentifier = $"'{memberIdentifier}'"; }
            }

            string genericParameters = "";
            if (m.GenericParameters?.Any() ?? false) {
                if (m.Parameters == null) { throw new InvalidOperationException("Cannot have generic parameters on properties."); }
                genericParameters = $"<{m.GenericParameters.Joined(",", x => GetTypeString(x, ns, true))}>";
            }

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

            var returnType = isClass && isConstructor ? "" : $": {GetTypeString(m.ReturnType, ns)}";

            $"{accessModifier}{@readonly}{memberIdentifier}{genericParameters}{parameterList}{returnType};".AppendLineTo(sb, indentationLevel);
        }

        /// <summary>Provides a simple way to order members by the set of parameters</summary>
        private string parametersString(TSMemberDescription m) => m.Parameters?.JoinedKVP((name, prm) => $"{name}: {GetTypeString(prm.Type, "")}");

        private void writeTSLintRuleDisable(string ruleName, int indentationLevel) => writeTSLintRuleDisable(new[] { ruleName }, indentationLevel);
        private void writeTSLintRuleDisable(IEnumerable<string> ruleNames, int indentationLevel) => $"// tslint:disable-next-line {ruleNames.Joined(" ")}".AppendLineTo(sb, indentationLevel);

        private void writeInterface(KeyValuePair<string, TSInterfaceDescription> x, string ns, int indentationLevel) {
            var name = SplitName(x.Key).name;
            if (ParseTypeName(name) is TSGenericType generic) { name = generic.Name; }

            var @interface = x.Value;
            writeJsDoc(@interface.JsDoc, indentationLevel);

            var tslintRules = new List<string>();
            if (!@interface.IsClass && name.StartsWith("I")) { tslintRules.Add("interface-name"); }

            var typeDefiner = @interface.IsClass ? "class" : "interface";

            var genericParameters = "";
            if (@interface.GenericParameters.Any()) { genericParameters = $"<{@interface.GenericParameters.Joined(",", y => GetTypeString(y, ns, true))}>"; }

            var extends = "";
            if (@interface.Extends.Any()) { extends = "extends " + @interface.Extends.Joined(", ", y => RelativeName(y, ns)) + " "; }

            if (@interface.Members.None() && @interface.Constructors.None()) {
                tslintRules.Add("no-empty-interface");
                writeTSLintRuleDisable(tslintRules, indentationLevel);
                $"{typeDefiner} {name}{genericParameters} {extends}{{ }}".AppendWithNewSection(sb, indentationLevel);
                return;
            }

            writeTSLintRuleDisable(tslintRules, indentationLevel);
            $"{typeDefiner} {name}{genericParameters} {extends}{{".AppendLineTo(sb, indentationLevel);

            @interface.Members
                .Concat(@interface.Constructors.Select(y=>KVP("",y)))
                .OrderByDescending(y=>y.Value.Private)
                .ThenBy(y=>y.Key.IsNullOrEmpty())
                .ThenBy(y => y.Key)
                .ThenByDescending(y => y.Value.Parameters?.Count ?? -1)
                .ThenByDescending(y => y.Value.GenericParameters.Any())
                .ThenBy(y => parametersString(y.Value))
                .ForEach(y => writeMember(y.Value, ns, indentationLevel + 1,y.Key, @interface.IsClass));

            "}".AppendWithNewSection(sb, indentationLevel);
        }

        private void writeAlias(KeyValuePair<string, TSAliasDescription> x, string ns, int indentationLevel) {
            writeJsDoc(x.Value.JsDoc, indentationLevel);
            $"type {SplitName(x.Key).name} = {GetTypeString(x.Value.TargetType, ns)};".AppendWithNewSection(sb, indentationLevel);
        }

        private void writeNamespace(KeyValuePair<string, TSNamespaceDescription> x, string ns, int indentationLevel) {
            var nsDescription = x.Value;
            if (nsDescription.IsEmpty) { return; }
            var isRootNamespace = nsDescription is TSRootNamespaceDescription; //this has to be here, before we overwrite nsDescription with nested namespaces

            while (nsDescription.JsDoc.None() && nsDescription.Aliases.None() && nsDescription.Enums.None() && nsDescription.Interfaces.None() && nsDescription.Namespaces.Count() == 1) {
                string nextKey;
                (nextKey, nsDescription) = nsDescription.Namespaces.First();
                x = KVP($"{x.Key}.{nextKey}", nsDescription);
            }

            var currentNamespace = MakeNamespace(ns, x.Key);

            writeJsDoc(nsDescription.JsDoc, 0);
            $"{(isRootNamespace ? "declare " : "")}namespace {x.Key} {{".AppendLineTo(sb, indentationLevel);

            nsDescription.Aliases.OrderBy(y => y.Key).ForEach(y => writeAlias(y, currentNamespace, indentationLevel + 1));

            nsDescription.Enums.OrderBy(y => y.Key).ForEach(y => writeEnum(y, indentationLevel + 1));

            nsDescription.Interfaces.OrderBy(y => y.Key).ForEach(y => writeInterface(y, currentNamespace, indentationLevel + 1));

            nsDescription.Namespaces.OrderBy(y => y.Key).ForEach(y => writeNamespace(y, currentNamespace, indentationLevel + 1));

            "}".AppendWithNewSection(sb, indentationLevel);
        }

        private void writeNominalType(string typename, StringBuilder sb) {
            var x = ParseTypeName(typename);
            switch (x) {
                case TSSimpleType simpleType:
                    $"declare class {simpleType.FullName} {{".AppendLineTo(sb);
                    $"private typekey: {simpleType.FullName};".AppendLineTo(sb, 1); //we can hardcode the indentation level here, because currently nominal types exist at the root level only
                    "}".AppendWithNewSection(sb);
                    break;
                case TSGenericType genericType: //this handles up to 7 generic type parameters -- T-Z
                    var parameterNames = genericType.Parameters.Select((t, index) => $"{(char)(84 + index)}");
                    $"declare class {genericType.Name}<{parameterNames.Joined(",", y => $"{y} = any")}> {{".AppendLineTo(sb);
                    $"private typekey: {genericType.Name}<{parameterNames.Joined()}>;".AppendLineTo(sb, 1); //we can hardcode the indentation level here, because currently nominal types exist at the root level only
                    "}".AppendWithNewSection(sb);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private static Regex blankLineAtBlockEnd = new Regex(@"(}|;)(" + NewLine + @"){2}(?=\s*})");
        private KeyValuePair<string, NamespaceOutput> GetTypescript(KeyValuePair<string, TSRootNamespaceDescription> x) {
            var ns = x.Value;

            sb = new StringBuilder();

            x.Value.ConsolidateMembers();

            writeNamespace(KVP<string, TSNamespaceDescription>(x.Key, x.Value), "", 0);

            ns.GlobalInterfaces.OrderBy(y => y.Key).ForEach(y => writeInterface(y, "", 0));

            var mainFile = sb.ToString()
                .Replace("{" + NewLine + NewLine, "{" + NewLine) //writeJsdoc inserts a blank line before the jsdoc; if the member is the first after an opening brace, tslint doesn't like it
                .RegexReplace(blankLineAtBlockEnd, "$1" + NewLine) //removes the blank line after the last interface in the namespace; including nested namespaces
                .Trim() + NewLine;

            var ret = new NamespaceOutput() {
                LocalTypes = mainFile,
                RootNamespace = ns
            };

            //Build the tests file
            ns.GlobalInterfaces.IfContainsKey("ActiveXObjectNameMap", y => {
                ret.TestsFile = y.Members.Joined(NewLine.Repeated(2), (kvp, index) => $"let obj{index} = new ActiveXObject('{kvp.Key}');") + NewLine;
            });

            //these have to be written separately, because if the final output is for a single file, these types cannot be repeated
            var sbNominalTypes = new StringBuilder();
            ns.NominalTypes.ForEach(y => writeNominalType(y, sbNominalTypes));
            ret.NominalTypes = sbNominalTypes.ToString();

            return KVP(x.Key, ret);
        }

        public List<KeyValuePair<string, NamespaceOutput>> GetTypescript(TSNamespaceSet namespaceSet) {
            var ret = namespaceSet.Namespaces.Select(kvp => GetTypescript(kvp)).ToList();

            var mergedNominalsBuilder = new StringBuilder();
            var mergedNominals = namespaceSet.Namespaces.SelectMany(x => x.Value.NominalTypes).Distinct().ForEach(x => writeNominalType(x, mergedNominalsBuilder));
            ret.Values().ForEach(x => x.MergedNominalTypes = mergedNominalsBuilder.ToString());

            return ret;
        }
    }
}