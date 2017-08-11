using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using static System.IO.Directory;
using static TsActivexGen.Functions;
using System.Diagnostics;
using static System.Environment;
using System.Text.RegularExpressions;

namespace TsActivexGen.idlbuilder {
    public class DoxygenIDLBuilder {
        private string idlPath;
        public DoxygenIDLBuilder(string idlPath) => this.idlPath = idlPath;

        public TSNamespaceSet Generate() {
            var ret = new TSNamespaceSet();
            foreach (var x in EnumerateFiles(idlPath, "*.xml").Where(x=>!x.EndsWith("_8idl.xml") && !x.StartsWith("dir_")).Select(x => (path: x, doc: XDocument.Load(x)))) {
                var root = x.doc.Root;
                var compounddef = root.Elements("compounddef").SingleOrDefault();
                if (compounddef == null) { continue; }

                switch ((string)compounddef.Attribute("kind")) {
                    case "file":
                    case "page":
                    case "dir":
                    case "namespace":
                        continue;

                    case "interface":
                    case "exception":
                    case "service":
                    case "singleton":
                    case "struct":
                        var kvp = parseInterface(compounddef);
                        var (ns, _) = SplitName(kvp.Key);
                        ret.GetNamespace(ns).Interfaces.Add(kvp);
                        break;

                    default:
                        throw new NotImplementedException("Unparsed compound type");
                }
            }

            return ret;
        }

        int counter = 0;
        private KeyValuePair<string, TSInterfaceDescription> parseInterface(XElement x) {
            var fullName = x.Element("compoundname").Value.DeJavaName();
            Debug.Print($"{counter} -- {fullName}");
            counter += 1;

            var ret = new TSInterfaceDescription();

            var baseref = (string)x.Element("basecompoundref");
            if (!baseref.IsNullOrEmpty()) { ret.Extends.Add(baseref.DeJavaName()); }

            buildJsDoc(x, ret.JsDoc);

            // it is possible to have multiple sectiondefs
            var sectiondef = x.Elements("sectiondef").Where(y => y.Attribute("kind").Value.In("public-func", "public-attr")).SingleOrDefault();
            if (sectiondef != null) {
                sectiondef.Elements("memberdef").Select(parseMember).AddRangeTo(ret.Members);
            }

            return KVP(fullName, ret);
        }

        private KeyValuePair<string, TSMemberDescription> parseMember(XElement x) {
            var ret = new TSMemberDescription();
            buildJsDoc(x, ret.JsDoc);

            ret.ReturnType = parseType(x);

            // are there never any properties?
            ret.Parameters = x.Elements("param").Select(parseParameter).ToList();
            // assuming there are no properties, there is never any readonly either

            return KVP(x.Element("name").Value, ret);
        }

        private KeyValuePair<string, TSParameterDescription> parseParameter(XElement x) {
            var ret = new TSParameterDescription();
            ret.Type = parseType(x);

            if (x.Element("attributes").Value.NotIn("[in]", "[out]","[inout]")) { throw new NotImplementedException("Unknown parameter attribute"); }

            return KVP(x.Element("declname").Value, ret);
        }

        private TSSimpleType parseType(XElement x) {
            var type = x.Element("type");
            string ret;
            if (type.HasElements) {
                ret = type.Element("ref").Value;
            } else {
                ret =type.Value;
            }
            if (ret.IsNullOrEmpty()) { return TSSimpleType.Void; }
            if (ret.In("long","short","hyper","byte","double")) { return TSSimpleType.Number; }
            if (ret == "char") { return TSSimpleType.String; }
            return ret.DeJavaName();
        }

        static Regex reNewLine = new Regex(@"(?:\r\n|\r|\n)\s*");
        private void buildJsDoc(XElement x, List<KeyValuePair<string, string>> dest) {
            var description = x.Elements("detaileddescription").SingleOrDefault();
            if (description == null) { return; }

            var parts = parseNode(description);
            var results = new List<(string tag, string value)>();
            foreach (var part in parts) {
                switch (part) {
                    case string s:
                        if (results.None()) { results.Add("", ""); }
                        var (tag, value) = results.Last();
                        var newValue = (value + s).RegexReplace(reNewLine, "; ");
                        results[0] = (tag, value += s);
                        break;
                    case ValueTuple<string, string> t:
                        t = (t.Item1, t.Item2.RegexReplace(reNewLine, "; "));
                        results.Add(t);
                        break;

                }
            }
            results.Select(y => KVP(y.tag, y.value)).AddRangeTo(dest);
        }

