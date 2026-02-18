using System;

namespace Microsoft.Diagnostics.Tracing.TraceEvent
{
    // Minimal compatibility stubs that mimic the TraceEvent TraceLog shapes used by the postprocessor.
    public static class TraceLog
    {
        // OpenOrConvert(string) -> returns a TraceLog-like object
        public static object OpenOrConvert(string path)
        {
            return new TraceLogInstance(path);
        }

        // Some versions provide Open(string)
        public static object Open(string path)
        {
            return new TraceLogInstance(path);
        }
    }

    public class TraceLogInstance
    {
        private readonly string _path;
        public TraceLogInstance(string path) { _path = path; }
        public string EtlxFileName => System.IO.Path.ChangeExtension(_path, ".etlx");
    }
}
