using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using static System.IO.Directory;
using static TsActivexGen.Functions;
using static TsActivexGen.MiscExtensions;
using static System.IO.Path;
using System.Diagnostics;
using static TsActivexGen.idlbuilder.Context;

namespace TsActivexGen.idlbuilder {
    public class DoxygenIDLBuilder {
        private string idlPath;
        private readonly Context context;

        public DoxygenIDLBuilder(string idlPath, Context context) {
            this.idlPath = idlPath;
            this.context = context;

            var indexRoot = XDocument.Load(Combine(idlPath, "index.xml")).Root;
            refIDs = indexRoot.Elements("compound").Select(x => KVP(x.Attribute("refid").Value, x.Element("name").Value)).ToDictionary();
            indexRoot.Elements("compound")
                .Where(x => x.Attribute("kind").Value == "namespace")
                .SelectMany(x => x.Elements("member").Where(y => y.Attribute("kind").Value.In("enum", "typedef")))
                .Select(x => KVP(x.Attribute("refid").Value, $"{x.Parent.Element("name").Value}::{x.Element("name").Value}"))
                .AddRangeTo(refIDs);
        }

        private Dictionary<string, string> refIDs;
        private HashSet<string> services = new HashSet<string>();
        private HashSet<string> singletons = new HashSet<string>();
        private HashSet<string> structs = new HashSet<string>();

