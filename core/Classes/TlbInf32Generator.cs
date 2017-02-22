using System;
using System.Collections.Generic;
using System.Linq;
using TLI;
using TsActivexGen.Util;
using static TsActivexGen.Util.Functions;
using static TsActivexGen.TSParameterType;
using static TLI.InvokeKinds;
using static TLI.TypeKinds;
using static TLI.TliVarType;
using System.Diagnostics;
using TsActivexGen.ActiveX;

namespace TsActivexGen {
    public class TlbInf32Generator {
        static string AsString(object value) {
            var t = value.GetType().UnderlyingIfNullable();
            if (t == typeof(string)) {
                return $"\'{(value as string).Replace("'", "\\'")}\'";
            } else if (t.IsNumeric()) {
                return $"{value}";
            } else if (t == typeof(bool)) {
                return (bool)value ? "true" : "false";
            }
            throw new Exception($"Unable to generate string representation of value '{value}' of type '{t.Name}'");
        }

        //unhandled values for VarType
        /*
VT_RESERVED	32768
VT_BYREF	16384
VT_CLSID	72
VT_CF	71
VT_BLOB_OBJECT	70
VT_STORED_OBJECT	69
VT_STREAMED_OBJECT	68
VT_STORAGE	67
VT_STREAM	66
VT_BLOB	65
VT_FILETIME	64
VT_RECORD	36
VT_USERDEFINED	29
VT_CARRAY	28
VT_SAFEARRAY	27
VT_PTR	26
VT_UNKNOWN	13
VT_ERROR	10
VT_NULL	1
 */

        private TSSimpleType GetTypeName(VarTypeInfo vti, object value = null) {
            var ret = new TSSimpleType();
            var splitValues = vti.VarType.SplitValues();
            if (splitValues.SequenceEqual(new[] { VT_EMPTY }) && vti.TypeInfo.TypeKind == TKIND_ALIAS && !vti.IsExternalType) {
                splitValues = vti.TypeInfo.ResolvedType.VarType.SplitValues();
            }
            var isArray = splitValues.ContainsAny(VT_VECTOR, VT_ARRAY);
            if (splitValues.ContainsAny(VT_I1, VT_I2, VT_I4, VT_I8, VT_R4, VT_R8, VT_UI1, VT_UI2, VT_UI4, VT_UI8, VT_CY, VT_DECIMAL, VT_INT, VT_UINT)) {
                ret.FullName = "number";
            } else if (splitValues.ContainsAny(VT_BSTR, VT_LPSTR, VT_LPWSTR)) {
                ret.FullName = "string";
            } else if (splitValues.ContainsAny(VT_BOOL)) {
                ret.FullName = "boolean";
            } else if (splitValues.ContainsAny(VT_VOID, VT_HRESULT)) {
                ret.FullName = "void";
            } else if (splitValues.ContainsAny(VT_DATE)) {
                ret.FullName = "VarDate";
            } else if (splitValues.ContainsAny(VT_EMPTY)) {
                var ti = vti.TypeInfo;
                ret.FullName = $"{ti.Parent.Name}.{ti.Name}";
                if (vti.IsExternalType) { AddTLI(vti.TypeLibInfoExternal); }
                interfaceToCoClassMapping.IfContainsKey(ret.FullName, val => ret.FullName = val.FirstOrDefault());
            } else if (splitValues.ContainsAny(VT_VARIANT, VT_DISPATCH)) {
                ret.FullName = "any";
            } else {
                if (Debugger.IsAttached) {
                    var debug = vti.Debug();
                }
                var external = vti.IsExternalType ? " (external)" : "";
                ret.Comment = $"{vti.VarType.ToString()}{external}";
                ret.FullName = "any";
            }

            if (ret.FullName == "any" && value != null) {
                var t = value.GetType();
                if (t == typeof(string)) {
                    ret.FullName = "string";
                } else if (t.IsNumeric()) {
                    ret.FullName = "number";
                }
            }
            if (isArray) { ret.FullName += "[]"; }
            return ret;
        }

        //https://github.com/zspitz/ts-activex-gen/issues/25
        private KeyValuePair<string, TSNamespaceDescription> ToTSNamespaceDescription(ConstantInfo c) {
            var ret = new TSNamespaceDescription();
            c.Members.Cast().Select(x => KVP(x.Name, AsString((object)x.Value))).AddRangeTo(ret.Members);
            ret.JsDoc.Add("", c.HelpString);
            return KVP($"{c.Parent.Name}.{c.Name}", ret);
        }

