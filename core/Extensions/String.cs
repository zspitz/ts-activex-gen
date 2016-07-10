﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

namespace TsActivexGen.Util {
    public static class StringExtensions {
        public static string RegexReplace(this string s, Regex re, string replacement) {
            return re.Replace(s, replacement);
        }
        public static string RegexReplace(this string s, string pattern, string replacement) {
            return Regex.Replace(s, pattern, replacement);
        }
        public static bool IsNullOrEmpty(this string s) {
            return string.IsNullOrEmpty(s);
        }
        public static void AppendLineTo(this string s, StringBuilder sb, int indentationLevel=0) {
            sb.Append(new string(' ', indentationLevel * 4));
            sb.AppendLine(s);
        }
        public static void AppendTo(this string s, StringBuilder sb) {
            sb.Append(s);
        }

        /// <summary>Appends the passed-in string as a line, followed by another empty line</summary>
        public static void AppendWithNewSection(this string s, StringBuilder sb, int indentationLevel = 0) {
            s.AppendLineTo(sb, indentationLevel);
            sb.AppendLine();
        }
        public static void AppendLinesTo(this IEnumerable<string> lines, StringBuilder sb, int indentationLevel=0, string endOfLine=null, string startOfLine=null) {
            var indentation = new string(' ', indentationLevel * 4);
            if (lines.Any()) { sb.Append(indentation); }
            lines.Joined(endOfLine +  Environment.NewLine + indentation + startOfLine).AppendLineTo(sb);
        }
        public static void AppendLinesTo<T>(this IEnumerable<T> lines, StringBuilder sb, Func<T,string> selector, int indentationLevel = 0, string endOfLine = null, string startOfLine = null) {
            lines.Select(selector).AppendLinesTo(sb, indentationLevel, endOfLine, startOfLine);
        }
        public static void AppendLinesTo<TKey,TValue>(this IEnumerable<KeyValuePair<TKey,TValue>> lines, StringBuilder sb, Func<TKey,TValue, string> selector, int indentationLevel = 0, string endOfLine = null, string startOfLine = null) {
            lines.SelectKVP(selector).AppendLinesTo(sb, indentationLevel, endOfLine, startOfLine);
        }

        public static bool Contains(this string source, string toCheck, StringComparison comp) {
            return source.IndexOf(toCheck, comp) >= 0;
        }
    }
}