        public TSNamespaceSet Generate() {
            var ret = new TSNamespaceSet();

            Func<string, bool> fileFilter = s => {
                if (s.EndsWith("_8idl.xml")) { return false; }
                if (s.StartsWith("dir_")) { return false; }
                if (s == "index.xml") { return false; }
                return true;
            };

            foreach (var x in EnumerateFiles(idlPath, "*.xml").Where(fileFilter).Select(x => (path: x, doc: XDocument.Load(x)))) {
                var root = x.doc.Root;
                var compounddef = root.Elements("compounddef").SingleOrDefault();
                if (compounddef == null) { continue; }

                string ns;
                switch ((string)compounddef.Attribute("kind")) {
                    case "file":
                    case "page":
                    case "dir":
                        continue;

                    case "interface":
                    case "exception":
                    case "service":
                    case "singleton":
                    case "struct":
                        var kvp = parseCompound(compounddef);
                        (ns, _) = SplitName(kvp.Key);
                        kvp.AddInterfaceTo(ret.GetNamespace(ns));
                        break;

                    case "namespace":
                        ns = compounddef.Element("compoundname").Value.DeJavaName();
                        var nsDesc = ret.GetNamespace(ns);
                        foreach (var sectiondef in compounddef.Elements("sectiondef")) {
                            switch ((string)sectiondef.Attribute("kind")) {
                                case "enum":
                                    sectiondef.Elements("memberdef").Select(y => parseEnum(y, ns)).AddRangeTo(nsDesc.Enums);
                                    break;
                                case "typedef":
                                    sectiondef.Elements("memberdef").Select(y => parseTypedef(y, ns)).AddRangeTo(nsDesc.Aliases);
                                    break;
                            }

                        }
                        break;

                    default:
                        throw new NotImplementedException("Unparsed compound type");
                }
            }

            var sequenceEquivalents = new List<string>() { "type", "sequence<>" };
            if (context == Automation) { sequenceEquivalents.Add("SafeArray<>"); }
            ret.Namespaces.ForEachKVP((key, rootNs) => sequenceEquivalents.AddRangeTo(rootNs.NominalTypes));

            //add strongly typed overloads for loadComponentFromURL
            var xcl = ret.FindTypeDescription("com.sun.star.frame.XComponentLoader").description as TSInterfaceDescription;
            var loadComponentFromURL = xcl.Members.FirstOrDefault(x => x.Key == "loadComponentFromURL");
            var insertPostion = xcl.Members.IndexOf(kvp => kvp.Key == "loadComponentFromURL");
            new[] {
                ("private:factory/swriter","com.sun.star.text.TextDocument"),
                ("private:factory/scalc", "com.sun.star.sheet.SpreadsheetDocument"),
                ("private:factory/sdraw", "com.sun.star.drawing.DrawingDocument"),
                ("private:factory/simpress", "com.sun.star.presentation.PresentationDocument")
            }.Select(x => {
                var (url, returnTypeName) = x;
                var (name, descr) = loadComponentFromURL.Clone();
                descr.SetParameter("URL", (TSSimpleType)$"'{url}'");
                descr.ReturnType = (TSSimpleType)returnTypeName;
                return KVP("loadComponentFromURL", descr);
            }).InsertRangeTo(insertPostion, xcl.Members);

            var loNamespace = ret.GetNamespace("LibreOffice") as TSRootNamespaceDescription;
            loNamespace.JsDoc.Add("", "Helper types which are not part of the UNO API");

            //build services map and overload
            var servicesMapName = "LibreOffice.ServicesNameMap";
            {
                loNamespace.AddMapType(servicesMapName, services);

                var xmsf = ret.FindTypeDescription("com.sun.star.lang.XMultiServiceFactory").description as TSInterfaceDescription;
                var overload = new TSMemberDescription();
                var placeholder = new TSPlaceholder() { Name = "K", Extends = new TSKeyOf() { Operand = (TSSimpleType)servicesMapName } };
                overload.GenericParameters.Add(placeholder);
                overload.AddParameter("aServiceSpecifier", placeholder);
                var lookup = new TSLookup() { Type = (TSSimpleType)servicesMapName, Accessor = placeholder };
                overload.ReturnType = lookup;
                xmsf.Members.Add("createInstance", overload);

                overload = xmsf.Members.Get("createInstanceWithArguments").Clone();
                overload.GenericParameters.Add(placeholder);
                overload.SetParameter("ServiceSpecifier", placeholder);
                overload.ReturnType = lookup;
                xmsf.Members.Add("createInstanceWithArguments", overload);
            }

            //build singletons map and relevant overloads
            var singletonOverloads = new List<KeyValuePair<string, TSMemberDescription>>();
            var singletonMapName = "LibreOffice.SingletonsNameMap";
            {
                //overload for singleton access that takes any parameter
                var overload = new TSMemberDescription();
                overload.AddParameter("aName", "string");
                overload.ReturnType = TSSimpleType.Any;
                singletonOverloads.Add("getByName", overload);

                //build the singleton name<->type mapping
                loNamespace.AddMapType(singletonMapName, singletons.Select(x => ($"/singleton/{x}", x)));

                //create the overload that uses the mapping
                overload = new TSMemberDescription();
                var placeholder = new TSPlaceholder() { Name = "K", Extends = new TSKeyOf() { Operand = (TSSimpleType)singletonMapName } };
                overload.GenericParameters.Add(placeholder);
                overload.AddParameter("aName", placeholder);
                overload.ReturnType = new TSLookup() { Type = (TSSimpleType)singletonMapName, Accessor = placeholder };
                singletonOverloads.Add("getByName", overload);
            }

            //build struct map
            var structMapName = "LibreOffice.StructNameMap";
            loNamespace.AddMapType(structMapName, structs);

            var instantiableMapName = "LibreOffice.InstantiableNameMap";
            {
                var composite = new TSComposedType("&");
                composite.Parts.Add((TSSimpleType)structMapName);
                composite.Parts.Add((TSSimpleType)servicesMapName);
                loNamespace.Aliases.Add(instantiableMapName, new TSAliasDescription() { TargetType = composite });
            }

            //modify reflection API for strong-typing on createObject method
            {
                var xidlClassName = "com.sun.star.reflection.XIdlClass";

                //add generic parameter with default to XIdlClass
                var xidlClass = ret.FindTypeDescription(xidlClassName).description as TSInterfaceDescription;
                var placeholder = new TSPlaceholder() { Name = "T" };
                xidlClass.GenericParameters.Add(placeholder);

                //modify createObject to take a tuple type of T
                var outParam = new TSTupleType();
                outParam.Members.Add(placeholder);
                xidlClass.Members.Get("createObject").SetParameter("obj", outParam);

                //add forName overload to com.sun.star.reflection.XIdlReflection that leverages the instantiableTypeMap
                var coreReflection = ret.FindTypeDescription("com.sun.star.reflection.XIdlReflection").description as TSInterfaceDescription;
                var overload = coreReflection.Members.Get("forName").Clone();
                var keysources = new TSComposedType();
                new[] { servicesMapName, structMapName }.Select(x => (TSSimpleType)x).AddRangeTo(keysources.Parts);
                placeholder = new TSPlaceholder() { Name = "K", Extends = new TSKeyOf() { Operand = keysources } };
                overload.GenericParameters.Add(placeholder);
                overload.SetParameter("aTypeName", placeholder);
                var returnType = new TSGenericType() { Name = xidlClassName };
                returnType.Parameters.Add(new TSLookup() { Accessor = placeholder, Type = (TSSimpleType)instantiableMapName });
                overload.ReturnType = returnType;
            }

            if (context == Automation) {
                //typed overloads for singleton access go on the script context, which is defined here
                var automationContextName = "LibreOffice.AutomationScriptContext";
                var automationContext = new TSInterfaceDescription();
                automationContext.Extends.Add("com.sun.star.container.XNameAccess");
                singletonOverloads.AddRangeTo(automationContext.Members);
                loNamespace.Interfaces.Add(automationContextName, automationContext);

                //enables access to the script context from the service manager
                var defaultContextMember = new TSMemberDescription();
                defaultContextMember.ReturnType = (TSSimpleType)automationContextName;
                var serviceManagerName = "LibreOffice.AutomationServiceManager";
                var serviceManagerInterface = new TSInterfaceDescription();
                serviceManagerInterface.Extends.Add("com.sun.star.lang.ServiceManager");
                serviceManagerInterface.Members.Add("defaultContext", defaultContextMember);
                loNamespace.Interfaces.Add(serviceManagerName, serviceManagerInterface);

                //add Bridge_GetStruct method, for returning structs
                var getStruct = new TSMemberDescription();
                var placeholder = new TSPlaceholder() { Name = "K", Extends = new TSKeyOf() { Operand = (TSSimpleType)structMapName} };
                getStruct.GenericParameters.Add(placeholder);
                getStruct.AddParameter("structName", placeholder);
                getStruct.ReturnType = new TSLookup() { Type = (TSSimpleType)structMapName, Accessor = placeholder };
                serviceManagerInterface.Members.Add("Bridge_GetStruct", getStruct);

                //adds the service manager entry point to the ActiveX interface
                var serviceManagerConstructor = new TSMemberDescription();
                serviceManagerConstructor.ReturnType = (TSSimpleType)serviceManagerName;
                serviceManagerConstructor.AddParameter("progid", "'com.sun.star.ServiceManager'");
                var @interface = new TSInterfaceDescription();
                @interface.Constructors.Add(serviceManagerConstructor);
                ret.Namespaces["com"].GlobalInterfaces["ActiveXObject"] = @interface;
            }

            //ret.FixBaseMemberConvlicts();

            if (ret.GetUndefinedTypes().Any()) {
                throw new Exception("Undefined types");
            }

            return ret;
        }

