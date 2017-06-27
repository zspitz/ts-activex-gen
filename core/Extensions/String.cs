using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using static System.StringComparison;

namespace TsActivexGen.Util {
    public static class StringExtensions {
        public static string RegexReplace(this string s, Regex re, string replacement) => re.Replace(s, replacement);
        public static string RegexReplace(this string s, string pattern, string replacement) => Regex.Replace(s, pattern, replacement);
        public static bool IsNullOrEmpty(this string s) => string.IsNullOrEmpty(s);

        private static Regex shortLineRegex = new Regex("^.{0,180}(,|{|$)");
        public static void AppendLineTo(this string s, StringBuilder sb, int indentationLevel = 0) {
            var toAppend = (new string(' ', indentationLevel * 4) + s).TrimEnd();
            if (toAppend.Length<=200) {
                sb.AppendLine(toAppend);
                return;
            }

            while (toAppend.Length > 200) {
                var match = shortLineRegex.Match(toAppend);
                if (!match.Success) { throw new Exception("Unable to split long line"); }
                sb.AppendLine(match.Value);
                toAppend = new string(' ', (indentationLevel + 1) * 4) + toAppend.Substring(match.Length);
            }
            if (toAppend.Length>0) { sb.AppendLine(toAppend); }
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
            }).ForEach(line => line.AppendLineTo(sb,indentationLevel));
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
    }
}
