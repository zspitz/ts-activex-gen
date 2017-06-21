using System;
using System.Collections.Generic;
using TsActivexGen.Util;

namespace TsActivexGen.Wpf {
    public static class Functions {
        public static string GetTsConfig(string name) {
            return @"
{
    ""compilerOptions"": {
        ""module"": ""commonjs"",
        ""lib"": [""scripthost""],
        ""noImplicitAny"": true,
        ""noImplicitThis"": true,
        ""strictNullChecks"": false,
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
        }

        public static string GetHeaders(string libraryName, string libraryUrl, string authorName, string authorUrl) {
            return $@"
// Type definitions for {libraryName}
// Project: {libraryUrl}
// Defintions by: {authorName} <{authorUrl}>
// Definitions: https://github.com/DefinitelyTyped/DefinitelyTyped
".Trim();
        }

        public static string ReferenceDirectives(IEnumerable<string> types) => types.Joined("", y => $"/// <reference types=\"{y}\" />" + Environment.NewLine);

        public static void test() {
            var x = 1;
            var s = string.Format("asdf {0} asdf", x);


        }
    }
}
