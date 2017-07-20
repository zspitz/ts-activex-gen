using System;
using System.Collections.Generic;

namespace TsActivexGen {
    public interface ITSType: IEquatable<ITSType> {
        IEnumerable<TSSimpleType> TypeParts();
    }
}