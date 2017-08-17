using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using static TsActivexGen.TSParameterType;
using static System.Environment;
using static System.Linq.Enumerable;

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

            var (typeNamespace, typeOnly) = SplitName(absoluteType);
            if (typeNamespace.Length == 0) { return absoluteType; }

            var typeNamespaceParts = typeNamespace.Split('.');
            var partsOfWithinNamespace = withinNamespace.Split('.');
            var retParts = new List<string>();
            var pathMismatch = false;
            for (int i = 0; i < typeNamespaceParts.Length; i++) {
                if (partsOfWithinNamespace.Length <= i || typeNamespaceParts[i] != partsOfWithinNamespace[i]) {
                    pathMismatch = true;
                }
                if (pathMismatch) {
                    retParts.Add(typeNamespaceParts[i]);
                }
            }
            retParts.Add(typeOnly);
            return retParts.Joined(".");
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

        //public static bool IsGenericTypeName(string fullName) => !IsLiteralTypeName(fullName) && fullName.Contains("<");

        public static string GetProgIDFromCLSID(string clsid) {
            using (var key = Registry.ClassesRoot.OpenSubKey($"CLSID\\{clsid}")) {
                using (var subkey = key?.OpenSubKey("VersionIndependentProgID") ?? key?.OpenSubKey("ProgID")) {
                    return (string)subkey?.GetValue("");
                }
            }
        }

        public static string GetTypeString(ITSType type, string ns) {
            string ret = null;

            switch (type) {
                case TSSimpleType x:
                    ret = RelativeName(x.FullName, ns);
                    break;
                case TSTupleType x:
                    ret = $"[{x.Members.Joined(", ", y => GetTypeString(y, ns))}]";
                    break;
                case TSObjectType x:
                    ret = $"{{{x.Members.JoinedKVP((key, val) => $"{(val.@readonly ? "readonly " : "")}{key}: {GetTypeString(val.type, ns)}", ", ")}}}";
                    break;
                case TSFunctionType x:
                    ret = $"({x.FunctionDescription.Parameters.Joined(", ", y => GetParameterString(y, ns))}) => {GetTypeString(x.FunctionDescription.ReturnType, ns)}";
                    break;
                case TSUnionType x:
                    ret = x.Parts.Select(y => GetTypeString(y, ns)).OrderBy(y => y).Joined(" | ");
                    break;
                case TSGenericType x:
                    ret = $"{x.Name}<{x.Parameters.Joined(",", y => GetTypeString(y, ns))}>";
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
        public static ITSType ParseTypeName(string typename, Dictionary<string, ITSType> mapping=null) {
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
                        currentNode = currentNode.AddChild((TSSimpleType)"");
                        break;
                    case '>':
                        currentNode = currentNode.Parent;
                        break;
                    case ',':
                        currentNode = currentNode.AddSibling((TSSimpleType)"");
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            fillNode(root);
            root.Descendants().ForEach(fillNode);
            return root.Data;

            void fillNode(SimpleTreeNode<ITSType> node)
            {
                switch (node.Data) {
                    case TSSimpleType x:
                        break;
                    case TSGenericType x:
                        node.Children.Select(y => y.Data).AddRangeTo(x.Parameters);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public static T IIFE<T>(Func<T> generator) => generator();
    }
}
