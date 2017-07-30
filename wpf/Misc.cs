using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using static System.IO.File;
using Newtonsoft.Json;

namespace TsActivexGen.Wpf {
    public static class Misc {
        public static Dictionary<string, ImportedDetails> StoredDetails = JsonConvert.DeserializeObject<Dictionary<string, ImportedDetails>>(ReadAllText("fixedDetails.json"));

        public static Task RunCommandlineAsync(string commandline) {
            // there is no non-generic TaskCompletionSource
            var tcs = new TaskCompletionSource<bool>();

            var process = new Process {
                StartInfo = {
                    FileName="cmd.exe",
                    Arguments = "/K echo " + commandline + " & " + commandline
                },
                EnableRaisingEvents = true
            };
            process.Exited += (s, e) => tcs.SetResult(true);

            process.Start();

            return tcs.Task;
        }
    }
}
