using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TsActivexGen.Wpf {
    public static class Misc {
        public static Dictionary<string, string> ProjectURL = new Dictionary<string, string>() {
            {"faxcomexlib","https://msdn.microsoft.com/en-us/library/windows/desktop/ms684513(v=vs.85).aspx"},
            {"shdocvw","https://msdn.microsoft.com/en-us/library/aa752040(v=vs.85).aspx"},
            {"shell32","https://msdn.microsoft.com/en-us/library/windows/desktop/bb773938(v=vs.85).aspx"},
            {"speechlib","https://msdn.microsoft.com/en-us/library/ee125663(v=vs.85).aspx"},
            {"wmi","https://msdn.microsoft.com/en-us/library/aa393258(v=vs.85).aspx"},
            {"access","https://msdn.microsoft.com/en-us/library/dn142571.aspx"},
            {"dao","https://msdn.microsoft.com/en-us/library/dn124645.aspx"},
            {"excel","https://msdn.microsoft.com/en-us/library/fp179694.aspx"},
            {"graph","https://msdn.microsoft.com/en-us/vba/excel-vba/articles/graph-visual-basic-reference"},
            {"infopath","https://msdn.microsoft.com/en-us/library/jj602751.aspx"},
            {"mshtml","https://msdn.microsoft.com/en-us/library/aa741317(v=vs.85).aspx"},
            {"msxml2","https://msdn.microsoft.com/en-us/library/ms763742.aspx"},
            {"office","https://msdn.microsoft.com/VBA/Office-Shared-VBA/articles/office-vba-object-library-reference"},
            {"outlook","https://msdn.microsoft.com/en-us/vba/vba-outlook"},
            {"powerpoint","https://msdn.microsoft.com/en-us/library/fp161225.aspx"},
            {"publisher","https://msdn.microsoft.com/en-us/vba/publisher-vba/articles/object-model-publisher-vba-reference"},
            {"vbide","https://msdn.microsoft.com/en-us/vba/language-reference-vba/articles/collections-visual-basic-add-in-model"},
            {"word","https://msdn.microsoft.com/en-us/library/fp179696.aspx"},
            {"adodb","https://msdn.microsoft.com/en-us/library/jj249010.aspx" }
        };

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
