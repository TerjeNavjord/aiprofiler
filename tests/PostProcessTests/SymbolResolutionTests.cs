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
            // Diagnostic: ensure the test SimpleResolver type is visible in loaded assemblies
            bool found = false;
            foreach (var a in assemblies)
            {
                try
                {
                    var types = a.GetTypes();
                    foreach (var t in types)
                    {
                        if (string.Equals(t.Name, "SimpleResolver", StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            Console.WriteLine($"DIAG: Found SimpleResolver in assembly: {a.GetName().Name} ({t.FullName})");
                            break;
                        }
                    }
                    if (found) break;
                }
                catch (Exception ex) { Console.WriteLine($"DIAG: Could not inspect assembly {a.GetName().Name}: {ex.Message}"); }
            }
            Assert.True(found, "SimpleResolver type not found in loaded assemblies - resolver fallback cannot run");
            var mi = typeof(PostProcessCommand).GetMethod("TryResolveSymbolsOnHotspots", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);
            mi!.Invoke(null, new object[] { hotspots, assemblies, diagnostics });

            // Dump diagnostics to help debugging when running tests locally
            foreach (var d in diagnostics)
            {
                Console.WriteLine("DIAG: " + d);
            }

            // Ensure we explicitly observed the resolver acceptance in diagnostics so the test
            // asserts behavior instead of inferring it solely from the hotspots map.
            bool accepted = diagnostics.Exists(d => d.IndexOf("Accepted SimpleResolver mapping for", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.True(accepted, "Diagnostics did not contain accepted SimpleResolver mapping message. Full diagnostics: " + string.Join(" | ", diagnostics));

            // Assert: hotspots should be remapped by the stub resolver
            Assert.Single(hotspots);
            var resolvedName = hotspots.Keys.GetEnumerator(); resolvedName.MoveNext();
            var name = resolvedName.Current;
            Assert.Contains("StubModule!Function", name, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(5, hotspots[name]);
        }
    }
}
