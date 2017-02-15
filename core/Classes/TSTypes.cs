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
        public string RelativeName(string currentNamespace) => Functions.RelativeName(FullName, currentNamespace);
        public string NameOnly => Functions.NameOnly(FullName);
        public string Comment { get; set; }
        public bool IsLiteralType => Functions.IsLiteralTypeName(FullName);
        public string GenericParameter => Functions.GenericParameter(FullName);

        public override bool Equals(TSSimpleType other) => FullName == other?.FullName;
        public override int GetHashCode() => FullName.GetHashCode();
        public override bool Equals(object other) => base.Equals(other);

        public static bool operator ==(TSSimpleType x, TSSimpleType y) => OperatorEquals(x, y);
        public static bool operator !=(TSSimpleType x, TSSimpleType y) => !OperatorEquals(x, y);

        public override string ToString() {
            var comment = Comment;
            if (!comment.IsNullOrEmpty()) { comment = $" /*{comment}*/"; }
            return $"{FullName}{comment}";
        }

        public TSSimpleType(string fullName = null) {
            FullName = fullName;
        }
    }

    public class TSArrayType : ITSType {
        public List<ITSType> Members { get; } = new List<ITSType>();
    }

    public class TSObjectType : ITSType {
        public Dictionary<string, ITSType> Members { get; } = new Dictionary<string, ITSType>();
    }

    public class TSFunctionType : ITSType {
        public TSMemberDescription FunctionDescription { get; set; }
    }
}
