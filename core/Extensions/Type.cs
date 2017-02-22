using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsActivexGen.Util {
    public static class TypeExtensions {
        public static Type DefinitionIfGeneric(this Type t) {
            if (t.IsGenericType) { t= t.GetGenericTypeDefinition(); }
            return t;
        }
        public static bool IsNullable(this Type t, bool orReferenceType=false) {
            if (orReferenceType && !t.IsValueType) { return true; }
            return t.DefinitionIfGeneric() == typeof(Nullable<>);
        }
        public static Type UnderlyingIfNullable(this Type t) {
            if (t.IsNullable()) { t = Nullable.GetUnderlyingType(t); }
            return t;
        }
        public static bool IsNumeric(this Type t) {
            return t.UnderlyingIfNullable().In(typeof(int), typeof(short), typeof(decimal), typeof(double), typeof(ushort), typeof(float)); //other types should be here too
        }
    }
}
