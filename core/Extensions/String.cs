using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using static System.StringComparison;
using System.Diagnostics;

namespace TsActivexGen.Util {
    public static class StringExtensions {
        public static string RegexReplace(this string s, Regex re, string replacement) => re.Replace(s, replacement);
        public static string RegexReplace(this string s, string pattern, string replacement) => Regex.Replace(s, pattern, replacement);
        public static bool IsNullOrEmpty(this string s) => string.IsNullOrEmpty(s);
        public static string IfNullOrEmpty(this string s, string replacement) => s.IsNullOrEmpty() ? replacement : s;

        private static Regex linebreakAfter = new Regex(@".*?(?:$|: (?:\(|\{))");
        private static Regex comma = new Regex(@".{0,170}(?:,\s+|$)"); // TODO 170 depends on the indentaition level
        public static void AppendLineTo(this string s, StringBuilder sb, int indentationLevel = 0) {
            var toAppend = (new string(' ', indentationLevel * 4) + s).TrimEnd();
            if (toAppend.Length <= 200) {
                sb.AppendLine(toAppend);
                return;
            }


            var lines = new List<string>();

            // first break at ( or {
            // then break after a , if needed -- can only by done once it has already been broken after a ( or a {
            var matches = linebreakAfter.Matches(toAppend);
            if (matches.Count == 0) { throw new Exception("Unable to split long line"); }
            foreach (Match match in matches) {
                if (match.Length == 0) { continue; }
                var line = toAppend.Substring(match.Index, match.Length).TrimEnd();
                if (match.Index > 0) { line = new string(' ', (indentationLevel + 1) * 4) + line; }
                if (line.Length <= 200) {
                    lines.Add(line);
                    continue;
                }
                var commaMatches = comma.Matches(line);
                if (commaMatches.Count == 0) { throw new Exception("Unable to split long line"); }
                foreach (Match commaMatch in commaMatches) {
                    var line1 = line.Substring(commaMatch.Index, commaMatch.Length).TrimEnd();
                    if (commaMatch.Index > 0) { line1 = new string(' ', (indentationLevel + 1) * 4) + line1; }
                    if (line1.Length > 200) { throw new Exception("Unable to split long line"); }
                    lines.Add(line1);
                }
            }

            foreach (var line in lines) {
                sb.AppendLine(line);
            }
        }

        /// <summary>Appends the passed-in string as a line, followed by another empty line</summary>
        public static void AppendWithNewSection(this string s, StringBuilder sb, int indentationLevel = 0) {
            s.AppendLineTo(sb, indentationLevel);
            sb.AppendLine();
        }
        public static void AppendLinesTo(this IEnumerable<string> lines, StringBuilder sb, int indentationLevel = 0, string endOfLine = null) {
            var indentation = new string(' ', indentationLevel * 4);
            lines.Select((x, index, atEnd) => {
                var actualEndOfLine = atEnd ? "" : endOfLine;
                return $"{x}{actualEndOfLine}";
            }).ForEach(line => line.AppendLineTo(sb, indentationLevel));
        }
        public static void AppendLinesTo<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> lines, StringBuilder sb, Func<TKey, TValue, string> selector, int indentationLevel = 0, string endOfLine = null) {
            lines.SelectKVP(selector).AppendLinesTo(sb, indentationLevel, endOfLine);
        }

        public static bool Contains(this string source, string toCheck, StringComparison comp) => source.IndexOf(toCheck, comp) >= 0;

        public static string ForceEndsWith(this string s, string end, StringComparison comparisonType = OrdinalIgnoreCase) {
            var ret = s;
            if (!s.EndsWith(end, comparisonType)) { ret += end; }
            return ret;
        }

        public static bool StartsWithAny(this string s, IEnumerable<string> tests, StringComparison comparisonType = OrdinalIgnoreCase) => tests.Any(x => s.StartsWith(x, comparisonType));
        public static bool In(this string s, IEnumerable<string> vals, StringComparison comparisonType = OrdinalIgnoreCase) => vals.Any(x => string.Equals(s, x, comparisonType));
        public static string SubstringIndexes(this string s, int start, int stop) {
            var absoluteStart = start < 0 ? s.Length + start : start;
            var absoluteStop = stop < 0 ? s.Length + stop : stop;
            var length = Math.Max(absoluteStop - absoluteStart, 0);
            return s.Substring(absoluteStart, length);
        }
    }
}
