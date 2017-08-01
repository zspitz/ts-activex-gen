using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TsActivexGen {
    /// <summary>Describes namespace+name types, as well as built-in and literal types</summary>
    public struct TSSimpleType : ITSType {
        public static readonly TSSimpleType Any = new TSSimpleType("any");
        public static readonly TSSimpleType Void = new TSSimpleType("void");
        public static readonly TSSimpleType Undefined = new TSSimpleType("undefined");

        public string FullName { get; }
        public string Namespace {
            get {
                if (IsLiteralType) { return ""; }
                var parts = FullName.Split('.');
                if (parts.Length == 1) { return ""; }
                return parts[0];
            }
        }
        public bool IsLiteralType => Functions.IsLiteralTypeName(FullName);

        private static Regex re = new Regex("^.*<(.*)>$");
        public string GenericParameter {
            get {
                //HACK The only generic types used in this code base are Enumerator<T>, which has a single parameter; this code really should do a full parse, but YAGNI
                if (IsLiteralType) { return null; }
                var match = re.Match(FullName);
                var ret = match.Groups[1].Value;
                if (ret.IsNullOrEmpty()) { return null; }
                return ret;
            }
        }

        public IEnumerable<TSSimpleType> TypeParts() => new TSSimpleType[] { GenericParameter ?? FullName }; //HACK the only generic type in this code base is Enumerator<T>, so the only relevant type parts are the parameters if there are any
        public bool Equals(ITSType other) => other is TSSimpleType x && FullName == x.FullName;

        public TSSimpleType(string fullName = null) => FullName = fullName;

        public static implicit operator string(TSSimpleType t) => t.FullName;
        public static implicit operator TSSimpleType(string s) => new TSSimpleType(s);

        public override string ToString() => FullName;
    }

    public struct TSTupleType : ITSType {
        public List<ITSType> Members { get; }  //TODO Consider using an immutable list

        public IEnumerable<TSSimpleType> TypeParts() => Members.SelectMany(x => x.TypeParts());
        public bool Equals(ITSType other) => other is TSTupleType x && Members.SequenceEqual(x.Members);

        public TSTupleType(IEnumerable<ITSType> members) => Members = members.ToList();
        public TSTupleType(IEnumerable<string> members) => Members = members.Select(x=>new TSSimpleType(x)).Cast<ITSType>().ToList();

        public override string ToString() => "[" + Members.Joined(",") + "]";
    }

    public struct TSObjectType : ITSType {
        public static readonly TSObjectType PlainObject = new TSObjectType(Enumerable.Empty<KeyValuePair<string, (ITSType, bool)>>());

        public Dictionary<string, (ITSType type, bool @readonly)> Members { get; } 

        public bool Equals(ITSType other) => other is TSObjectType x && Members.OrderBy(y=>y.Key).ToList().SequenceEqual(x.Members.OrderBy(y=>y.Key).ToList());
        public IEnumerable<TSSimpleType> TypeParts() => Members.Values.SelectMany(x => x.type.TypeParts());

        public TSObjectType(IEnumerable<KeyValuePair<string, (ITSType type, bool @readonly)>> members) => Members = members.ToDictionary();

        public override string ToString() => $"{Members.Keys.Joined(",",x=>$".{x}")}";
    }

    public struct TSFunctionType : ITSType {
        public TSMemberDescription FunctionDescription { get; }

        public bool Equals(ITSType other) => other is TSFunctionType x && FunctionDescription.Equals(x.FunctionDescription);
        public IEnumerable<TSSimpleType> TypeParts() => FunctionDescription.TypeParts();

        public TSFunctionType(TSMemberDescription fn)  => FunctionDescription = fn;

        public override string ToString() => $"({FunctionDescription.Parameters.Keys().Joined(",")}) => {FunctionDescription.ReturnType}";
    }

    public class TSUnionType : ITSType {
        public HashSet<ITSType> Parts { get; } = new HashSet<ITSType>();

        //TODO implement this as an operator overload on +=
        public void AddPart(ITSType part) {
            if (part is TSUnionType x) {
                x.Parts.AddRangeTo(Parts);
            } else {
                Parts.Add(part);
            }
        }

        public bool Equals(ITSType other) => other is TSUnionType x && Parts.SequenceEqual(x.Parts);
        public IEnumerable<TSSimpleType> TypeParts() => Parts.SelectMany(x => x.TypeParts());

        public override string ToString() => Parts.Joined("|");
    }
}
