using System;

namespace PostProcessTests.Stubs
{
    // Minimal stub types to exercise version-aware reflection paths.
    public static class TraceLogV2
    {
        public static object OpenOrConvert(string path)
        {
            return new TraceLogV2Instance(path);
        }
    }

    public class TraceLogV2Instance
    {
        private readonly string _path;
        public TraceLogV2Instance(string path) { _path = path; }
        public string EtlxFileName => System.IO.Path.ChangeExtension(_path, ".etlx");
    }
}