        private KeyValuePair<string, TSEnumDescription> ToTSEnumDescription(ConstantInfo c) {
            var ret = new TSEnumDescription();
            c.Members.Cast().Select(x => {
                var oValue = (object)x.Value;
                var typename = GetTypeName(x.ReturnType, oValue);
                if (ret.Typename == null) {
                    ret.Typename = typename;
                } else if (ret.Typename != TSSimpleType.Any && ret.Typename != typename) {
                    ret.Typename = TSSimpleType.Any;
                }
                return KVP(x.Name, AsString(oValue));
            }).AddRangeTo(ret.Members);
            ret.JsDoc.Add("", c.HelpString);
            return KVP($"{c.Parent.Name}.{c.Name}", ret);
        }

        private KeyValuePair<string, TSParameterDescription> ToTSParameterDescription(ParameterInfo p, bool isRest, List<KeyValuePair<string,string>> jsDoc) {
            var ret = new TSParameterDescription();
            var name = p.Name;
            var returnType = GetTypeName(p.VarTypeInfo);
            ret.Type = returnType;
            if (isRest) {
                ret.ParameterType = Rest;
            } else if (p.Optional || p.Default) {
                ret.ParameterType = Optional;
            } else {
                ret.ParameterType = Standard;
            }
            if (p.Default) {
                var defaultValue = p.DefaultValue;
                if (defaultValue != null) {
                    var kvp = KVP("param", $"{returnType.FullName} [{name}={AsString(p.DefaultValue)}]");
                    if (!jsDoc.Contains(kvp)) { jsDoc.Add(kvp); }
                }
            }
            return KVP(p.Name, ret);
        }

        private List<KeyValuePair<string, TSParameterDescription>> GetSingleParameterList(IEnumerable<MemberInfo> members, List<KeyValuePair<string,string>> jsDoc) {
            var parameterLists = members.Select(m => {
                var parameterCount = m.Parameters.Count;
                return m.Parameters.Cast().Select((p, index) => {
                    bool isRest = m.Parameters.OptionalCount == -1 && index == parameterCount - 1;
                    return ToTSParameterDescription(p, isRest, jsDoc);
                }).ToList();
            }).Distinct(new TSParameterListComparer()).ToList();
            if (parameterLists.Count > 1) {
                throw new InvalidOperationException("Unable to parse different parameter lists");
            }
            return parameterLists.FirstOrDefault();
        }

        private TSMemberDescription GetMemberDescriptionForName(IEnumerable<MemberInfo> members) {
            var ret = new TSMemberDescription();

            var parameterList = GetSingleParameterList(members, ret.JsDoc);
            var paramType = Standard;
            parameterList.ForEachKVP((name, p) => {
                if (p.ParameterType == Standard && paramType == Optional) { p.ParameterType = Optional; }
                paramType = p.ParameterType;
            });

            var memberCount = members.Count();
            bool hasSetter = false;
            if (memberCount > 1) {
                if (!members.All(x => x.IsProperty())) {
                    throw new InvalidOperationException("Unable to parse single name with property and non-property members");
                }
                if (memberCount.In(2, 3)) {
                    //readwrite properties will have multiple members - one getter and one setter; setters can also be either Set or Let (simple assignment)
                    bool hasGetter = members.Any(m => m.InvokeKind == INVOKE_PROPERTYGET);
                    hasSetter = members.Any(m => m.InvokeKind.In(INVOKE_PROPERTYPUT, INVOKE_PROPERTYPUTREF));
                    if (!hasGetter || !hasSetter) { throw new InvalidOperationException("Unable to parse multiple getters or multiple setter."); }
                } else {
                    throw new InvalidOperationException("Unable to parse multiple getters or multiple setter.");
                }
            }

            var invokeable = members.First().IsInvokeable();
            if (members.Any(x => x.IsInvokeable() != invokeable)) {
                throw new InvalidOperationException("Invokeable and non-invokeable members with the same name.");
            }
            invokeable = invokeable || (parameterList != null && parameterList.Any());
            if (invokeable) {
                ret.Parameters = parameterList ?? new List<KeyValuePair<string, TSParameterDescription>>();
            }

            if (!invokeable) {
                ret.ReadOnly = !hasSetter;
            }

            ret.ReturnType = GetTypeName(members.First().ReturnType);
            if (hasSetter && parameterList.Any()) {
                ret.Comment = "Also has setter with parameters";
            }

            ret.JsDoc.Add("", members.Select(x => x.HelpString).Distinct().Joined(" / "));

            return ret;
        }