        /// <returns>null or string or value tuple of two strings</returns>
        private List<object> parseNode(XNode node) {
            var parts = new List<object>();
            if (node is XText txt) {
                addPart(txt.Value);
                return parts;
            }

            var elem = (XElement)node;
            var id = (string)elem.Attribute("id");
            List<object> nodeResults;
            switch (elem.LocalName()) {
                case "para":
                case "parameternamelist":
                case "parameterdescription":
                case "parametername":
                    addNodeResults();
                    break;

                case "anchor":
                    break;

                case "detaileddescription":
                    if (elem.AnyChildNot("para")) { @throw("Unknown detaileddescription part"); }
                    addNodeResults();
                    break;
                case "ref":
                    addPart($"{{@link {elem.Value.DeJavaName()}}}");
                    break;
                case "computeroutput":
                case "preformatted":
                    addPart($"`{elem.Value}`");
                    break;
                case "simplesect":
                    switch ((string)elem.Attribute("kind")) {
                        case var kind when kind.In("since", "author", "version"):
                            addPair(kind, elem.Value);
                            break;
                        case "see":
                            addPair("see", elem.Value.DeJavaName());
                            break;
                        case "return":
                            nodeResults = elem.Nodes().SelectMany(parseNode).ToList();
                            if (nodeResults.OfType<(string, string)>().Count() > 1) {
                                Debug.Print("Nested block tag within return section"); //com.sun.star.i18n.XCollator
                                nodeResults = nodeResults.Select(y => {
                                    object ret=null;
                                    switch (y) {
                                        case string s:
                                            ret = y;
                                            break;
                                        case ValueTuple<string, string> t when t.Item1.IsNullOrEmpty():
                                            if (t.Item2.IsNullOrEmpty()) {
                                                ret = ";";
                                            } else {
                                                ret = t.Item2;
                                            }
                                            break;
                                        default:
                                            @throw("Unrecognized node result type");
                                            break;
                                    }
                                    return ret;
                                }).Where(y=>y != null).ToList();
                            }
                            addParts(nodeResults);
                            break;
                    }
                    break;
                case "xrefsect" when id?.StartsWith("deprecated") ?? false:
                    addPair("deprecated", elem.Value);
                    break;
                case "xrefsect" when id?.StartsWith("todo") ?? false:
                    addPair("todo", elem.Value);
                    break;
                case "itemizedlist":
                    if (elem.AnyChildNot("listitem")) { @throw("Unrecognized child of itemizedlist"); }
                    addNodeResults();
                    break;
                case "listitem":
                    if (elem.AnyChildNot("para")) { @throw("Unrecognized child of listitem"); }
                    addNodeResults();
                    break;
                case "emphasis":
                case "bold":
                case "term":
                    addPart($"**{elem.Value}**");
                    break;
                case "linebreak":
                    addPair("", "");
                    break;
                case "ulink":
                    nodeResults = elem.Nodes().SelectMany(parseNode).ToList();
                    if (nodeResults.OfType<(string, string)>().Any()) { @throw("Nested block tag within ulink"); }
                    addPart("[");
                    addParts(nodeResults);
                    addPart($"]{{@link {elem.Attribute("url")}}}");
                    break;
                case "nonbreakablespace":
                    addPart(" ");
                    break;
                case "parameterlist":
                    if (elem.AnyChildNot("parameteritem")) { @throw("Unrecognized child of parameterlist"); }
                    addNodeResults();
                    break;
                case "parameteritem":
                    if (elem.AnyChildNot("parameternamelist", "parameterdescription")) { @throw("Unrecognized child of parameteritem"); }
                    addPair("param", "");
                    nodeResults = elem.Nodes().SelectMany(parseNode).ToList();
                    if (nodeResults.OfType<(string, string)>().Any()) {
                        Debug.Print($"file:{elem.BaseUri}, element:{elem.LocalName()}, msg:Nested block tags within parameter description");
                        nodeResults = nodeResults.TakeWhile(y => !(y is ValueType)).ToList();
                    }
                    addParts(nodeResults);
                    break;
                case "ndash":
                    addPart(" - ");
                    break;
                case "orderedlist": //com.sun.star.awt.XContainerWindowProvider
                    if (elem.AnyChildNot("listitem")) { @throw("Unrecognized child of orderedlist"); }
                    addNodeResults();
                    break;
                case "table":
                    addPart("{{table here, see documentation}}");
                    break;
                case "variablelist": //com.sun.star.beans.XIntrospection
                    addParts(elem.Elements("varlistentry").Select(parseNode));
                    break;
                case "varlistentry":
                    if (elem.NextNode is XElement elem1 && elem1.LocalName() != "listitem") { @throw("Unrecognized element after varlistentry"); }
                    addNodeResults();
                    addPart(": ");
                    addParts(parseNode(elem.NextNode));
                    break;
                case "superscript":
                    addPart($"<sup>{elem.Value}</sup>");
                    break;
                case "verbatim":
                    var text = elem.Value;
                    var lines = text.Split(new[] { NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    if (text.StartsWith("@")) {
                        lines.ForEach(line => {
                            var (first, rest) = FirstPathPart(line, " ");
                            if (!first.StartsWith("@")) { @throw("Invalid jsdoc tag"); }
                            addPair(first.Substring(1), rest);
                        });
                    } else { //com.sun.star.drawing.XEnhancedCustomShapeDefaulter
                        lines.ForEach(line => addPair("", line));
                    }
                    break;
                case "programlisting": //com.sun.star.container.XContentEnumerationAccess
                    addPart("{{program example here, see documentation}}");
                    break;
                case "heading":
                    addPart("**");
                    addNodeResults();
                    addPart("**: ");
                    break;
                case "mdash": //com.sun.star.io.XAsyncOutputMonitor
                    addPart(" -- ");
                    break;
                case "ldquo": //com.sun.star.reflection.XPublished
                case "rdquo":
                    addPart("\"");
                    break;
                case "prime": //com.sun.star.uri.UriReferenceFactory
                    addPart("'");
                    break;
                default:
                    throw new NotImplementedException("Unrecognized element");
            }

            return parts.Where(x => x != null).ToList();

            void @throw(string msg) => throw new NotImplementedException(msg);
            void addPart(string part) => parts.Add(part);
            void addPair(string key, string value) => parts.Add((key, value));
            void addNodeResults() => elem.Nodes().SelectMany(parseNode).AddRangeTo(parts);
            void addParts(IEnumerable<object> toAdd) => toAdd.AddRangeTo(parts);
        }
    }
}
