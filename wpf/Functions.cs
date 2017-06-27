using System;
using System.Collections.Generic;
using TsActivexGen.Util;

namespace TsActivexGen.Wpf {
    public static class Functions {
        public static string GetTsConfig(string name)  => @"
{
    ""compilerOptions"": {
        ""module"": ""commonjs"",
        ""lib"": [""scripthost""],
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
// Type definitions for {name} - {description}
// Project: {libraryUrl}
// Definitions by: {authorName} <{authorUrl}>
// Definitions: https://github.com/DefinitelyTyped/DefinitelyTyped
".Trim();

        public static string ReferenceDirectives(IEnumerable<string> types) => types.Joined("", y => $"/// <reference types=\"activex-{y.ToLower()}\" />" + Environment.NewLine);
    }
}
