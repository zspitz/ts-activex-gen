using System;
using System.Collections.Generic;
using System.Linq;
using static TsActivexGen.TSParameterType;
using static System.Environment;
using static System.Linq.Enumerable;
using System.Text.RegularExpressions;
using static System.Globalization.NumberStyles;

namespace TsActivexGen {
    public static class Functions {
        public static KeyValuePair<TKey, TValue> KVP<TKey, TValue>(TKey key, TValue value) => new KeyValuePair<TKey, TValue>(key, value);

        public static string RelativeName(string absoluteType, string withinNamespace) {
            if (IsLiteralTypeName(absoluteType)) { return absoluteType; }

            if (absoluteType.Contains("<")) {
                //currently, TSInterfaceDescription.Extends is a HashSet<string>, which means it might contain generic types within the string
                //we're not expecting any interface should extend from a generic types
                throw new NotImplementedException("Parse string as generic type");
            }

            var typeParts = absoluteType.Split('.');
            if (typeParts.Length <= 1) { return absoluteType; }
            var nsParts = withinNamespace.Split('.');
            var finalParts = typeParts.SkipWhile((x, i) => i < nsParts.Length && x == nsParts[i]).ToList();
            if (finalParts.Intersect(nsParts).Any()) { return absoluteType; }  //e.g. com.sun.star.inspection.XPropertyHandler within namespace com.sun.star.form.inspection, will otherwise resolve to form.inspection
            return finalParts.Joined(".");
        }
        public static (string @namespace, string name) SplitName(string typename, string delimiter = ".") {
            if (IsLiteralTypeName(typename)) { return ("", typename); }
            switch (typename.LastIndexOf(delimiter)) {
                case int x when x < 0:
                    return ("", typename);
                case int x when x == 0:
                    return ("", typename.Substring(1));
                case int x:
                    return (typename.Substring(0, x), typename.Substring(x + 1));
            }
        }
        public static bool IsLiteralTypeName(string typename) {
            if (typename.IsNullOrEmpty()) { return false; }
            if (typename.In(new[] { "true", "false", "null", "undefined" }, StringComparison.Ordinal)) { return true; }
            var firstChar = typename[0];
            if (char.IsLetter(firstChar) || firstChar == '_') { return false; }
            return true;
        }
        public static string MakeNamespace(string part1, string part2) {
            if (part1.IsNullOrEmpty() || part2.IsNullOrEmpty()) {
                return part1 + part2;
            } else {
                return $"{part1}.{part2}";
            }
        }

        public static string GetTypeString(ITSType type, string ns, bool asConstraint = false) {
            string ret = null;

            switch (type) {
                case TSSimpleType x when x.FullName == "Array":
                    ret = "[]";
                    break;
                case TSSimpleType x:
                    ret = RelativeName(x.FullName, ns);
                    break;
                case TSTupleType x:
                    ret = $"[{x.Members.Joined(", ", y => GetTypeString(y, ns))}]";
                    break;
                case TSObjectType x:
                    var joined = x.Members.JoinedKVP((key, val) => {
                        var @readonly = val.@readonly ? "readonly " : "";
                        if (key.Contains(".")) { key = $"'{key}'"; }
                        return $"{@readonly}{key}: {GetTypeString(val.type, ns)}";
                    }, ", ");
                    ret = $"{{{joined}}}";
                    break;
                case TSFunctionType x:
                    ret = $"({x.FunctionDescription.Parameters.Joined(", ", y => GetParameterString(y, ns))}) => {GetTypeString(x.FunctionDescription.ReturnType, ns)}";
                    break;
                case TSComposedType x:
                    ret = x.Parts.Select(y => GetTypeString(y, ns)).OrderBy(y => y).Joined($" {x.Operator} ");
                    break;
                case TSGenericType x when x.Name == "Array" && x.Parameters.Count == 1 && (x.Parameters.Single() is TSSimpleType || x.Parameters.Single() is TSPlaceholder):
                    var prm = x.Parameters.Single();
                    ret = $"{GetTypeString(prm, ns)}[]";
                    break;
                case TSGenericType x:
                    ret = $"{x.Name}<{x.Parameters.Joined(", ", y => GetTypeString(y, ns))}>";
                    break;
                case TSPlaceholder x:
                    var @default = "";
                    var extends = "";
                    if (asConstraint) {
                        if (x.Default != null) { @default = $" = {GetTypeString(x.Default, ns)}"; }
                        if (x.Extends != null) { extends = $" extends {GetTypeString(x.Extends, ns)}"; }
                    }
                    ret = $"{x.Name}{extends}{@default}";
                    break;
                case TSKeyOf x:
                    ret = $"keyof {GetTypeString(x.Operand, ns)}";
                    break;
                case TSLookup x:
                    ret = $"{GetTypeString(x.Type, ns)}[{x.Accessor}]";
                    break;
                default:
                    if (type != null) { throw new NotImplementedException(); }
                    break;
            }

            return ret;
        }

        public static string GetParameterString(KeyValuePair<string, TSParameterDescription> x, string ns) {
            var name = x.Key;
            var parameterDescription = x.Value;
            if (parameterDescription.ParameterType == Rest) {
                name = "..." + name;
            } else if (parameterDescription.ParameterType == Optional) {
                name += "?";
            }
            return $"{name}: {GetTypeString(parameterDescription.Type, ns)}";
        }

        public static string NewLines(int count) => Repeat(NewLine, count).Joined("");

        public static (string first, string rest) FirstPathPart(string path, string delimiter = ".") {
            if (path.IsNullOrEmpty()) { return ("", ""); }
            var parts = path.Split(new[] { delimiter }, StringSplitOptions.None);
            return (parts[0], parts.Skip(1).Joined(delimiter));
        }

