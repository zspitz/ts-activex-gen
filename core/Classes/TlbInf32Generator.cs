using System;
using System.Collections.Generic;
using System.Linq;
using TLI;
using TsActivexGen.Util;
using static TsActivexGen.Util.Functions;
using static TsActivexGen.TSParameterType;
using static TLI.InvokeKinds;
using static TLI.TypeKinds;

namespace TsActivexGen {
    public class TlbInf32Generator : ITSNamespaceGenerator {
        static string AsString(object value) {
            var t = value.GetType().UnderlyingIfNullable();
            if (t == typeof(string)) {
                return $"\'{(value as string).Replace("'", "\\'")}\'";
            } else if (t.IsNumeric()) {
                return $"{value}";
            } else if (t==typeof(bool)) {
                return (bool)value ? "true" : "false";
            }
            throw new Exception($"Unable to generate string representation of value '{value}' of type '{t.Name}'");
        }

        string tlbid;
        short majorVersion;
        short minorVersion;
        int lcid;

        string filePath;

        TypeLibInfo tli;
        Dictionary<string, List<string>> interfaceToCoClassMapping;

        public static TlbInf32Generator CreateFromRegistry(string tlbid, short majorVersion, short minorVersion, int lcid) {
            ////TODO include logic here to figure out majorVersion/minorVersion/lcid even when not passed in
            return new TlbInf32Generator() {
                tlbid = tlbid,
                majorVersion = majorVersion,
                minorVersion = minorVersion,
                lcid = lcid
            };
        }

        public static TlbInf32Generator CreateFromFile(string filename) {
            return new TlbInf32Generator() {
                filePath = filename
            };
        }

        private TlbInf32Generator() { }

        private void GetTypeLibInfo() {
            var tliApp = new TLIApplication() { ResolveAliases = false }; //Setting ResolveAliases to true has the odd side-effect of resolving enum types to the hidden version in Microsoft Scripting Runtime
            if (!tlbid.IsNullOrEmpty()) {
                tli = tliApp.TypeLibInfoFromRegistry(tlbid, majorVersion, minorVersion, lcid);
            } else if (!filePath.IsNullOrEmpty()) {
                tli = tliApp.TypeLibInfoFromFile(filePath);
            } else {
                throw new ArgumentException();
            }
            interfaceToCoClassMapping = tli.CoClasses.Cast().GroupBy(x => x.DefaultInterface?.Name, (key, grp) => KVP(key ?? "", grp.Select(x => x.Name).OrderBy(x => x.StartsWith("_")).ToList())).ToDictionary();
        }

        //We're assuming all members are constant
        //In any case, in JS there is no way to access module members
        private KeyValuePair<string,TSNamespaceDescription> ToTSNamespaceDescription(ConstantInfo c) {
            var ret = new TSNamespaceDescription();
            ret.Members = c.Members.Cast().Select( x=> KVP(x.Name,AsString((object)x.Value))).ToDictionary();
            return KVP(c.Name,ret);
        }

        private KeyValuePair<string, TSEnumDescription> ToTSEnumDescription(ConstantInfo c) {
            var ret = new TSEnumDescription();
            //TODO if all the types are the same, then set ret.Typename to that type; otherwise set to null and treat as module with constants
            ret.Members = c.Members.Cast().Select(x => {
                var oValue = (object)x.Value;
                var typename = x.ReturnType.GetTypeName(null, oValue);
                if (ret.Typename == null) {
                    ret.Typename = typename;
                } else if (ret.Typename != typename) {
                    throw new InvalidOperationException("Multiple types in enum");
                }
                return KVP(x.Name, AsString(oValue));
            }).ToDictionary();
            return KVP(c.Name, ret);
        }

