#if NETSTANDARD2_1
// Compiler-known attributes missing from netstandard2.1, required for init accessors and records.

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit;
#endif