        private KeyValuePair<string, TSEnumDescription> parseEnum(XElement x, string ns) {
            var fullname = $"{ns}.{x.Element("name").Value}";
            var ret = new TSEnumDescription();

            long currentValue = 0;
            x.Elements("enumvalue").Select(y => parseEnumValue(y, ref currentValue)).AddRangeTo(ret.Members);

            buildJsDoc(x, ret.JsDoc);
            return KVP(fullname, ret);
        }

        private KeyValuePair<string, TSEnumValueDescription> parseEnumValue(XElement x, ref long currentValue) {
            var initializer = x.Element("initializer");
            if (initializer != null) {
                var initializerValue = initializer.Value.TrimStart(' ', '=');
                currentValue = ParseNumber(initializerValue);
            }
            var name = x.Element("name").Value;
            var ret = new TSEnumValueDescription() { Value = currentValue.ToString() };
            buildJsDoc(x, ret.JsDoc);
            currentValue += 1;
            return KVP(name, ret);
        }

        private KeyValuePair<string, TSAliasDescription> parseTypedef(XElement x, string ns) {
            var fullname = $"{ns}.{x.Element("name").Value}";
            var ret = new TSAliasDescription();
            ret.TargetType = parseType(x, null); //might be for a return type, might be for a parameter type
            buildJsDoc(x, ret.JsDoc);
            return KVP(fullname, ret);
        }

