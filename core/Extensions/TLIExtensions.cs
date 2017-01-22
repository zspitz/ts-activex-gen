using System;
using System.Collections.Generic;
using System.Linq;
using TLI;
using static TLI.TliVarType;
using static TLI.DescKinds;
using static TLI.InvokeKinds;
using System.Diagnostics;

namespace TsActivexGen.Util {
    public static class TLIExtensions {
        //using the LINQ Cast method fails; apprently because the source collections cannot be casted to IEnumerable (???)

        public static IEnumerable<CoClassInfo> Cast(this CoClasses src) {
            foreach (CoClassInfo item in src) {
                yield return item;
            }
        }
        public static IEnumerable<ConstantInfo> Cast(this Constants src) {
            foreach (ConstantInfo item in src) {
                yield return item;
            }
        }
        public static IEnumerable<DeclarationInfo> Cast(this Declarations src) {
            foreach (DeclarationInfo item in src) {
                yield return item;
            }
        }
        public static IEnumerable<InterfaceInfo> Cast(this Interfaces src) {
            foreach (InterfaceInfo item in src) {
                yield return item;
            }
        }
        public static IEnumerable<IntrinsicAliasInfo> Cast(this IntrinsicAliases src) {
            foreach (IntrinsicAliasInfo item in src) {
                yield return item;
            }
        }
        public static IEnumerable<MemberInfo> Cast(this Members src) {
            foreach (MemberInfo item in src) {
                yield return item;
            }
        }
        public static IEnumerable<ParameterInfo> Cast(this Parameters src) {
            foreach (ParameterInfo item in src) {
                yield return item;
            }
        }
        public static IEnumerable<RecordInfo> Cast(this Records src) {
            foreach (RecordInfo item in src) {
                yield return item;
            }
        }
        public static IEnumerable<TypeInfo> Cast(this TypeInfos src) {
            foreach (TypeInfo item in src) {
                yield return item;
            }
        }
        public static IEnumerable<UnionInfo> Cast(this Unions src) {
            foreach (UnionInfo item in src) {
                yield return item;
            }
        }

        public static bool IsRestricted(this MemberInfo mi) {
            if (mi.DescKind == DESCKIND_FUNCDESC) {
                if (((FuncFlags)mi.AttributeMask).HasFlag(FuncFlags.FUNCFLAG_FRESTRICTED)) { return true; }
            }
            return false;
        }
        public static bool IsProperty(this MemberInfo mi) {
            return mi.InvokeKind.In(INVOKE_PROPERTYGET, INVOKE_PROPERTYPUT, INVOKE_PROPERTYPUTREF);
        }
        public static bool IsCreateable(this CoClassInfo cc) {
            return (cc.AttributeMask & 2) == 2;
        }
        public static bool IsInvokeable(this MemberInfo mi) {
            return mi.InvokeKind.In(INVOKE_FUNC, INVOKE_EVENTFUNC);
        }

        public static HashSet<TliVarType> SplitValues(this TliVarType varType) {
            var ret = new List<TliVarType>();
            if (varType == VT_EMPTY) {
                ret.Add(VT_EMPTY);
            } else {
                var enumValues = Enum.GetValues(typeof(TliVarType)).Cast<TliVarType>().Where(x=>x != VT_EMPTY).OrderedDescending();
                foreach (var v in enumValues) {
                    if ((varType & v) == v) {
                        ret.Add(v);
                        varType -= v;
                    }
                }
            }
            return ret.ToHashSet();
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
VT_EMPTY	0
 */

        public static List<object> Debug(this CoClasses coclasses) {
            return coclasses.Cast().Select(x => {
                Array strings;
                var i = x.AttributeStrings[out strings];
                return new {
                    x.Name,
                    x.AttributeMask,
                    AttributeStrings = strings?.Cast<string>().Joined(),
                    ITypeInfo = x.ITypeInfo.ToString(),
                    x.TypeKind,
                    x.TypeKindString,
                    DefaultInterface = x.DefaultInterface.Debug()
                };
            }).ToObjectList();
        }
        public static List<object> Debug(this Members members) {
            return members.Cast().Debug();
        }
        public static List<object> Debug(this IEnumerable<MemberInfo> members) {
            return members.Select(x => {
                Array strings;
                var i = x.AttributeStrings[out strings];
                return new {
                    x.Name,
                    Restricted = x.IsRestricted(),
                    x.AttributeMask,
                    AttributeStrings = strings?.Cast<string>().Joined(),
                    x.CallConv,
                    x.DescKind,
                    x.InvokeKind,
                    ParameterCount = x.Parameters.Count,
                    Parameters = x.Parameters.Debug(),
                    ReturnType = x.ReturnType.Debug()
                };
            }).OrderBy(x => x.Restricted).ThenBy(x => x.Name).ToObjectList();
        }
        public static List<object> Debug(this Interfaces interfaces) {
            return interfaces.Cast().Select(Debug).ToObjectList();
        }
        public static object Debug(this InterfaceInfo ii) {
            Array strings;
            var i = ii.AttributeStrings[out strings];
            return new {
                ii.Name,
                ii.AttributeMask,
                AttributeStrings = strings?.Cast<string>().Joined(),
                ii.TypeKind,
                ii.TypeKindString,
                ImpliedInterfaces = ii.ImpliedInterfaces.Debug(),
                Members = ii.Members.Debug()
            };
        }
        public static object Debug(this TypeInfo t) {
            Array strings;
            var i = t.AttributeStrings[out strings];
            VarTypeInfo resolvedType=null;
            try {
                resolvedType = t.ResolvedType;
            } catch (Exception) { }
            return new {
                t.Name,
                t.AttributeMask,
                DefaultInterface = t.DefaultInterface?.Debug(),
                ResolvedType = resolvedType?.Debug(),
                t.TypeKind
            };
        }
        public static object Debug(this VarTypeInfo vt) {
            if (vt == null) { return null; }
            //var Typename = vt.GetTypeName();
            return new {
                //Typename,
                vt.IsExternalType,
                vt.TypeLibInfoExternal?.Name,
                vt.VarType//,
                //TypeInfo = vt.TypeInfo?.Debug()
            };
        }
        public static List<object> Debug(this Parameters parameters) {
            return parameters.Cast().Select(p => new {
                p.Name,
                p.Default,
                p.Optional,
                VarTypeInfo = p.VarTypeInfo.Debug()
            }).OrderBy(x => x.Name).ToObjectList();
        }
        public static List<object> Debug(this IntrinsicAliases aliases) {
            return aliases.Cast().Select(ia => {
                Array strings;
                var i = ia.AttributeStrings[out strings];
                return new {
                    ia.Name,
                    ia.AttributeMask,
                    AttributeString = strings?.Cast<string>().Joined(),
                    ResolvedType = ia.ResolvedType.Debug(),
                    ia.TypeKind,
                    ia.TypeKindString
                };
            }).ToObjectList();
        }
    }
}
