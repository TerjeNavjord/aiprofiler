using Xunit;

namespace TraceEventStubs.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void TraceLog_OpenOrConvert_Returns_Instance()
        {
            var inst = Microsoft.Diagnostics.Tracing.TraceEvent.TraceLog.OpenOrConvert("dummy.path");
            Assert.NotNull(inst);
        }
    }
}
