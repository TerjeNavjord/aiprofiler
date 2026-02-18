using System;

namespace SymbolResolverStubs
{
    // A simple stub resolver exposing methods the fallback heuristics will call.
    public class SimpleResolver
    {
        public string Resolve(ulong addr)
        {
            return $"StubModule!Function+0x{addr:x}";
        }

        public string Resolve(string module, ulong offset)
        {
            return $"{module}!StubSymbol+0x{offset:x}";
        }
    }
}

namespace Microsoft.Diagnostics.Symbols
{
    // Provide a tiny SymbolReader-like stub so TryUseMicrosoftDiagnosticsSymbols finds a canonical type.
    public class SymbolReader
    {
        public SymbolReader() { }

        public string Resolve(ulong addr)
        {
            return $"StubModule!Function+0x{addr:x}";
        }

        public string Resolve(string module, ulong offset)
        {
            return $"{module}!StubSymbol+0x{offset:x}";
        }
    }
}