        private KeyValuePair<string, TSInterfaceDescription> parseCompound(XElement x) {
            var fullName = x.Element("compoundname").Value.DeJavaName();

            switch ((string)x.Attribute("kind")) {
                case "service":
                    services.Add(fullName);
                    break;
                case "singleton":
                    singletons.Add(fullName);
                    break;
                case "struct":
                    structs.Add(fullName);
                    break;
            }

            var ret = new TSInterfaceDescription();

            x.Elements("basecompoundref").Select(y => y.Value.DeJavaName()).AddRangeTo(ret.Extends);

            buildJsDoc(x, ret.JsDoc);

            // it is possible to have multiple sectiondefs
            var sectiondef = x.Elements("sectiondef").Where(y => y.Attribute("kind").Value.In("public-func", "public-attrib"));
            if (sectiondef.Any()) {
                sectiondef.SelectMany(y => y.Elements("memberdef")).Select(parseMember).AddRangeTo(ret.Members);
            }

            if (context == Automation) {
                //map pairs of getter/setter methods, or single getter methods, to properties
                var setters = ret.Members.WhereKVP((name, descr) => name.StartsWith("set") && descr.Parameters?.Count == 1).ToDictionary();

                ret.Members.WhereKVP((name, descr) => {
                    if (!name.StartsWith("get")) { return false; }
                    if (descr.Parameters == null || descr.Parameters.Any()) { return false; }
                    return true;
                }).SelectKVP((name, descr) => {
                    var propertyName = name.Substring(3);
                    if (setters.TryGetValue("set" + propertyName, out var setter)) {
                        if (!setter.Parameters.First().Value.Type.Equals(descr.ReturnType)) { setter = null; }
                    }
                    var propertyDescription = new TSMemberDescription {
                        ReadOnly = setter == null,
                        ReturnType = descr.ReturnType
                    };
                    descr.JsDoc.AddRangeTo(propertyDescription.JsDoc);
                    return KVP(propertyName, propertyDescription);
                }).ToList().AddRangeTo(ret.Members);
            }

            return KVP(fullName, ret);
        }

        HashSet<string> kinds = new HashSet<string>();
        private KeyValuePair<string, TSMemberDescription> parseMember(XElement x) {
            var kind = (string)x.Attribute("kind");
            var ret = new TSMemberDescription();
            buildJsDoc(x, ret.JsDoc);
            ret.ReturnType = parseType(x, true);
            if (kind == "function") {
                ret.Parameters = x.Elements("param").Select(parseParameter).ToList();
            }
            return KVP(x.Element("name").Value, ret);
        }

        private KeyValuePair<string, TSParameterDescription> parseParameter(XElement x) {
            var ret = new TSParameterDescription();
            var type = parseType(x, false);
            switch ((string)x.Element("attributes")) {
                case "[out]":
                case "[inout]":
                    var outWrapper = new TSTupleType();
                    outWrapper.Members.Add(type);
                    ret.Type = outWrapper;
                    break;
                default:
                    ret.Type = type;
                    break;
            }

            return KVP(x.Element("declname").Value, ret);
        }

