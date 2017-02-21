using System;
using System.Linq;
using System.Collections.Generic;
using TsActivexGen.Util;

namespace TsActivexGen {
    /// <summary>Describes namespace+name types, as well as built-in and literal types</summary>
    public class TSSimpleType : EqualityBase<TSSimpleType>, ITSType {
        public static TSSimpleType Any = new TSSimpleType { FullName = "any" };
        public string FullName { get; set; }
        public string Namespace {
            get {
                if (IsLiteralType) { return ""; }
                var parts = FullName.Split('.');
                if (parts.Length == 1) { return ""; }
                return parts[0];
            }
        }
        public string NameOnly => Functions.NameOnly(FullName);
        public string Comment { get; set; }
        public bool IsLiteralType => Functions.IsLiteralTypeName(FullName);
        public string GenericParameter => Functions.GenericParameter(FullName);

        public List<KeyValuePair<string, string>> JsDoc { get; } = new List<KeyValuePair<string, string>>();

        public override bool Equals(TSSimpleType other) => FullName == other?.FullName;
        public override int GetHashCode() => FullName.GetHashCode();
        public override bool Equals(object other) => base.Equals(other);

        public static bool operator ==(TSSimpleType x, TSSimpleType y) => OperatorEquals(x, y);
        public static bool operator !=(TSSimpleType x, TSSimpleType y) => !OperatorEquals(x, y);

        public string[] TypeParts() => new string[] { GenericParameter ?? FullName };

        public TSSimpleType(string fullName = null) {
            FullName = fullName;
        }
    }

    public class TSTupleType : ITSType {
        public List<ITSType> Members { get; } = new List<ITSType>();
        public string[] TypeParts() => Members.SelectMany(x => x.TypeParts()).ToArray();
    }

    public class TSObjectType : ITSType {
        public Dictionary<string, ITSType> Members { get; } = new Dictionary<string, ITSType>();
        public string[] TypeParts() => Members.Values.SelectMany(x => x.TypeParts()).ToArray();
    }

    public class TSFunctionType : ITSType {
        public TSMemberDescription FunctionDescription { get; set; }
        public string[] TypeParts() => FunctionDescription.TypeParts();
    }
}
