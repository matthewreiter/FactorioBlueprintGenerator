using System;

namespace FactoVision.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method, Inherited = false)]
    public class InlineAttribute : Attribute { }
}