        private static Dictionary<string, ITSType> typeMapping = IIFE(() => {
            var ret = new Dictionary<string, ITSType>();
            new[] { "long", "short", "hyper", "byte", "double", "unsigned short", "unsigned long", "unsigned hyper", "float" }.ForEach(x => ret[x] = TSSimpleType.Number);
            ret[""] = TSSimpleType.Void;
            ret["char"] = TSSimpleType.String; //TODO this needs to be verified; according to the official mapping, it returns a short
            ret["type"] = (TSSimpleType)"type";
            builtins.ForEach(x => ret[x] = (TSSimpleType)x);
            return ret;
        });

        private ITSType parseType(XElement x, bool? forReturn) {
            Func<ITSType, ITSType> mapper = y => {
                if (!(y is TSGenericType t) || t.Name != "sequence") { return y; }
                if (forReturn ?? false) {
                    if (context != Automation) { throw new NotImplementedException(); }
                    var safearray = new TSGenericType() { Name = "SafeArray" };
                    safearray.Parameters.Add(t.Parameters.Single());
                    return safearray;
                } else {
                    var union = new TSComposedType();
                    if (context != Automation) { throw new NotImplementedException(); }
                    new[] { "sequence", "Array", "SafeArray" }.Select(z => {
                        var generic = new TSGenericType() { Name = z };
                        generic.Parameters.Add(t.Parameters.Single());
                        return generic;
                    }).AddRangeTo(union.Parts);
                    return union;
                }
            };

            var type = x.Element("type");
            var name = type.Nodes().Joined("", nodeMapper);
            var ret = ParseTypeName(name, typeMapping);
            ret = MappedType(ret, mapper);
            return ret;

            string nodeMapper(XNode node) {
                string ret1;
                switch (node) {
                    case XText txt:
                        ret1 = txt.Value;
                        break;
                    case XElement elem when elem.Name == "ref":
                        if (!refIDs.TryGetValue(elem.Attribute("refid").Value, out ret1)) {
                            ret1 = elem.Value;
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }
                return ret1.DeJavaName();
            }
        }

        private void buildJsDoc(XElement x, List<KeyValuePair<string, string>> dest) {
            var description = x.Elements("detaileddescription").SingleOrDefault();
            if (description == null) { return; }

            var parts = parseDescriptionNode(description);
            if (parts.Count(y => y.tag == "description") > 1) { throw new InvalidOperationException("Multiple description tags"); }

            for (int i = 0; i < parts.Count;) {
                var (currentTag, aggregateString) = parts[i];
                i++;
                while (i < parts.Count && parts[i].tag.IsNullOrEmpty()) {
                    aggregateString += parts[i].value; //TODO what happens if there is a newline within the text? both for description tag and for other tagswritejsdoc
                    i++;
                }
                aggregateString = aggregateString.Replace("\n", "\n\n"); //single newlines are not rendered as newlines in JsDoc
                dest.Add(currentTag, aggregateString.Trim());
            }
        }

        private List<(string tag, string value)> parseDescriptionNode(XNode node, string currentTag = "") {
            var multiline = currentTag == "description";

            var parts = new List<(string tag, string value)>();
            if (node is XText txt) {
                var prepend = txt.PreviousNode == null ? "" : " ";
                var append = txt.NextNode == null ? "" : " ";
                addInlineText($"{prepend}{txt.Value.Trim()}{append}");
                return parts;
            }

            var elem = (XElement)node;
            var id = (string)elem.Attribute("id");
            var kind = (string)elem.Attribute("kind");
            switch (elem.LocalName()) {
                case "detaileddescription": //com.sun.star.accessibility.IllegalAccessibleComponentStateException
                    if (elem.AnyChildNot("para")) { @throw("Unknown detaileddescription part"); }
                    addNodeResults("description");
                    break;
                case "para": //com.sun.star.accessibility.IllegalAccessibleComponentStateException
                    addNodeResults(currentTag);
                    if (elem.NodesAfterSelf().Any()) {
                        var separator = multiline ? "\n" : "; ";
                        parts.Add("", separator);
                    }
                    break;
                case "ref": //exceptioncom_1_1sun_1_1star_1_1accessibility_1_1_illegal_accessible_component_state_exception.xml
                    addInlineText($"{{@link {elem.Value.DeJavaName()}}}");
                    break;
                case "computeroutput":
                case "preformatted":
                    addInlineText($"`{elem.Value}`");
                    break;

                case "simplesect":
                    switch (kind) {
                        case "attention":
                        case "note":
                            break;
                        case "since":
                        case "author":
                        case "version":
                            addPair(kind, elem.Value);
                            break;
                        case "see": //exceptioncom_1_1sun_1_1star_1_1accessibility_1_1_illegal_accessible_component_state_exception.xml
                            addPair("see", elem.Value.DeJavaName());
                            break;
                        case "return":
                            parts.Add("returns", "");
                            addNodeResults("returns");
                            break;
                        case "pre":
                        case "remark":
                            var intro = kind == "pre" ? "Precondition" : "Remark";
                            parts.Add("", "\n");
                            parts.Add("", $"**{intro}**: ");
                            addNodeResults("");
                            break;
                        default:
                            @throw("Unhandled simplesect");
                            break;
                    }
                    break;

                case "xrefsect" when id?.StartsWith("deprecated") ?? false: //exceptioncom_1_1sun_1_1star_1_1beans_1_1_introspection_exception.xm
                    addPair("deprecated", elem.Value);
                    break;
                case "xrefsect" when id?.StartsWith("todo") ?? false:
                    addPair("todo", elem.Value);
                    break;

                case "itemizedlist":
                    if (elem.AnyChildNot("listitem")) { @throw("Unrecognized child of itemizedlist"); }
                    addNodeResults(currentTag);
                    break;
                case "listitem":
                    if (elem.AnyChildNot("para")) { @throw("Unrecognized child of listitem"); }
                    addNodeResults(currentTag);
                    break;
                case "emphasis":
                case "bold":
                case "term":
                    addInlineText($"**{elem.Value}**");
                    break;
                case "superscript":
                    addInlineText($"<sup>{elem.Value}</sup>");
                    break;
                case "heading":
                    parts.Add("", "**");
                    addNodeResults("");
                    parts.Add("", "**");
                    break;

                case "anchor":
                    break;

                case "linebreak":
                    if (multiline) {
                        addPair("", "\n");
                    } else {
                        addInlineText("\n");
                    }
                    break;
                case "ulink":
                    addInlineText("[");
                    addNodeResults(""); //forces inline
                    addInlineText($"]{{@link {elem.Attribute("url")}}}");
                    break;

                case "nonbreakablespace":
                    addInlineText(" ");
                    break;
                case "ndash":
                    addInlineText(" - ");
                    break;
                case "mdash": //com.sun.star.io.XAsyncOutputMonitor
                    addInlineText(" -- ");
                    break;
                case "ldquo": //com.sun.star.reflection.XPublished
                case "rdquo":
                    addInlineText("\"");
                    break;
                case "prime": //com.sun.star.uri.UriReferenceFactory
                    addInlineText("'");
                    break;

                case "parameterlist" when kind == "param" || kind == "exception":
                    if (elem.AnyChildNot("parameteritem")) { @throw("Unrecognized child of parameterlist"); }
                    var nextTag = kind == "param" ? "param" : "throws";
                    addNodeResults(nextTag);
                    break;
                case "parameteritem":
                    if (elem.AnyChildNot("parameternamelist", "parameterdescription")) { @throw("Unrecognized child of parameteritem"); }
                    var parameterName = elem.Elements("parameternamelist").Single().Elements("parametername").First().Value;
                    addPair(currentTag, parameterName);
                    parseDescriptionNode(elem.Elements("parameterdescription").Single()).AddRangeTo(parts);
                    break;
                case "parameterdescription":
                    parts.Add("", " ");
                    addNodeResults("");
                    break;

                case "orderedlist":
                    if (!multiline) {
                        addInlineText("{{ordered list here, see documentation}}");
                    } else {
                        if (elem.AnyChildNot("listitem")) { @throw("Unrecognized child of orderedlist"); }
                        var toAdd = elem.Nodes().SelectMany((y, index) => {
                            var ret = parseDescriptionNode(y, "description");
                            ret.Insert(0, ("", $" {index + 1}. "));
                            return ret;
                        }).ToList();
                        if (toAdd.Any(x => !x.tag.IsNullOrEmpty())) { @throw("Block tag inside ordered list item"); }
                        toAdd.AddRangeTo(parts);
                    }
                    break;

                case "table":
                    addInlineText("{{table here, see documentation}}");
                    break;

                case "programlisting": //com.sun.star.container.XContentEnumerationAccess
                    addInlineText("{{program example here, see documentation}}");
                    if (multiline) { parts.Add("", "\n"); }
                    break;

                case "variablelist":
                    elem.Elements("varlistentry").SelectMany(x => parseDescriptionNode(x, currentTag)).AddRangeTo(parts);
                    break;
                case "varlistentry":
                    if (elem.NextNode is XElement elem1 && elem1.LocalName() != "listitem") { @throw("Unrecognized element after varlistentry"); }
                    addNodeResults("");
                    addInlineText(": ");
                    parseDescriptionNode(elem.NextNode).AddRangeTo(parts);
                    if (multiline) { parts.Add("", "\n"); }
                    break;

                case "verbatim":
                    var value = elem.Value.Trim();
                    if (!value.IsNullOrEmpty()) {
                        //TODO advanced verbatim parsing
                        //if (!value.Contains("</p>")) { value = value.Replace("<p>", ""); } // handles malformed HTML
                        //if (value.Contains("<")) {
                        //    //parse as XML, calling parseDescriptionNode on element
                        //    //wrap with root

                        //    var toParse = XElement.Parse("<root>" + value + "</root>");
                        //    parseDescriptionNode(toParse, currentTag).AddRangeTo(parts);
                        //} else {
                        //    var lines = value.Split(new[] { '\n' });
                        //    for (int i = 0; i < lines.Length; i++) {
                        //        var line = lines[i];
                        //        var (first, rest) = FirstPathPart(line, " ");
                        //        if (first.Trim().StartsWith("@")) {
                        //            parts.Add(first.Trim().Substring(1), rest);
                        //            if (first == "@param") {
                        //                var paramWords = rest.Split(' ');
                        //                if (paramWords.Length==1) {
                        //                    i++;
                        //                    addInlineText(lines[i]);
                        //                }
                        //            }
                        //        }
                        //    }

                        //    @throw("");
                        //}
                        foreach (var line in value.Split('\n')) {
                            var trimmed = line.Trim();
                            if (trimmed.StartsWith("@")) {
                                var (first, rest) = FirstPathPart(trimmed, " ");
                                addPair(first.Trim(), rest.Trim());
                            } else {
                                parts.Add("", line.TrimEnd());
                                var separator = multiline ? "\n" : " ";
                                parts.Add("", separator);
                            }
                        }
                    }
                    break;

                default:
                    @throw("Unrecognized element");
                    break;
            }

            return parts;

            void addInlineText(string text) {
                text = text.Replace("\n", "; ");
                parts.Add("", text);
            }
            void addPair(string tag, string text) {
                text = text.Replace("\n", "; ");
                parts.Add(tag, text);
            }
            void @throw(string msg) => throw new NotImplementedException(msg);
            void addNodeResults(string tag) => elem.Nodes().SelectMany(x => parseDescriptionNode(x, tag)).AddRangeTo(parts);
        }
    }
}
