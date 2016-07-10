using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsActivexGen.Util {
    public enum KeyConflictResolution {
        Error,
        DontAdd,
        Overwrite,
        ErrorIfNotEqual
    }
}

namespace TsActivexGen {
    public enum TSParameterType {
        Standard,
        Optional,
        Rest
    }
}
