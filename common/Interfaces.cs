using System;
using System.Collections.Generic;

namespace TsActivexGen {
    public interface ITSType: IEquatable<ITSType>, IClonable<ITSType> {
        IEnumerable<TSSimpleType> TypeParts();
    }
    public interface IClonable<T> {
        T Clone();
    }
}