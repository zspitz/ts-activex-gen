using System.Collections.Generic;

namespace TsActivexGen.idlbuilder {
    class JsDocPartsCollection {
        private List<object> parts = new List<object>();
        public void AddPart(string s) => parts.Add(s);
        public void AddPart((string key, string value) pair) => parts.Add(pair);
    }
}
