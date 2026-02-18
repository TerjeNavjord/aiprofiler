using System;
using System.Threading;

class Program
{
    static void Main()
    {
        Console.WriteLine("TestApp starting");
        while (true)
        {
            BurnCpu();
            Thread.Sleep(50);
        }
    }

    static void BurnCpu()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double x = 0;
        while (sw.ElapsedMilliseconds < 20)
        {
            for (int i = 1; i < 1000; i++) x += Math.Sqrt(i) * Math.PI;
        }
    }
}
