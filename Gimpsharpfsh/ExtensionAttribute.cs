namespace System.Runtime.CompilerServices
{
    // a little hack to force extension methods to work under .NET 2.0
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    internal sealed class ExtensionAttribute : Attribute
    {
    }
}
