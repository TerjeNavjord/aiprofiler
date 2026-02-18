using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using DotAi.Commands;

namespace PostProcessTests
{
    public class SymbolResolutionTests
    {
        [Fact]
        public void TryUseMicrosoftDiagnosticsSymbols_Remaps_Hotspots_With_StubResolver()
        {
            // Arrange: a hotspot keyed by a hex address that our SimpleResolver should resolve
            ulong addr = 0x1A2BUL;
            var key = $"0x{addr:X}"; // uppercase as produced by TryUseMicrosoftDiagnosticsSymbols
            var hotspots = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { { key, 5 } };
            var diagnostics = new List<string>();

            // Act: invoke the private TryResolveSymbolsOnHotspots via reflection (fallback path)
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var mi = typeof(PostProcessCommand).GetMethod("TryResolveSymbolsOnHotspots", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);
            mi!.Invoke(null, new object[] { hotspots, assemblies, diagnostics });

            // Assert: hotspots should be remapped by the stub resolver
            Assert.Single(hotspots);
            var resolvedName = hotspots.Keys.GetEnumerator(); resolvedName.MoveNext();
            var name = resolvedName.Current;
            Assert.Contains("StubModule!Function", name, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(5, hotspots[name]);
        }
    }
}
