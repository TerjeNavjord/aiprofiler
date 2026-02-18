using System;
using System.IO;
using System.Reflection;
using Xunit;
using DotAi.Commands;

namespace PostProcessTests
{
    public class VersionedTraceEventTests
    {
        [Fact]
        public void TryTraceLogConvert_Targeted_With_ToolsLoaded_Works()
        {
            // Arrange: ensure TryLoadTraceEventFromTools can be invoked (it will be a no-op in tests)
            var miLoad = typeof(PostProcessCommand).GetMethod("TryLoadTraceEventFromTools", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(miLoad);
            miLoad!.Invoke(null, new object[] { null });

            // create a temp .nettrace file
            var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            File.WriteAllText(tmp, "stub content");

            var mi = typeof(PostProcessCommand).GetMethod("TryTraceLogConvert_Targeted", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);
            var ret = mi!.Invoke(null, new object[] { tmp, null, null, null });
            Assert.IsType<bool>(ret);
            var ok = (bool)ret!;

            Assert.True(ok);

            try { File.Delete(tmp); } catch { }
        }
    }
}
