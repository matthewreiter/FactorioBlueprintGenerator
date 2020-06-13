using System;
using System.Reflection;

namespace CompilerCommon
{
    public static class MethodBaseExtensions
    {
        public static Type GetReturnType(this MethodBase method) => method is MethodInfo ? ((MethodInfo)method).ReturnType : typeof(void);
    }
}
