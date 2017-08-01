using System;
using System.Linq;
using System.Collections.Generic;
using static System.Environment;
using static TsActivexGen.Functions;

namespace TsActivexGen.Wpf {
    public static class Functions {
        public static string GetTsConfig(string name) => @"
{
    ""compilerOptions"": {
        ""module"": ""commonjs"",
        ""lib"": [""es5"", ""scripthost""],
        ""noImplicitAny"": true,
        ""noImplicitThis"": true,
        ""strictNullChecks"": true,
        ""strict"": true,
        ""baseUrl"": ""../"",
        ""typeRoots"": [
            ""../""
        ],
        ""types"": [],
        ""noEmit"": true,
        ""forceConsistentCasingInFileNames"": true
    },
    ""files"": [
        ""index.d.ts"",
        """ + name + @"-tests.ts""
    ]
}".Trim();

        public static string GetHeaders(string name, string description, string libraryUrl, string authorName, string authorUrl, int majorVerion, int minorVersion) {
            var lines = new List<string>();
            $"// Type definitions for {new[] { description, name }.Where(x => !x.IsNullOrEmpty()).Joined(" - ")} {majorVerion}.{minorVersion}".AddTo(lines);
            if (!libraryUrl.IsNullOrEmpty()) { $"// Project: {libraryUrl}".AddTo(lines); }
            $"// Definitions by: {authorName} <{authorUrl}>".AddTo(lines);
            $"// Definitions: https://github.com/DefinitelyTyped/DefinitelyTyped".AddTo(lines);
            return lines.Joined(NewLine) + NewLines(2);
        }

        public static string ReferenceDirectives(IEnumerable<string> types) {
            var ret = types.Joined("", y => $"/// <reference types=\"activex-{y.ToLower()}\" />" + NewLine);
            if (!ret.IsNullOrEmpty()) { ret += NewLine; }
            return ret;
        }
    }
}
