using System;
using System.Collections.Generic;
using TsActivexGen.Util;
using System.Linq;

namespace TsActivexGen {
    public abstract class EqualityBase<T> : IEquatable<T> where T : class {
        public abstract bool Equals(T other);
        public override bool Equals(object other) {
            if (ReferenceEquals(null, other)) { return false; }
            if (ReferenceEquals(this, other)) { return true; }
            if (GetType() != other.GetType()) { return false; }
            return Equals(other as T);
        }
        public abstract override int GetHashCode();

        public static bool OperatorEquals(T x, T y) {
            if (ReferenceEquals(x, null)) { return ReferenceEquals(y, null); }
            return x.Equals(y);
        }
    }

    public class TSTypeName : EqualityBase<TSTypeName> {
        public string Name { get; set; }
        public string Comment { get; set; }

        public override bool Equals(TSTypeName other) {
            return Name == other?.Name;
        }
        public override int GetHashCode() {
            return Name.GetHashCode();
        }
        public override bool Equals(object other) {
            return base.Equals(other);
        }

        public static bool operator ==(TSTypeName x, TSTypeName y) {
            return OperatorEquals(x, y);
        }
        public static bool operator !=(TSTypeName x, TSTypeName y) {
            return !OperatorEquals(x, y);
        }

        public override string ToString() {
            var comment = Comment;
            if (!comment.IsNullOrEmpty()) { comment = $" /*{comment}*/"; }
            return $"{Name}{comment}";
        }
        
        
    }

    public class TSEnumDescription {
        public TSTypeName Typename { get; set; }
        public Dictionary<string, string> Members { get; set; } //values -> string representation of value
    }

    public class TSParameterDescription : EqualityBase<TSParameterDescription> {
        public TSTypeName Typename { get; set; }
        public TSParameterType ParameterType { get; set; }

        public override bool Equals(TSParameterDescription other) {
            return Typename == other.Typename
                && ParameterType == other.ParameterType;
        }
        public override int GetHashCode() {
            unchecked {
                int hash = 17;
                hash = hash * 486187739 + Typename.GetHashCode();
                hash = hash * 486187739 + ParameterType.GetHashCode();
                return hash;
            }
        }
        public override bool Equals(object other) {
            return base.Equals(other);
        }

        public static bool operator ==(TSParameterDescription x, TSParameterDescription y) {
            return OperatorEquals(x, y);
        }
        public static bool operator !=(TSParameterDescription x, TSParameterDescription  y) {
            return !OperatorEquals(x, y);
        }
    }

    public class TSMemberDescription {
        public List<KeyValuePair<string, TSParameterDescription>> Parameters { get; set; } //(null means a property, empty means empty parameter list); this mut be a list, becaus parameter order is important
        public TSTypeName ReturnTypename { get; set; }
        public string Comment { get; set; }
    }

    public class TSInterfaceDescription {
        //TODO This functionality is specific to ActiveX definition creation, and should really be in the TlbInf32Generator class
        public bool IsActiveXCreateable { get; set; }
        public Dictionary<string, TSMemberDescription> Members { get; set; }
    }

    public class TSNamespace {
        public string Name { get; set; }
        public Dictionary<string, TSEnumDescription> Enums { get; set; }
        public Dictionary<string, TSInterfaceDescription> Interfaces { get; set; }
        public HashSet<string> GetUsedTypes() {
            return Interfaces.SelectKVP((name, i) => i.Members.SelectKVP((memberName, m) => m.ReturnTypename.Name)).SelectMany().ToHashSet();
        }
        public HashSet<string> GetKnownTypes() {
            var ret = new[] { "any", "void", "boolean", "string", "number", "undefined", "null", "never", "VarDate" }.ToHashSet();
            Enums.Keys.AddRangeTo(ret);
            Interfaces.Keys.AddRangeTo(ret);
            return ret;
        }
        public HashSet<string> GetUndefinedTypes() {
            var ret = GetUsedTypes();
            ret.ExceptWith(GetKnownTypes());
            return ret;
        }
    }

    public class TSParameterListComparer : IEqualityComparer<List<KeyValuePair<string, TSParameterDescription>>> {
        public bool Equals(List<KeyValuePair<string, TSParameterDescription>> x, List<KeyValuePair<string, TSParameterDescription>> y) {
            if (x==null) { return y == null; }
            return x.SequenceEqual(y);
        }
        public int GetHashCode(List<KeyValuePair<string, TSParameterDescription>> obj) {
            unchecked {
                return obj.Aggregate(17, (hash, x) => hash * 486187739 + x.GetHashCode());
            }
        }
    }
}