        private Dictionary<string, TSMemberDescription> GetMembers(Members members, ref string enumerableType) {
            var ret = members.Cast().Where(x => !x.IsRestricted() && x.Name != "_NewEnum").ToLookup(x => x.Name).Select(grp => KVP(grp.Key, GetMemberDescriptionForName(grp))).ToDictionary();

            var enumerableType1 = enumerableType; //because ref parameters cannot be used within lambda expressions
            members.Cast().ToLookup(x => x.Name).IfContainsKey("_NewEnum", mi => {
                ret.IfContainsKey("Item", itemMI => enumerableType1 = (itemMI.ReturnType as TSSimpleType).FullName);
            });
            enumerableType = enumerableType1;

            return ret;
        }

        private KeyValuePair<string, TSInterfaceDescription> ToTSInterfaceDescriptionBase(Members m, string typename, string helpString) {
            var ret = new TSInterfaceDescription();
            string enumerableType = null;
            GetMembers(m, ref enumerableType).AddRangeTo(ret.Members);
            if (!enumerableType.IsNullOrEmpty()) { enumerableCollectionItemMapping[new TSSimpleType(typename)] = enumerableType; }
            ret.JsDoc.Add("", helpString);
            return KVP(typename, ret);
        }

        private KeyValuePair<string, TSInterfaceDescription> ToTSInterfaceDescription(InterfaceInfo i) {
            var typename = $"{i.Parent.Name}.{i.Name}";
            return ToTSInterfaceDescriptionBase(i.Members, typename,i.HelpString);
        }

        private KeyValuePair<string, TSInterfaceDescription> ToTSInterfaceDescription(CoClassInfo c) {
            var typename = $"{c.Parent.Name}.{c.Name}";
            //scripting environments can only use the default interface, because they don't have variable types
            //we can thus ignore everything else in c.Interfaces
            return ToTSInterfaceDescriptionBase(c.DefaultInterface.Members, typename,c.HelpString);
        }

        private KeyValuePair<string, TSInterfaceDescription> ToTSInterfaceDescription(RecordInfo r) {
            var ret = new TSInterfaceDescription();
            string enumerableType = null; //this value will be ignored for RecordInfo
            GetMembers(r.Members, ref enumerableType).AddRangeTo(ret.Members);
            ret.JsDoc.Add("", r.HelpString);
            return KVP($"{r.Parent.Name}.{r.Name}", ret);
        }

        private TSMemberDescription ToActiveXObjectConstructorDescription(CoClassInfo c) {
            var ret = new TSMemberDescription();
            var typename = $"{c.Parent.Name}.{c.Name}";
            ret.AddParameter("progid", $"'{typename}'"); //note the string literal type
            ret.ReturnType = new TSSimpleType(typename);
            return ret;
        }

        //ActiveXObject.on(obj: 'Word.Application', 'BeforeDocumentSave', ['Doc','SaveAsUI','Cancel'], function (params) {});
        private TSMemberDescription ToActiveXEventMember(MemberInfo m, CoClassInfo c) {
            var args = m.Parameters.Cast().Select(x => KVP<string,ITSType>(x.Name, GetTypeName(x.VarTypeInfo)));
            var typename = $"{c.Parent.Name}.{c.Name}";

            var ret = new TSMemberDescription();
            ret.AddParameter("obj", $"{typename}");
            ret.AddParameter("eventName", $"'{m.Name}'");
            if (args.Keys().Any()) {ret.AddParameter("eventArgs", new TSTupleType(args.Keys().Select(x => $"'{x}'")));}

            var parameterType = new TSObjectType();
            args.AddRangeTo(parameterType.Members);
            var fnType = new TSFunctionType();
            fnType.FunctionDescription.AddParameter("this", typename);
            fnType.FunctionDescription.AddParameter("parameter", parameterType);
            fnType.FunctionDescription.ReturnType = TSSimpleType.Void;
            ret.AddParameter("handler", fnType);

            ret.ReturnType = TSSimpleType.Void;

            return ret;
        }

