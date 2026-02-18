using System;
using System.IO;
using Xunit;
using DotAi.Commands;

namespace PostProcessTests
{
    public class TraceLogConversionTests
    {
        [Fact]
        public void TryTraceLogConvert_Targeted_Finds_Stub_TraceLog()
        {
            // Arrange: create a temp .nettrace file
            var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            File.WriteAllText(tmp, "stub content");

            // Act: invoke the internal try method (returns bool)
            var mi = typeof(PostProcessCommand).GetMethod("TryTraceLogConvert_Targeted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(mi);
            var ret = mi!.Invoke(null, new object[] { tmp, null, null, null });
            Assert.IsType<bool>(ret);
            var ok = (bool)ret!;

            // Assert: should be true because our stub TraceEvent.TraceLog is available in test project
            Assert.True(ok);

            // Cleanup
            try { File.Delete(tmp); } catch { }
        }
    }
}