        private KeyValuePair<string, TSParameterDescription> ToTSParameterDescription(ParameterInfo p, bool isRest) {
            var ret = new TSParameterDescription();
            ret.Typename = p.VarTypeInfo.GetTypeName(interfaceToCoClassMapping);
            if (isRest) {
                ret.ParameterType = Rest;
            } else if (p.Optional || p.Default) {
                ret.ParameterType = Optional;
            } else {
                ret.ParameterType = Standard;
            }
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

            var memberCount = members.Count();
            bool hasSetter = false;
            if (memberCount > 1) {
                if (!members.All(x => x.IsProperty())) {
                    throw new InvalidOperationException("Unable to parse single name with property and non-property members");
                }
                if (memberCount.In(2, 3)) {
                    //readwrite properties will have multiple members - one getter and one setter
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
            ret.ReadOnly = invokeable || !hasSetter;

            ret.ReturnTypename = members.First().ReturnType.GetTypeName(interfaceToCoClassMapping);
            if (hasSetter && parameterList.Any()) {
                ret.Comment = "Also has setter with parameters";
            }
            return ret;
        }

        private Dictionary<string, TSMemberDescription> GetMembers(Members members) {
            return members.Cast().Where(x => !x.IsRestricted()).ToLookup(x => x.Name).Select(grp => KVP(grp.Key, GetMemberDescriptionForName(grp))).ToDictionary();
        }

        private KeyValuePair<string, TSInterfaceDescription> ToTSInterfaceDescription(InterfaceInfo i) {
            var ret = new TSInterfaceDescription();
            ret.Members = GetMembers(i.Members);
            return KVP(i.Name, ret);
        }

        private KeyValuePair<string, TSInterfaceDescription> ToTSInterfaceDescription(CoClassInfo c) {
            var ret = new TSInterfaceDescription();
            ret.Members = GetMembers(c.DefaultInterface.Members);
            ret.IsActiveXCreateable = c.IsCreateable();
            return KVP(c.Name, ret);
        }

        private KeyValuePair<string, TSInterfaceDescription> ToTSInterfaceDescription(RecordInfo r) {
            var ret = new TSInterfaceDescription();
            ret.Members = GetMembers(r.Members);
            return KVP(r.Name, ret);
        }

        public TSNamespace Generate() {
            GetTypeLibInfo();

            var ret = new TSNamespace() { Name = tli.Name };

            ret.Enums = tli.Constants.Cast().Where(x=>x.TypeKind != TKIND_MODULE).Select(ToTSEnumDescription).ToDictionary();

            ret.Namespaces = tli.Constants.Cast().Where(x => x.TypeKind == TKIND_MODULE).Select(ToTSNamespaceDescription).ToDictionary();

            ret.Interfaces = tli.CoClasses.Cast().Select(ToTSInterfaceDescription).ToDictionary();

            var undefinedTypes = ret.GetUndefinedTypes();
            if (undefinedTypes.Any()) {
                var tliInterfaces = tli.Interfaces.Cast().Select(x => KVP(x.Name, x)).ToDictionary();
                var tliRecords = tli.Records.Cast().Select(x => KVP(x.Name, x)).ToDictionary();
                do {
                    undefinedTypes.Select(s => {
                        if (tliInterfaces.ContainsKey(s)) {
                            return ToTSInterfaceDescription(tliInterfaces[s]);
                        }
                        if (tliRecords.ContainsKey(s)) {
                            return ToTSInterfaceDescription(tliRecords[s]);
                        }
                        throw new InvalidOperationException($"Unable to find type '{s}'.");
                    }).AddRangeTo(ret.Interfaces);
                    undefinedTypes = ret.GetUndefinedTypes();
                } while (undefinedTypes.Any());
            }

            //TODO do we need to look at ImpliedInterfaces? Should we use GetMembers instead of manually iterating over Members?

            //Haven't seen any of these yet; not sure what they even are
            if (tli.Declarations.Cast().Any()) {
                var lst = tli.Declarations.Cast().Select(x => x.Name).ToList();
                throw new NotImplementedException();
            }
            if (tli.Unions.Cast().Any()) {
                throw new NotImplementedException();
            }

            return ret;
        }
    }
}