        private TSMemberDescription ToEnumeratorConstructorDescription(KeyValuePair<TSSimpleType,string> kvp) {
            var collectionTypeName = kvp.Key;
            var itemTypeName = kvp.Value;
            var ret = new TSMemberDescription();
            ret.AddParameter("col", collectionTypeName);
            ret.ReturnType = new TSSimpleType($"Enumerator<{itemTypeName}>");
            return ret;
        }

        private TSNamespace ToNamespace(TypeLibInfo tli) {
            var coclasses = tli.CoClasses.Cast().ToList();

            var ret = new TSNamespace() { Name = tli.Name };
            tli.Constants.Cast().Where(x => x.TypeKind != TKIND_MODULE).Select(ToTSEnumDescription).AddRangeTo(ret.Enums);
            tli.Constants.Cast().Where(x => x.TypeKind == TKIND_MODULE).Select(ToTSNamespaceDescription).AddRangeTo(ret.Namespaces);
            coclasses.Select(ToTSInterfaceDescription).AddRangeTo(ret.Interfaces);

            if (tli.Declarations.Cast().Any()) {
                //not sure what these are, if they are accessible from JScript
                //there is one in the Excel object library
            }
            if (tli.Unions.Cast().Any()) {
                //not sure what these are, if they are accessible from JScript
                //there are a few in the DirectX transforms library
            }

            var activex = new TSInterfaceDescription();
            coclasses.Where(x => x.IsCreateable()).OrderBy(x => x.Name).Select(ToActiveXObjectConstructorDescription).AddRangeTo(activex.Constructors);
            coclasses.Select(x => new {
                coclass = x,
                eventInterface = x.DefaultEventInterface
            }).Where(x => x.eventInterface != null).SelectMany(x => x.eventInterface.Members.Cast().Select(y => KVP("on", ToActiveXEventMember(y, x.coclass)))).AddRangeTo(activex.Members);

            if (activex.Constructors.Any()) {
                ret.GlobalInterfaces["ActiveXObject"] = activex;
            }

            var guid = tli.GUID;
            ret.Description = TypeLibDetails.FromRegistry.Value.FirstOrDefault(x => x.TypeLibID == guid).Name;

            return ret;
        }

        private KeyValuePair<string, TSSimpleType> ToTypeAlias(IntrinsicAliasInfo ia) {
            var ret = KVP($"{ia.Parent.Name}.{ ia.Name}", GetTypeName(ia.ResolvedType));
            ret.Value.JsDoc.Add("", ia.HelpString);
            return ret;
        }
        private KeyValuePair<string, TSSimpleType> ToTypeAlias(UnionInfo u) {
            var ret= KVP($"{u.Parent.Name}.{u.Name}", TSSimpleType.Any);
            ret.Value.JsDoc.Add("", u.HelpString);
            return ret;
        }

        TLIApplication tliApp = new TLIApplication() { ResolveAliases = false }; //Setting ResolveAliases to true has the odd side-effect of resolving enum types to the hidden version in Microsoft Scripting Runtime
        List<TypeLibInfo> tlis = new List<TypeLibInfo>();
        Dictionary<string, List<string>> interfaceToCoClassMapping = new Dictionary<string, List<string>>();
        Dictionary<TSSimpleType, string> enumerableCollectionItemMapping = new Dictionary<TSSimpleType, string>();

        ILookup<string, InterfaceInfo> allInterfaces = null;
        Dictionary<string, RecordInfo> allRecords = null;
        Dictionary<string, IntrinsicAliasInfo> allAliases = null;
        Dictionary<string, UnionInfo> allUnions = null;
        int currentTliCount = 0;

        private void AddTLI(TypeLibInfo tli) {
            if (tlis.Any(x => x.IsSameLibrary(tli))) { return; }
            tlis.Add(tli);
            tli.CoClasses.Cast().GroupBy(x => x.DefaultInterface?.Name ?? "", (key, grp) => KVP(key, grp.Select(x => x.Name))).ForEachKVP((interfaceName, coclasses) => {
                var fullInterfaceName = $"{tli.Name}.{interfaceName}";
                var current = Enumerable.Empty<string>();
                interfaceToCoClassMapping.IfContainsKey(fullInterfaceName, val => current = val);
                interfaceToCoClassMapping[fullInterfaceName] = coclasses.Concat(current).OrderBy(x => x.StartsWith("_")).Select(x => $"{tli.Name}.{x}").ToList();
            });

            for (int i = 0; i < tlis.Count; i++) {  //don't use foreach here, as additional libraries might have been added in the meantime
                var name = tlis[i].Name;
                if (NSSet.Namespaces.ContainsKey(name)) { continue; }
                var toAdd = ToNamespace(tlis[i]);
                if (NSSet.Namespaces.ContainsKey(name)) { continue; } //because the current tli might have been already added, as part of ToNamespace
                NSSet.Namespaces.Add(toAdd.Name, toAdd);
            }
        }

