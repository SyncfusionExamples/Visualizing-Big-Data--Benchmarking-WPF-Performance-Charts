namespace SfChartBenchmark
{
    public class BenchmarkResult
    {
        public string SeriesType { get; set; } = "";
        public long InitialLoadMs { get; set; }
        public long PanScrollMs { get; set; }
        public long ZoomMs { get; set; }
        public double MemoryMB { get; set; }
        public double AvgUIFrameMs { get; set; }
        public override string ToString() =>
            $"{SeriesType}: Load={InitialLoadMs} ms, Pan/Scroll={PanScrollMs} ms, Zoom={ZoomMs} ms, Mem={MemoryMB:F1} MB, UI={AvgUIFrameMs:F2} ms";
    }
}