        //TODO currently handles generic and simple types, not other type combinations e.g. intersection, union types
        //TODO handle array types as well
        public static ITSType ParseTypeName(string typename, Dictionary<string, ITSType> mapping = null) {
            if (typename.IsNullOrEmpty()) { return TSSimpleType.Void; }

            var root = new SimpleTreeNode<ITSType>();
            var currentNode = root;

            for (var currentPos = 0; currentPos < typename.Length; currentPos++) {
                switch (typename[currentPos]) {
                    case '&':
                    case '|':
                        throw new NotImplementedException("Intersection / union types not implemented");
                    case var c when char.IsWhiteSpace(c):
                    case '.': //ignore initial period; other periods will be handled together with the identifier
                        continue;
                    case var c when char.IsLetter(c) || c == '_':
                        var j = currentPos + 1;
                        for (; j < typename.Length; j++) {
                            var nextChar = typename[j];
                            if (char.IsLetter(nextChar) || nextChar.In("0123456789_.")) { continue; }
                            break;
                        }
                        var identifier = typename.Substring(currentPos, j - currentPos);
                        if (mapping != null && mapping.TryGetValue(identifier, out var result)) {
                            currentNode.Data = result;
                        } else {
                            currentNode.Data = (TSSimpleType)identifier;
                        }
                        currentPos = j - 1; //because the for loop will advance currentPos
                        break;
                    case '<':
                        if (currentNode.Data == null) { throw new NullReferenceException(); }
                        currentNode.Data = new TSGenericType() { Name = (currentNode.Data as TSSimpleType).FullName };
                        currentNode = currentNode.AddChild((ITSType)(TSSimpleType)"");
                        break;
                    case '>':
                        if (currentNode.Parent.Data is TSPlaceholder) {
                            currentNode = currentNode.Parent<ITSType, SimpleTreeNode<ITSType>>().Parent<ITSType, SimpleTreeNode<ITSType>>();
                        } else {
                            currentNode = currentNode.Parent<ITSType, SimpleTreeNode<ITSType>>();
                        }
                        break;
                    case ',':
                        currentNode = currentNode.AddSibling<ITSType, SimpleTreeNode<ITSType>>((TSSimpleType)"");
                        break;
                    case '=':
                        var placeholder = new TSPlaceholder() { Name = (currentNode.Data as TSSimpleType).FullName };
                        currentNode.Data = placeholder;
                        currentNode = currentNode.AddChild((ITSType)(TSSimpleType)"");
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            fillNode(root);
            root.Descendants<ITSType, SimpleTreeNode<ITSType>>().ForEach(fillNode);
            return root.Data;

            void fillNode(SimpleTreeNode<ITSType> node) {
                switch (node.Data) {
                    case TSSimpleType x:
                        break;
                    case TSGenericType x:
                        node.Children.Select(y => y.Data).AddRangeTo(x.Parameters);
                        break;
                    case TSPlaceholder x:
                        x.Default = node.Children.Single().Data;
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public static T IIFE<T>(Func<T> generator) => generator();

        /// <summary>Generates a string representation of literal values</summary>
        public static string AsString(object value) {
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

        private static Regex reHex = new Regex("(-)?(0x)?(.+)");
        public static long ParseLong(string s) {
            if (!s.ContainsAny('x', 'X', '&')) { return long.Parse(s); }
            if (s.Contains(".")) { throw new InvalidOperationException("Use parseDecimal"); }
            var groups = reHex.Match(s).Groups.Cast<Group>().ToList();
            var ret = long.Parse(groups[3].Value, HexNumber);
            if (groups[1].Success) { ret = -ret; }
            return ret;
        }
        public static decimal ParseDecimal(string s) {
            if (!s.ContainsAny('x', 'X', '&')) { return decimal.Parse(s); }
            var groups = reHex.Match(s).Groups.Cast<Group>().ToList();
            decimal ret;
            if (s.Contains(".")) {
                if (groups[2].Success) { throw new InvalidOperationException("No hex together with decimal"); }
                ret = decimal.Parse(groups[3].Value);
            } else {
                ret = long.Parse(groups[3].Value, HexNumber);
            }
            if (groups[1].Success) { ret = -ret; }
            return ret;
        }

        public static ITSType MappedType(ITSType t, Func<ITSType, ITSType> mapper) {
            switch (t) {
                case TSSimpleType x:
                case TSPlaceholder y:
                    break;

                case TSGenericType x:
                    for (int i = 0; i < x.Parameters.Count; i++) {
                        x.Parameters[i] = mapper(x.Parameters[i]);
                    }
                    break;
                case TSComposedType x:
                    for (int i = 0; i < x.Parts.Count; i++) {
                        var toAdd = x.Parts.Select(mapper).ToList();
                        x.Parts.Clear();
                        toAdd.AddRangeTo(x.Parts);
                    }
                    break;
                case TSTupleType x:
                    for (int i = 0; i < x.Members.Count; i++) {
                        x.Members[i] = mapper(x.Members[i]);
                    }
                    break;
                case TSObjectType x:
                    x.Members.ForEachKVP((name, t2) => {
                        x.Members[name] = (mapper(t2.type), t2.@readonly);
                    });
                    break;
                case TSFunctionType x:
                    var m = x.FunctionDescription;
                    m.ReturnType = mapper(m.ReturnType);
                    for (int i = 0; i < m.Parameters.Count; i++) {
                        var (name, p) = m.Parameters[i];
                        p.Type = mapper(p.Type);
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }
            return mapper(t);
        }

        public static IEnumerable<T> WrappedSequence<T>(T item) => Repeat(item, 1);

        public static TreeNodeVM<TData> CreateTreeNode<TData>(TData data, TreeNodeVM<TData> parent = null) => new TreeNodeVM<TData>(data) { Parent=parent };
    }
}
