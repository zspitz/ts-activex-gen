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
    public class TlbInf32Generator : ITSNamespaceGenerator {
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

        private TSTypeName GetTypeName(VarTypeInfo vti, object value = null) {
            var ret = new TSTypeName();
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

        //We're assuming all members are constant
        //In any case, in JS there is no way to access module members
        private KeyValuePair<string, TSNamespaceDescription> ToTSNamespaceDescription(ConstantInfo c) {
            var ret = new TSNamespaceDescription();
            c.Members.Cast().Select(x => KVP(x.Name, AsString((object)x.Value))).AddRangeTo(ret.Members);
            return KVP($"{c.Parent.Name}.{c.Name}", ret);
        }

        private KeyValuePair<string, TSEnumDescription> ToTSEnumDescription(ConstantInfo c) {
            var ret = new TSEnumDescription();
            c.Members.Cast().Select(x => {
                var oValue = (object)x.Value;
                var typename = GetTypeName(x.ReturnType, oValue);
                if (ret.Typename == null) {
                    ret.Typename = typename;
                } else if (ret.Typename != typename) {
                    throw new InvalidOperationException("Multiple types in enum"); //this should really be handled as a module with constants; but it's irrelevant because Javascript has no way to access module members
                }
                return KVP(x.Name, AsString(oValue));
            }).AddRangeTo(ret.Members);
            return KVP($"{c.Parent.Name}.{c.Name}", ret);
        }

        private KeyValuePair<string, TSParameterDescription> ToTSParameterDescription(ParameterInfo p, bool isRest) {
            var ret = new TSParameterDescription();
            ret.Typename = GetTypeName(p.VarTypeInfo);
            if (isRest) {
                ret.ParameterType = Rest;
            } else if (p.Optional || p.Default) {
                ret.ParameterType = Optional;
            } else {
                ret.ParameterType = Standard;
            }
            var name = p.Name;
            return KVP(p.Name, ret);
        }

        private List<KeyValuePair<string, TSParameterDescription>> GetSingleParameterList(IEnumerable<MemberInfo> members) {
            var parameterLists = members.Select(m => {
                var parameterCount = m.Parameters.Count;
                return m.Parameters.Cast().Select((p, index) => {
                    bool isRest = m.Parameters.OptionalCount == -1 && index == parameterCount - 1;
                    return ToTSParameterDescription(p, isRest);
                }).ToList();
            }).Distinct(new TSParameterListComparer()).ToList();
            if (parameterLists.Count > 1) {
                throw new InvalidOperationException("Unable to parse different parameter lists");
            }
            return parameterLists.FirstOrDefault();
        }

        private TSMemberDescription GetMemberDescriptionForName(IEnumerable<MemberInfo> members) {
            var ret = new TSMemberDescription();

            var parameterList = GetSingleParameterList(members);
            var paramType = Standard;
            parameterList.ForEachKVP((name, p) => {
                if (p.ParameterType==Standard && paramType == Optional) { p.ParameterType = Optional; }
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

            ret.ReturnTypename = GetTypeName(members.First().ReturnType);
            if (hasSetter && parameterList.Any()) {
                ret.Comment = "Also has setter with parameters";
            }
            return ret;
        }

        private Dictionary<string, TSMemberDescription> GetMembers(Members members, ref string enumerableType) {
            var ret = members.Cast().Where(x => !x.IsRestricted() && x.Name!="_NewEnum").ToLookup(x => x.Name).Select(grp => KVP(grp.Key, GetMemberDescriptionForName(grp))).ToDictionary();

            var enumerableType1 = enumerableType; //because ref parameters cannot be used within lambda expressions
            members.Cast().ToLookup(x => x.Name).IfContainsKey("_NewEnum", mi => {
                ret.IfContainsKey("Item", itemMI => enumerableType1 = itemMI.ReturnTypename.FullName);
            });
            enumerableType = enumerableType1;

            return ret;
        }

        private KeyValuePair<string, TSInterfaceDescription> ToTSInterfaceDescription(InterfaceInfo i) {
            var ret = new TSInterfaceDescription();
            string enumerableType=null;
            GetMembers(i.Members, ref enumerableType).AddRangeTo(ret.Members);
            ret.EnumerableType = enumerableType;
            return KVP($"{i.Parent.Name}.{i.Name}", ret);
        }

        private KeyValuePair<string, TSInterfaceDescription> ToTSInterfaceDescription(CoClassInfo c) {
            var ret = new TSInterfaceDescription();
            string enumerableType = null;
            GetMembers(c.DefaultInterface.Members, ref enumerableType).AddRangeTo(ret.Members);
            ret.EnumerableType = enumerableType;
            ret.IsActiveXCreateable = c.IsCreateable();
            return KVP($"{c.Parent.Name}.{c.Name}", ret);
        }

        private KeyValuePair<string, TSInterfaceDescription> ToTSInterfaceDescription(RecordInfo r) {
            var ret = new TSInterfaceDescription();
            string enumerableType = null; //this value will be ignored for RecordInfo
            GetMembers(r.Members, ref enumerableType).AddRangeTo(ret.Members);
            return KVP($"{r.Parent.Name}.{r.Name}", ret);
        }

        private TSNamespace ToNamespace(TypeLibInfo tli) {
            var ret = new TSNamespace() { Name = tli.Name };
            tli.Constants.Cast().Where(x => x.TypeKind != TKIND_MODULE).Select(ToTSEnumDescription).AddRangeTo(ret.Enums);
            tli.Constants.Cast().Where(x => x.TypeKind == TKIND_MODULE).Select(ToTSNamespaceDescription).AddRangeTo(ret.Namespaces);
            tli.CoClasses.Cast().Select(ToTSInterfaceDescription).AddRangeTo(ret.Interfaces);

            //TODO do we need to look at ImpliedInterfaces? Should we use GetMembers instead of manually iterating over Members?

            if (tli.Declarations.Cast().Any()) {
                //not sure what these are, if they are accessible from JScript
                //there is one in the Excel object library
            }
            if (tli.Unions.Cast().Any()) {
                //not sure what these are, if they are accessible from JScript
                //there are a few in the DirectX transforms library
            }

            return ret;
        }

        private KeyValuePair<string, TSTypeName> ToTypeAlias(IntrinsicAliasInfo ia)  => KVP($"{ia.Parent.Name}.{ ia.Name}", GetTypeName(ia.ResolvedType));
        private KeyValuePair<string, TSTypeName> ToTypeAlias(UnionInfo u) => KVP($"{u.Parent.Name}.{u.Name}", TSTypeName.Any);

        TLIApplication tliApp = new TLIApplication() { ResolveAliases = false }; //Setting ResolveAliases to true has the odd side-effect of resolving enum types to the hidden version in Microsoft Scripting Runtime
        List<TypeLibInfo> tlis = new List<TypeLibInfo>();
        Dictionary<string, List<string>> interfaceToCoClassMapping = new Dictionary<string, List<string>>();

        private void AddTLI(TypeLibInfo tli, string dependencySource=null) {
            if (tlis.Any(x => x.IsSameLibrary(tli))) { return; }
            tlis.Add(tli);
            tli.CoClasses.Cast().GroupBy(x => x.DefaultInterface?.Name ?? "", (key, grp) => KVP(key, grp.Select(x => x.Name))).ForEachKVP((interfaceName, coclasses) => {
                var fullInterfaceName = $"{tli.Name}.{interfaceName}";
                if (interfaceToCoClassMapping.ContainsKey(fullInterfaceName)) { throw new Exception($"Interface {fullInterfaceName} already exists in dictionary"); }
                interfaceToCoClassMapping[fullInterfaceName] = coclasses.OrderBy(x => x.StartsWith("_")).Select(x => $"{tli.Name}.{x}").ToList();
            });
        }

        public void AddFromRegistry(string tlbid, short? majorVersion=null, short? minorVersion=null, int? lcid=null) {
            var tlb = TypeLibDetails.FromRegistry.Value.Where(x =>
                x.TypeLibID == tlbid
                && (majorVersion == null || x.MajorVersion == majorVersion)
                && (minorVersion == null || x.MinorVersion == minorVersion)
                && (lcid == null || x.LCID == lcid)).OrderByDescending(x => x.MajorVersion).ThenByDescending(x => x.MinorVersion).ThenBy(x => x.LCID).First();
            var toAdd = tliApp.TypeLibInfoFromRegistry(tlb.TypeLibID, tlb.MajorVersion, tlb.MinorVersion, tlb.LCID);
            AddTLI(toAdd);
        }

        public void AddFromFile(string filename) {
            var toAdd = tliApp.TypeLibInfoFromFile(filename);
            AddTLI(toAdd);
        }

        public TSNamespaceSet Generate() {
            var ret = new TSNamespaceSet();
            for (int i = 0; i < tlis.Count; i++) {
                var toAdd = ToNamespace(tlis[i]);
                ret.Namespaces.Add(toAdd.Name, toAdd);
            }

            var undefinedTypes = ret.GetUndefinedTypes();
            if (undefinedTypes.Any()) {
                ILookup<string, InterfaceInfo> allInterfaces = null;
                Dictionary<string, RecordInfo> allRecords = null;
                Dictionary<string, IntrinsicAliasInfo> allAliases = null;
                Dictionary<string, UnionInfo> allUnions = null;
                var currentTliCount = 0;
                bool foundTypes;
                do {
                    foundTypes = false;
                    if (currentTliCount != tlis.Count) {
                        //a previously unused external type was discovered, adding to the list of TypeLibInfos
                        allInterfaces = tlis.SelectMany(tli => tli.Interfaces.Cast()).ToLookup(x => $"{x.Parent.Name}.{x.Name}");
                        allRecords = tlis.SelectMany(tli => tli.Records.Cast()).ToDictionary(x => $"{x.Parent.Name}.{x.Name}");
                        allAliases = tlis.SelectMany(tli => tli.IntrinsicAliases.Cast()).ToDictionary(x => $"{x.Parent.Name}.{x.Name}");
                        allUnions = tlis.SelectMany(tli => tli.Unions.Cast()).ToDictionary(x => $"{x.Parent.Name}.{x.Name}");
                        currentTliCount = tlis.Count;
                    }
                    undefinedTypes.ForEach(s => {
                        var ns = ret.Namespaces[s.Split('.')[0]];

                        //go pattern matching!!!!
                        if (allInterfaces.IfContainsKey(s, grp => grp.Select(ToTSInterfaceDescription).AddRangeTo( ns.Interfaces))
                            || allRecords.IfContainsKey(s, x => ns.Interfaces.Add(ToTSInterfaceDescription(x))) 
                            || allAliases.IfContainsKey(s, x => ns.Aliases.Add(ToTypeAlias(x)))
                            || allUnions.IfContainsKey(s, x => ns.Aliases.Add(ToTypeAlias(x)))
                        ) {
                            foundTypes = true;
                        } else if (Debugger.IsAttached) {
                            Debugger.Break();
                        }
                    });

                    undefinedTypes = ret.GetUndefinedTypes();
                } while (undefinedTypes.Any() && foundTypes);
            }

            ret.Namespaces.ForEachKVP((name, ns) => {
                ns.GetUsedTypes().Select(x => x.Split('.')).Where(parts => 
                    parts.Length > 1  //exclude built-in types (without '.')
                    && parts[0] != name).Select(parts => parts[0]).AddRangeTo(ns.Depndencies);
            });

            return ret;
        }
    }
}