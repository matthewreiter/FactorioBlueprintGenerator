using System;
using System.Reflection;

namespace CompilerCommon
{
    public static class MethodBaseExtensions
    {
        public static Type GetReturnType(this MethodBase method) => method is MethodInfo methodInfo ? methodInfo.ReturnType : typeof(void);

        public static bool IsVoid(this MethodBase method) => method.GetReturnType() == typeof(void);
    }
}
