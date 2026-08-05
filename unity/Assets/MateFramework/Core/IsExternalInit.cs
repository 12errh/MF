// Unity's .NET 4.x profile does not define IsExternalInit, which C# record types
// require. This shim provides it so the shared Mate.Core records compile under Unity.
// Under .NET 8 (runtime/) the BCL already defines it, so this file lives only in the
// Unity copy and is not part of the runtime/Mate.Core source.
namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit { }
}