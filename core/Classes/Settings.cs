using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsActivexGen.Util;
using Newtonsoft.Json;

namespace TsActivexGen {
    public class Settings {
        public string OutputPath { get; set; }
        public bool AutogenerateTests { get; set; } = true;
        public string DefaultAuthorName { get; set; }
        public string DefaultAuthorUrl { get; set; }
        public List<string> RecentFiles { get; set; }
        public List<LibrarySettings> LibrarySettings { get; set; }

        public string ToJSON() {
            return JsonConvert.SerializeObject(this);
        }
        public static Settings FromJSON(string json) {
            return JsonConvert.DeserializeObject<Settings>(json);
        }
    }
    public class LibrarySettings {
        public string TLBID { get; set; }
        public string FilePath {  get; set; }
        public string ProjectURL { get; set; }
        public string Name { get; set; }
        public string AuthorName { get; set; }
        public string AuthorUrl { get; set; }
    }
}
