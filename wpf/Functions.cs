using System;
using System.Collections.Generic;
using TsActivexGen.Util;
using static System.Environment;

namespace TsActivexGen.Wpf {
    public static class Functions {
        public static string GetTsConfig(string name)  => @"
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

        public static string GetHeaders(string name, string description, string libraryUrl, string authorName, string authorUrl) => $@"
// Type definitions for {description.IfNullOrEmpty(name)}
// Project: {libraryUrl}
// Definitions by: {authorName} <{authorUrl}>
// Definitions: https://github.com/DefinitelyTyped/DefinitelyTyped".Trim() + NewLine + NewLine;

        public static string ReferenceDirectives(IEnumerable<string> types) {
            var ret = types.Joined("", y => $"/// <reference types=\"activex-{y.ToLower()}\" />" + NewLine);
            if (!ret.IsNullOrEmpty()) { ret += NewLine; }
            return ret;
        }
    }
}