        private void GenerateNSSetParts() {
            var undefinedTypes = NSSet.GetUndefinedTypes();
            if (undefinedTypes.Any()) {
                bool foundTypes;
                do {
                    foundTypes = false;
                    if (currentTliCount != tlis.Count) {
                        //a previously unused external type was discovered, adding to the list of TypeLibInfos
                        allInterfaces = tlis.SelectMany(x => x.Interfaces.Cast()).ToLookup(x => $"{x.Parent.Name}.{x.Name}");
                        allRecords = tlis.SelectMany(x => x.Records.Cast()).ToDictionary(x => $"{x.Parent.Name}.{x.Name}");
                        allAliases = tlis.SelectMany(x => x.IntrinsicAliases.Cast()).ToDictionary(x => $"{x.Parent.Name}.{x.Name}");
                        allUnions = tlis.SelectMany(x => x.Unions.Cast()).ToDictionary(x => $"{x.Parent.Name}.{x.Name}");
                        currentTliCount = tlis.Count;
                    }
                    undefinedTypes.ForEach(s => {
                        var ns = NSSet.Namespaces[s.Split('.')[0]];

                        //go pattern matching!!!!
                        if (allInterfaces.IfContainsKey(s, grp => grp.Select(ToTSInterfaceDescription).AddRangeTo(ns.Interfaces))
                            || allRecords.IfContainsKey(s, x => ns.Interfaces.Add(ToTSInterfaceDescription(x)))
                            || allAliases.IfContainsKey(s, x => ns.Aliases.Add(ToTypeAlias(x)))
                            || allUnions.IfContainsKey(s, x => ns.Aliases.Add(ToTypeAlias(x)))
                        ) {
                            foundTypes = true;
                        } else if (Debugger.IsAttached) {
                            Debugger.Break();
                        }
                    });

                    undefinedTypes = NSSet.GetUndefinedTypes();
                } while (undefinedTypes.Any() && foundTypes);
            }

            NSSet.Namespaces.ForEachKVP((name, ns) => {
                ns.GetUsedTypes().Select(x => x.Split('.')).Where(parts =>
                    parts.Length > 1  //exclude built-in types (without '.')
                    && parts[0] != name)
                    .Select(parts => parts[0]).AddRangeTo(ns.Dependencies);

                var enumerables = enumerableCollectionItemMapping.WhereKVP((collectionType, itemType) => collectionType.Namespace == name).ToList();
                if (enumerables.Any()) {
                    var enumerable = new TSInterfaceDescription();
                    enumerables.Select(ToEnumeratorConstructorDescription).AddRangeTo(enumerable.Constructors);
                    ns.GlobalInterfaces["EnumeratorConstructor"] = enumerable;
                }
            });
        }

        public void AddFromRegistry(string tlbid, short? majorVersion = null, short? minorVersion = null, int? lcid = null) {
            var tlb = TypeLibDetails.FromRegistry.Value.Where(x =>
                x.TypeLibID == tlbid
                && (majorVersion == null || x.MajorVersion == majorVersion)
                && (minorVersion == null || x.MinorVersion == minorVersion)
                && (lcid == null || x.LCID == lcid)).OrderByDescending(x => x.MajorVersion).ThenByDescending(x => x.MinorVersion).ThenBy(x => x.LCID).First();
            TypeLibInfo toAdd;
            try {
                toAdd = tliApp.TypeLibInfoFromRegistry(tlb.TypeLibID, tlb.MajorVersion, tlb.MinorVersion, tlb.LCID);
            } catch (Exception) {
                return;
            }
            AddTLI(toAdd);
            GenerateNSSetParts();
        }

        public void AddFromFile(string filename) {
            var toAdd = tliApp.TypeLibInfoFromFile(filename);
            AddTLI(toAdd);
            GenerateNSSetParts();
        }

        public TSNamespaceSet NSSet { get; } = new TSNamespaceSet();
    }
}