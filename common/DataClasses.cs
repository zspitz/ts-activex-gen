using System;
using System.Collections.Generic;
using System.Linq;
using static TsActivexGen.TSParameterType;
using System.Diagnostics;
using static TsActivexGen.Functions;

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

        protected static bool OperatorEquals(T x, T y) {
            if (ReferenceEquals(x, null)) { return ReferenceEquals(y, null); }
            return x.Equals(y);
        }
    }

    public class TSEnumDescription {
        public Dictionary<string, string> Members { get; } = new Dictionary<string, string>(); //values -> string representation of value
        public List<KeyValuePair<string, string>> JsDoc { get; } = new List<KeyValuePair<string, string>>();
    }

    public class TSParameterDescription : EqualityBase<TSParameterDescription> {
        public ITSType Type { get; set; }
        public TSParameterType ParameterType { get; set; } = Standard;

        public override bool Equals(TSParameterDescription other) {
            return Type.Equals(other.Type)
                && ParameterType == other.ParameterType;
        }
        public override int GetHashCode() {
            unchecked {
                int hash = 17;
                hash = hash * 486187739 + Type.GetHashCode();
                hash = hash * 486187739 + ParameterType.GetHashCode();
                return hash;
            }
        }
        public override bool Equals(object other) => base.Equals(other);

        public static bool operator ==(TSParameterDescription x, TSParameterDescription y) => OperatorEquals(x, y);
        public static bool operator !=(TSParameterDescription x, TSParameterDescription y) => !OperatorEquals(x, y);

        public void MergeTypeFrom(TSParameterDescription source) {
            if (Type is TSUnionType x) {
                x.AddPart(source.Type);
            } else if (Type != source.Type) {
                var final = new TSUnionType();
                final.AddPart(Type);
                final.AddPart(source.Type);
                if (final.Parts.Count > 1) {
                    Type = final;
                }
            }
        }
    }

    public class TSMemberDescription : EqualityBase<TSMemberDescription> {
        public List<KeyValuePair<string, TSParameterDescription>> Parameters { get; set; } //(null means a property, empty means empty parameter list); this mut be a list, becaus parameter order is important
        public ITSType ReturnType { get; set; }
        public bool? ReadOnly { get; set; }
        public List<KeyValuePair<string, string>> JsDoc { get; } = new List<KeyValuePair<string, string>>();

        public void AddParameter(string name, ITSType type) {
            if (Parameters == null) { Parameters = new List<KeyValuePair<string, TSParameterDescription>>(); }
            Parameters.Add(name, new TSParameterDescription() { Type = type });
        }
        public void AddParameter(string name, TSSimpleType type) => AddParameter(name, (ITSType)type);

        public override bool Equals(TSMemberDescription other) {
            bool parameterEquality;
            if ((Parameters == null) != (other.Parameters == null)) {
                parameterEquality = false;
            } else if (Parameters == null) {
                parameterEquality = true;
            } else {
                parameterEquality = Parameters.SequenceEqual(other.Parameters);
            }
            return parameterEquality && ReadOnly == other.ReadOnly && ReturnType.Equals(other.ReturnType);
        }

        public override int GetHashCode() {
            unchecked {
                int hash = 17;
                hash = hash * 486187739 + ReturnType.GetHashCode();
                foreach (var prm in Parameters) {
                    hash = hash * 486187739 + prm.GetHashCode();
                }
                if (ReadOnly.HasValue) {
                    hash = hash * 486187739 + ReadOnly.GetHashCode();
                }
                return hash;
            }
        }

        public IEnumerable<TSSimpleType> TypeParts() => Parameters.DefaultIfNull().Values().SelectMany(x => x.Type.TypeParts()).Concat(ReturnType.TypeParts());

        public bool TryFoldInto(TSMemberDescription destination) {
            if (Parameters == null || destination.Parameters == null) { return false; } //property
            if (!Equals(ReturnType, destination.ReturnType)) { return false; }
            if (Parameters.Count != destination.Parameters.Count) { return false; }
            if (!Parameters.Keys().SequenceEqual(destination.Parameters.Keys())) { return false; }
            if (Parameters.Values().Any(x => x.ParameterType == Rest) || destination.Parameters.Values().Any(x => x.ParameterType == Rest)) { return false; }

            var parameter1Type = Parameters.Get("obj")?.Type;
            var destParameter1Type = destination.Parameters.Get("obj")?.Type;
            var parameter2Type = Parameters.Get("event")?.Type;
            var destParameter2Type = destination.Parameters.Get("event")?.Type;
            if (parameter1Type != null && parameter1Type.Equals(destParameter1Type) && parameter2Type != null && parameter2Type.Equals(destParameter2Type)) {
                Debugger.Break();
            }

            var foldableMethod = true;
            var unfoldableParameters = Parameters.Values().Zip(destination.Parameters.Values(), (sourceParam, destParam) => {
                if (!foldableMethod) { return null; }
                if (sourceParam.ParameterType != destParam.ParameterType) { //if "optionality" of corresponding parameters doesn't match
                    foldableMethod = false;
                    return null;
                }
                var alreadyIncludesType = false;
                if (destParam.Type is TSUnionType x && sourceParam.Type is TSUnionType y) {
                    alreadyIncludesType = y.Parts.Except(x.Parts).None();
                } else if (destParam.Type is TSUnionType x1) {
                    alreadyIncludesType = x1.Parts.Contains(sourceParam.Type);
                } else {
                    alreadyIncludesType = destParam.Type.Equals(sourceParam.Type);
                }
                return new { sourceParam, destParam, alreadyIncludesType };
            }).Where(x => !x.alreadyIncludesType).ToList();
            if (!foldableMethod) { return false; }

            switch (unfoldableParameters.Count) {
                case 0:
                    return true;
                case var x2 when x2 > 1:
                    return false;
                default: // 1
                    var details = unfoldableParameters[0];
                    details.destParam.MergeTypeFrom(details.sourceParam);
                    return true;
            }
        }
    }

    public class TSInterfaceDescription : EqualityBase<TSInterfaceDescription> {
        public List<KeyValuePair<string, TSMemberDescription>> Members { get; } = new List<KeyValuePair<string, TSMemberDescription>>();
        public List<TSMemberDescription> Constructors { get; } = new List<TSMemberDescription>();
        public List<KeyValuePair<string, string>> JsDoc { get; } = new List<KeyValuePair<string, string>>();
        public HashSet<string> Extends { get; } = new HashSet<string>();

        public void ConsolidateMembers() {

            //consolidate members
            var membersLookup = Members.Select((kvp, index) => (key: kvp.Key, value: kvp.Value, position: index)).ToLookup(x => new {
                name = x.key,
                returnType = x.value.ReturnType,
                parameterString = x.value.Parameters?.Keys().Joined()
            });
            var positionsToRemove = new List<int>();
            foreach (var grp in membersLookup) {
                if (grp.Count() == 1) { continue; }
                if (grp.Key.parameterString == null) { continue; } //ignore properties
                ConsolidateGroup(grp.Select(x => (x.value, x.position))).AddRangeTo(positionsToRemove);
            }
            Members.RemoveMultipleAt(positionsToRemove);

            //consolidate constructors
            var constructorsLookup = Constructors.Select((ctor, index) => (ctor: ctor, position: index)).ToLookup(x => new {
                returnType = x.ctor.ReturnType,
                parameterString = x.ctor.Parameters?.Keys().Joined()
            });
            positionsToRemove = new List<int>();
            foreach (var grp in constructorsLookup) {
                if (grp.Count() == 1) { continue; }
                //constructors are always members, never properties; we don't have to check for null parameter collections
                ConsolidateGroup(grp).AddRangeTo(positionsToRemove);
            }
            Constructors.RemoveMultipleAt(positionsToRemove);


            List<int> ConsolidateGroup(IEnumerable<(TSMemberDescription member, int position)> grp)
            {
                var ret = new List<int>();
                var lst = grp.Reverse().ToList();
                bool modified;
                do {
                    modified = false;
                    for (int i = lst.Count - 1; i >= 0; i -= 1) {
                        var source = lst[i];
                        for (int j = 0; j < i; j++) {
                            var dest = lst[j];
                            if (source.member.TryFoldInto(dest.member)) {
                                modified = true;
                                ret.Add(lst[i].position);
                                lst.RemoveAt(i);
                                break;
                            }
                        }
                    }
                } while (modified);
                return ret;
            }
        }
        
        //TODO if an interface extends other interfaces, it's considered not equal to any other interface; ideally equality would involve checcking that the sum of all inherited members are equal
        public override bool Equals(TSInterfaceDescription other) => Extends.None() && Members.SequenceEqual(other.Members) && Constructors.SequenceEqual(other.Constructors);

        public override int GetHashCode() {
            unchecked {
                int hash = 17;
                hash = hash * 486187739 + Members.GetHashCode();
                hash = hash * 486187739 + Constructors.GetHashCode();
                return hash;
            }
        }
    }

    public class TSNamespaceDescription {
        public Dictionary<string, TSEnumDescription> Enums { get; } = new Dictionary<string, TSEnumDescription>();
        public Dictionary<string, TSInterfaceDescription> Interfaces { get; } = new Dictionary<string, TSInterfaceDescription>();
        public Dictionary<string, TSAliasDescription> Aliases { get; } = new Dictionary<string, TSAliasDescription>();
        public Dictionary<string, TSNamespaceDescription> Namespaces { get; } = new Dictionary<string, TSNamespaceDescription>();
        public List<KeyValuePair<string, string>> JsDoc { get; } = new List<KeyValuePair<string, string>>();

        protected virtual IEnumerable<TSInterfaceDescription> allInterfaces => Interfaces.Values;

        public HashSet<string> GetUsedTypes() {
            var types = new List<TSSimpleType>();
            allInterfaces.SelectMany(i => i.Members).Values().SelectMany(x => x.TypeParts()).AddRangeTo(types);
            Aliases.SelectMany(x => x.Value.TargetType.TypeParts()).AddRangeTo(types);
            types.RemoveAll(x => x.IsLiteralType);
            var ret = types.Select(x => x.FullName).ToHashSet();
            Namespaces.SelectMany(x => x.Value.GetUsedTypes()).AddRangeTo(ret);
            return ret;
        }
        public HashSet<string> GetKnownTypes() {
            var ret = MiscExtensions.builtins.ToHashSet();
            Enums.Keys.AddRangeTo(ret);
            Interfaces.Keys.AddRangeTo(ret);
            Aliases.Keys.AddRangeTo(ret);
            ret.ToList().Select(x => $"SafeArray<{x}>").AddRangeTo(ret);

            Namespaces.Values().SelectMany(x => x.GetKnownTypes()).AddRangeTo(ret);

            return ret.ToHashSet();
        }
        public HashSet<string> GetUndefinedTypes() {
            var ret = GetUsedTypes();
            ret.ExceptWith(GetKnownTypes());
            return ret;
        }

        public void ConsolidateMembers() {
            allInterfaces.ForEach(x => x.ConsolidateMembers());
            Namespaces.ForEachKVP((name, ns) => ns.ConsolidateMembers());
        }

        public TSNamespaceDescription GetNamespace(string path) {
            if (path.IsNullOrEmpty()) { return null; }
            var parts = path.Split('.');
            if (!Namespaces.TryGetValue(parts[0], out var next)) {
                next = new TSNamespaceDescription();
                Namespaces.Add(parts[0], next);
            }
            if (parts.Length==1) { return next; }
            path = parts.Skip(1).Joined(".");
            return next.GetNamespace(path);
        }
    }

    public class TSAliasDescription : EqualityBase<TSAliasDescription> {
        public ITSType TargetType { get; set; }
        public List<KeyValuePair<string, string>> JsDoc { get; } = new List<KeyValuePair<string, string>>();

        public override bool Equals(TSAliasDescription other) => TargetType.Equals(other.TargetType);
        public override int GetHashCode() => unchecked(17 * 486187739 + TargetType.GetHashCode());
    }

    public class TSRootNamespaceDescription : TSNamespaceDescription {
        public string Description { get; set; }
        public int MajorVersion { get; set; }
        public int MinorVersion { get; set; }
        public HashSet<string> Dependencies { get; } = new HashSet<string>();

        public Dictionary<string, TSInterfaceDescription> GlobalInterfaces { get; } = new Dictionary<string, TSInterfaceDescription>();

        public HashSet<TSSimpleType> NominalTypes { get; } = new HashSet<TSSimpleType>();

        protected override IEnumerable<TSInterfaceDescription> allInterfaces => Interfaces.Values.Concat(GlobalInterfaces.Values);
    }

    public class TSNamespaceSet {
        public Dictionary<string, TSRootNamespaceDescription> Namespaces { get; } = new Dictionary<string, TSRootNamespaceDescription>();
        public HashSet<string> GetUsedTypes() => Namespaces.SelectMany(x => x.Value.GetUsedTypes()).ToHashSet();
        public HashSet<string> GetKnownTypes() => Namespaces.SelectMany(x => x.Value.GetKnownTypes()).ToHashSet();
        public HashSet<string> GetUndefinedTypes() {
            var ret = GetUsedTypes();
            ret.ExceptWith(GetKnownTypes());
            return ret;
        }
        public TSNamespaceDescription GetNamespace(string path) {
            var (firstPart, rest) = FirstPathPart(path);
            if (!Namespaces.TryGetValue(firstPart, out var root)) {
                root = new TSRootNamespaceDescription();
                Namespaces.Add(firstPart, root);
            }
            return root.GetNamespace(rest);
        }
    }

    public class TSParameterListComparer : IEqualityComparer<List<KeyValuePair<string, TSParameterDescription>>> {
        public bool Equals(List<KeyValuePair<string, TSParameterDescription>> x, List<KeyValuePair<string, TSParameterDescription>> y) {
            if (x == null) { return y == null; }
            return x.SequenceEqual(y);
        }
        public int GetHashCode(List<KeyValuePair<string, TSParameterDescription>> obj) {
            unchecked {
                return obj.Aggregate(17, (hash, x) => hash * 486187739 + x.GetHashCode());
            }
        }
    }

    public class NamespaceOutput {
        public string Description { get; set; }
        public int MajorVersion { get; set; }
        public int MinorVersion { get; set; }
        public string MainFile { get; set; }
        public HashSet<string> Dependencies { get; set; }
        public string TestsFile { get; set; }
    }
}