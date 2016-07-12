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
        //each collection type needs its own Cast extension method
        //cannot be made generic because the source collections cannot be casted to IEnumerable (???)

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
VT_DISPATCH	9
VT_NULL	1
VT_EMPTY	0
 */



        internal static TSTypeName GetTypeName(this VarTypeInfo vti, Dictionary<string, List<string>> mapping = null, object value = null) {
            //TODO this should be in TlbInf32Generator class; then it will have access to the mapping
            var ret = new TSTypeName();
            var splitValues = vti.VarType.SplitValues();
            var isArray = splitValues.ContainsAny(VT_VECTOR,VT_ARRAY);
            if (splitValues.ContainsAny(VT_I1, VT_I2, VT_I4, VT_I8, VT_R4, VT_R8, VT_UI1, VT_UI2, VT_UI4, VT_UI8, VT_CY, VT_DECIMAL, VT_INT, VT_UINT)) {
                ret.Name = "number";
            } else if (splitValues.ContainsAny(VT_BSTR, VT_LPSTR, VT_LPWSTR)) {
                ret.Name = "string";
            } else if (splitValues.ContainsAny(VT_BOOL)) {
                ret.Name = "boolean";
            } else if (splitValues.ContainsAny(VT_VOID, VT_HRESULT)) {
                ret.Name = "void";
            } else if (splitValues.ContainsAny(VT_DATE)) {
                ret.Name = "VarDate";
            } else if (splitValues.ContainsAny(VT_EMPTY)) {
                var ti = vti.TypeInfo;
                ret.Name = ti.Name;
                mapping.IfContainsKey(ret.Name, val => ret.Name = val.FirstOrDefault());
            } else if (splitValues.ContainsAny(VT_VARIANT)) {
                ret.Name = "any";
            } else {
                if (vti.TypeInfo != null) {
                    var testName = vti.TypeInfo.Name;
                    Debugger.Break();
                }
                var external = vti.IsExternalType ? " (external)" : "";
                ret.Comment = $"{vti.VarType.ToString()}{external}";
                ret.Name = "any";
            }

            if (ret.Name == "any" && value != null) {
                var t = value.GetType();
                if (t == typeof(string)) {
                    ret.Name = "string";
                } else if (t.IsNumeric()) {
                    ret.Name = "number";
                }
            }
            if (isArray) { ret.Name += "[]"; }
            return ret;
        }

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
        public static object Debug(this VarTypeInfo vt) {
            if (vt == null) { return null; }
            var Typename = vt.GetTypeName();
            return new {
                Typename,
                vt.IsExternalType,
                vt.TypeLibInfoExternal?.Name,
                vt.VarType
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
