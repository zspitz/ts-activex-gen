using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using static System.IO.Directory;
using static TsActivexGen.Functions;
using static System.Xml.XmlNodeType;
using System.Diagnostics;

namespace TsActivexGen.idlbuilder {
    public class DoxygenIDLBuilder {
        private string idlPath;
        public DoxygenIDLBuilder(string idlPath) => this.idlPath = idlPath;

        public TSNamespaceSet Generate() {
            var ret = new TSNamespaceSet();
            foreach (var x in EnumerateFiles(idlPath, "*.xml").Select(x => (path: x, doc: XDocument.Load(x)))) {
                var root = x.doc.Root;
                var compounddef = root.Elements("compounddef").SingleOrDefault();
                if (compounddef == null) { continue; }

                switch ((string)compounddef.Attribute("kind")) {
                    case "file":
                    case "page":
                    case "dir":
                        continue;
                    case "interface":
                    case "exception":
                        var kvp = parseInterface(compounddef);
                        ret.GetNamespace(kvp.Key).Interfaces.Add(kvp);
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
            if (!baseref.IsNullOrEmpty()) { ret.Extends.Add(baseref); }

            buildJsDoc(x, ret.JsDoc);

            // it is possible to have multiple sectiondefs
            var sectiondef = x.Elements("sectiondef").Where(y => y.Attribute("kind").Value == "public-func").SingleOrDefault();
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

            if (x.Element("attributes").Value.NotIn("[in]","[out]")) { throw new NotImplementedException("Unknown parameter attribute"); }

            return KVP(x.Element("declname").Value, ret);
        }

        private TSSimpleType parseType(XElement x) {
            var type = x.Element("type");
            if (type.HasElements) {
                return type.Element("ref").Value;
            } else {
                return type.Value;
            }
        }

        private void buildJsDoc(XElement x, List<KeyValuePair<string, string>> dest) {
            var description = x.Elements("detaileddescription").SingleOrDefault();
            if (description == null) { return; }

            if (description.Elements().Any(y => y.Name.LocalName != "para")) { throw new NotImplementedException("Unknown description part"); }
            description.Elements().SelectMany(y => parsePara(y).Select(z=>KVP(z.key,z.value))).AddRangeTo(dest);
        }

        private List<(string key, string value)> parsePara(XElement x) {
            var parts = new List<(string key, string value)>() { ("", "") };
            foreach (var node in x.Nodes()) {
                if (node is XText txt) {
                    appendInline(txt.Value);
                    continue;
                }

                var elem = (XElement)node;
                switch (elem.Name.LocalName) {
                    case "ref":
                        appendInline(elem.Value.DeJavaName());
                        break;
                    case "simplesect":
                        switch ((string)elem.Attribute("kind")) {
                            case "version":
                            case "author":
                                continue;
                            case "see":
                                appendNewPart("see", elem.Value.DeJavaName());
                                break;
                            case "since":
                                appendNewPart("since", elem.Value);
                                break;
                            case "return":
                                parsePara(elem.Element("para")).ForEach((z, index) => {
                                    if (!z.key.IsNullOrEmpty()) { throw new NotImplementedException("Nested block tag within inline tag"); }
                                    appendNewPart("returns", z.value);
                                });
                                break;
                        }
                        break;
                    case "computeroutput":
                        appendInline($"`{elem.Value}`");
                        break;
                    case "xrefsect" when elem.Attribute("id").Value.StartsWith("deprecated"):
                        continue;
                    case "itemizedlist":
                        //structure of itemizedlist: itemizedlist -> listitem (multiple) -> each listitem has one para
                        var listParas = elem.Elements().SelectMany(y => y.Elements());
                        if (listParas.Any(y => y.Name.LocalName != "para")) { throw new NotImplementedException("Non-para within itemized list item"); }
                        listParas.SelectMany(parsePara).AddRangeTo(parts);
                        break;
                    case "emphasis":
                        appendInline($"**{elem.Value}**");
                        break;
                    case "linebreak":
                        appendNewPart("","");
                        break;
                    case "ulink":
                        parsePara(elem).ForEach((z, index) => {
                            if (index >0 || !z.key.IsNullOrEmpty()) { throw new NotImplementedException("Nested block tag within inline tag"); }
                            appendInline($"[{z.value}]({elem.Attribute("url")})");
                        });
                        break;
                    case "nonbreakablespace":
                        appendInline(" ");
                        break;
                    case "parameterlist":
                        if (elem.Elements().Any(y=>y.Name.LocalName != "parameteritem")) { throw new NotImplementedException("Unrecognized child of parameterlist"); }
                        elem.Elements().ForEach(y => {
                            var parameterName = y.Descendants("parametername").Single().Value;
                            var parsedDescription = parsePara(y.Descendants("parameterdescription").Single().Elements("para").Single()).Single();
                            if (!parsedDescription.key.IsNullOrEmpty()) { throw new NotImplementedException("Nested block tag within inline tag"); }
                            appendNewPart("param", $"{parameterName} {parsedDescription.value}");
                        });
                        break;
                    case "bold":
                        appendInline($"**{elem.Value}**");
                        break;
                    case "ndash":
                        appendInline(" - ");
                        break;
                    case "orderedlist":
                        if (elem.Elements().Any(y=>y.Name.LocalName != "listitem")) { throw new NotImplementedException("Unrecognized child of orderedlist"); }
                        elem.Elements().SelectMany((y, index) => {
                            var ret = y.Elements("para").SelectMany(parsePara).ToList();
                            if (ret.Any()) { ret[0] = (ret[0].key, $"  {index}. {ret[0].value}"); }
                            return ret;
                        }).ForEach(y => appendNewPart(y.key, y.value));
                        break;
                    case "preformatted":
                        appendInline($"`{elem.Value}`");
                        break;
                    case "table":
                        appendInline("{{table here, see documentation}}");
                        break;
                    case "variablelist":
                        if (elem.Elements().Any(y => y.Name.LocalName.NotIn("varlistentry","listitem"))) { throw new NotImplementedException("Unrecognized child of variablelist"); }
                        elem.Elements("varlistentry").SelectMany(y=> {
                            var term = y.Elements("term").Single().Value;
                            var listitem = y.ElementsAfterSelf().Single();
                            if (listitem.Name.LocalName != "listitem") { throw new NotImplementedException("Unrecognized sibiling of varlistentry"); }
                            var parsedListItem = listitem.Elements("para").SelectMany(parsePara).ToList();
                            if (!parsedListItem.Any(z=>!z.key.IsNullOrEmpty())) { throw new NotImplementedException("Nested block within inline"); }
                            return parsedListItem.Select((z, index) => {
                                if (index == 0) {
                                    return (key: "", value: $"**{term}**: {z.value}");
                                } else {
                                    return (key: "", value: $"     {z.value}");
                                }
                            }).ToList();
                        }).ForEach(y => appendNewPart(y.key, y.value));
                        break;
                    default:
                        throw new NotImplementedException("Unparsed element within para");
                }
            }
            return parts;


            void appendInline(string s)
            {
                var kvp = parts[parts.Count - 1];
                parts[parts.Count - 1] = (kvp.key, kvp.value + s);
            }
            void appendNewPart(string key, string value)
            {
                var (lastKey, lastValue) = parts.Last();
                if (lastKey.IsNullOrEmpty() && lastValue.IsNullOrEmpty()) {
                    parts[parts.Count - 1] = (key, value);
                } else {
                    parts.Add((key, value));
                }
            }

            //TODO have two functions -- parseInline and parseBlock
            //      each takes an XElement
            //      parseInline should return a string
            //      parseBlock should return a keyvalue tuple; the key can be empty when needed
            //      
        }
    }
}
