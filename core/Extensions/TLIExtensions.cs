﻿using System;
using System.Collections.Generic;
using System.Linq;
using TLI;
using static TLI.TliVarType;
using static TLI.DescKinds;
using static TLI.InvokeKinds;

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

        internal static TSTypeName GetTypeName(this VarTypeInfo vti, Dictionary<string, List<string>> mapping = null, object value = null) {
            //TODO this should be in TlbInf32Generator class; then it will have access to the mapping
            var ret = new TSTypeName();
            switch (vti.VarType) {
                case VT_I1:
                case VT_I2:
                case VT_I4:
                case VT_R4:
                case VT_R8:
                case VT_UI1:
                case VT_UI2:
                case VT_UI4:
                case VT_UI8:
                case VT_CY:
                case VT_DECIMAL:
                case VT_INT:
                case VT_UINT:
                    ret.Name = "number";
                    break;
                case VT_BSTR:
                    ret.Name = "string";
                    break;
                case VT_BOOL:
                    ret.Name = "boolean";
                    break;
                case VT_VOID:
                case VT_HRESULT:
                    ret.Name = "void";
                    break;
                case VT_DATE:
                    ret.Name = "VarDate";
                    break;
                case VT_EMPTY:
                    var ti = vti.TypeInfo;
                    ret.Name = ti.Name;
                    mapping.IfContainsKey(ret.Name, val => ret.Name = val.FirstOrDefault());
                    break;
                case VT_VARIANT:
                    ret.Name = "any";
                    break;
                default:
                    var external = vti.IsExternalType ? " (external)" : "";
                    ret.Comment = $"{vti.VarType.ToString()}{external}";
                    ret.Name = "any";
                    break;
            }
            if (ret.Name == "any" && value != null) {
                var t = value.GetType();
                if (t==typeof(string)) {
                    ret.Name = "string";
                } else if (t.IsNumeric()) {
                    ret.Name = "number";
                }
            }
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