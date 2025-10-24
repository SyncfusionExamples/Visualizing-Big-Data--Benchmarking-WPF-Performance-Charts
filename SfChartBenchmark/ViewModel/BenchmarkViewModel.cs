using Syncfusion.UI.Xaml.Charts;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;

namespace SfChartBenchmark
{
    public class BenchmarkViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<double> LoadHistory { get; } = new(); 
        public ObservableCollection<double> UIRespHistory { get; } = new();
        public ObservableCollection<double> MemoryHistory { get; } = new();

        private const int DelayAfterLoadMs = 1200;

        private string _status = "Ready.";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        private string _selectedSeriesType = "FastLineSeries";

        public string SelectedSeriesType
        {
            get => _selectedSeriesType;
            set { _selectedSeriesType = value; OnPropertyChanged(); }
        }

        private bool _enableAA;
        public bool EnableAA
        {
            get => _enableAA;
            set { _enableAA = value; OnPropertyChanged(); }
        }

        public string LoadMetricsText { get => _loadMetricsText; private set { _loadMetricsText = value; OnPropertyChanged(); } }
        public string PanMetricsText { get => _panMetricsText; private set { _panMetricsText = value; OnPropertyChanged(); } }
        public string ZoomMetricsText { get => _zoomMetricsText; private set { _zoomMetricsText = value; OnPropertyChanged(); } }
        public string MemoryMetricsText { get => _memoryMetricsText; private set { _memoryMetricsText = value; OnPropertyChanged(); } }
        public string UiRespMetricsText { get => _uiRespMetricsText; private set { _uiRespMetricsText = value; OnPropertyChanged(); } }

        private string _loadMetricsText = "-";
        private string _panMetricsText = "-";
        private string _zoomMetricsText = "-";
        private string _memoryMetricsText = "-";
        private string _uiRespMetricsText = "-";

        public ICommand StartBenchmarkCommand { get; }
        public ICommand ClearCommand { get; }

        public SfChart? Chart { get; set; }

        private readonly int seriesCount = 10;
        private readonly int pointsPerSeries = 100_000;
        private readonly Random rng = new(7);
        private Random randomNumber = new();
        private List<ObservableCollection<DataModel>> dataset = new();

        private readonly Dictionary<string, BenchmarkResult> results = new();

        private readonly List<double> frameTimes = new();
        private readonly Stopwatch frameWatch = new();

        public BenchmarkViewModel()
        {
            StartBenchmarkCommand = new RelayCommand(async _ => await RunBenchmarkAsync(), _ => Chart != null);
            ClearCommand = new RelayCommand(_ => Clear());

            GenerateDataset();
        }

        public void OnRenderingTick()
        {
            if (!frameWatch.IsRunning)
            {
                frameWatch.Restart();
                return;
            }

            frameWatch.Stop();
            frameTimes.Add(frameWatch.Elapsed.TotalMilliseconds);
            if (frameTimes.Count > 1000) frameTimes.RemoveAt(0);
            frameWatch.Restart();
        }

        private void GenerateDataset()
        {
            dataset.Clear();

            for (int s = 0; s < seriesCount; s++)
            {
                var col = new ObservableCollection<DataModel>();
                double y = rng.NextDouble() * 10.0; // common initial band

                for (int i = 0; i < pointsPerSeries; i++)
                {
                    col.Add(new DataModel(i, y));

                    if (randomNumber.NextDouble() > .5)
                    {
                        y += randomNumber.NextDouble();
                    }
                    else
                    {
                        y -= randomNumber.NextDouble();
                    }
                }

                dataset.Add(col);
            }
        }

        private ChartSeriesCollection CreateSeries(bool isBitmap, bool antiAliasing)
        {
            var list = new ChartSeriesCollection();
            for (int s = 0; s < seriesCount; s++)
            {
                if (isBitmap)
                {
                    list.Add(new FastLineBitmapSeries
                    {
                        ItemsSource = dataset[s],
                        XBindingPath = nameof(DataModel.XValue),
                        YBindingPath = nameof(DataModel.YValue),
                        EnableAntiAliasing = antiAliasing,
                        StrokeThickness = 1,
                        Stroke = new SolidColorBrush(ColorFromIndex(s))
                    });
                }
                else
                {
                    list.Add(new FastLineSeries
                    {
                        ItemsSource = dataset[s],
                        XBindingPath = nameof(DataModel.XValue),
                        YBindingPath = nameof(DataModel.YValue),
                        StrokeThickness = 1,
                        Stroke = new SolidColorBrush(ColorFromIndex(s))
                    });
                }
            }

            return list;
        }

        private static Color ColorFromIndex(int i)
        {
            Color[] palette =
            {
                Colors.DeepSkyBlue, Colors.Orange, Colors.LimeGreen, Colors.Violet, Colors.Gold,
                Colors.Tomato, Colors.MediumTurquoise, Colors.SlateBlue, Colors.HotPink, Colors.CadetBlue
            };
            return palette[i % palette.Length];
        }

        private string CurrentKey() =>
            SelectedSeriesType == "FastLineSeries"
                ? "FLS"
                : (EnableAA ? "FLB_AA" : "FLB");

        /// <summary>
        /// Runs the chart benchmark asynchronously, measuring load time, memory usage, and interaction performance metrics.
        /// </summary>
        /// <returns>A task that represents the asynchronous benchmark operation.</returns>
        public async Task RunBenchmarkAsync()
        {
            if (Chart == null) return;

            Status = "Benchmark Started";
            CommandManager.InvalidateRequerySuggested();

            // Reset interactions
            if (Chart.PrimaryAxis is ChartAxisBase2D x)
            {
                x.ZoomFactor = 1; x.ZoomPosition = 0;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            bool isBitmap = SelectedSeriesType != "FastLineSeries";
            bool enableAntiAliasing = isBitmap && EnableAA;

            ChartSeriesCollection seriesCollection = CreateSeries(isBitmap, enableAntiAliasing);
 
            long memBefore = GC.GetTotalMemory(true);
            Chart.Series.Clear();

            // Scenario 1: Measure load time
            var stopWatch = Stopwatch.StartNew();            
            
            Chart.Series = seriesCollection; 
            await WaitForChartRenderAsync();
            stopWatch.Stop();
            long memAfter = GC.GetTotalMemory(true);
            long exactLoadMs = stopWatch.ElapsedMilliseconds;

            double memMB = (memAfter - memBefore) / (1024.0 * 1024.0);

            Status = $"Chart loaded in {exactLoadMs} ms. Starting interaction tests...";

            var key = CurrentKey();
            if (!results.TryGetValue(key, out var r))
                r = new BenchmarkResult { SeriesType = key };

            r.InitialLoadMs = exactLoadMs;
            r.MemoryMB = memMB;
            results[key] = r;

            PushHistory(LoadHistory, exactLoadMs);
            PushHistory(MemoryHistory, memMB);

            LoadMetricsText = FormatThreeCases(results, x => $"{x.InitialLoadMs} ms", "Load Time");
            MemoryMetricsText = FormatThreeCases(results, x => $"{x.MemoryMB:F1} MB", "Memory");

            await WaitForRenderAsync();
            await Task.Delay(DelayAfterLoadMs);

            // Scenario 2: Pan/Scroll
            frameTimes.Clear();
            int panSteps = 30;
            stopWatch.Restart();
            for (int i = 0; i < panSteps; i++)
            {
                Chart.PrimaryAxis.ZoomFactor = 0.2;
                Chart.PrimaryAxis.ZoomPosition = i / (double)panSteps * (1.0 - Chart.PrimaryAxis.ZoomFactor);
                await WaitForChartRenderAsync();
            }
            stopWatch.Stop();
            long panMs = stopWatch.ElapsedMilliseconds;

            // Scenario 3: Zoom cycles
            frameTimes.Clear();
            stopWatch.Restart();
            for (int i = 0; i < 5; i++)
            {
                await ZoomToAsync(0.1, i * 0.15);
                await ZoomToAsync(0.02, i * 0.15);
                await ZoomToAsync(1.0, 0.0);
            }
            stopWatch.Stop();
            long zoomMs = stopWatch.ElapsedMilliseconds;

            double avgUiMs = frameTimes.Count > 0 ? frameTimes.Average() : 0;

            r.PanScrollMs = panMs;
            r.ZoomMs = zoomMs;
            r.AvgUIFrameMs = avgUiMs;

            results[key] = r;

            PushHistory(UIRespHistory, avgUiMs);

            PanMetricsText = FormatThreeCases(results, x => $"{x.PanScrollMs} ms", "Pan/Scroll");
            ZoomMetricsText = FormatThreeCases(results, x => $"{x.ZoomMs} ms", "Zoom");
            UiRespMetricsText = FormatThreeCases(results, x => $"{x.AvgUIFrameMs:F2} ms/frame", "UI Resp");

            Status = "Benchmark Completed!";
        }

        private static string FormatThreeCases(Dictionary<string, BenchmarkResult> src, Func<BenchmarkResult, string> sel, string? title = null, string? extraLabel = null)
        {
            string F(string k, string name)
            {
                if (!src.TryGetValue(k, out var r)) return $"{name}: -";
                return $"{name}: {sel(r)}";
            }
            return string.Join(" | ", new[]
            {
                F("FLS","FastLine"),
                F("FLB","FastLineBitmap"),
                F("FLB_AA","FastLineBitmap+AA")
            });
        }

        public void Clear()
        {
            Chart?.Series.Clear();
            results.Clear();
            LoadHistory.Clear();
            UIRespHistory.Clear();
            MemoryHistory.Clear();

            LoadMetricsText = PanMetricsText = ZoomMetricsText = MemoryMetricsText = UiRespMetricsText = "-";
            Status = "Cleared. Ready.";
        }

        private async Task ZoomToAsync(double factor, double position)
        {
            ChartAxisBase2D x = Chart!.PrimaryAxis;
            ChartAxisBase2D y = Chart.SecondaryAxis;
            x.ZoomFactor = Math.Clamp(factor, 0.001, 1.0);
            x.ZoomPosition = Math.Clamp(position, 0.0, 1.0 - x.ZoomFactor);
            y.ZoomFactor = Math.Clamp(factor, 0.001, 1.0);
            y.ZoomPosition = 0.0;

            await WaitForChartRenderAsync();
        }

        /// <summary>
        /// Returns a task that completes when the next WPF render pass occurs.
        /// </summary>
        /// <remarks>This method is useful for awaiting the next frame render in WPF applications, such as
        /// when synchronizing UI updates with the rendering cycle. The returned task completes after a single render
        /// pass; subsequent calls are required to await additional render events.</remarks>
        /// <returns>A task that is completed after the next CompositionTarget.Rendering event is raised.</returns>
        private static Task WaitForRenderAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            void Handler(object? s, EventArgs e)
            {
                CompositionTarget.Rendering -= Handler;
                tcs.TrySetResult(true);
            }
            CompositionTarget.Rendering += Handler;

            return tcs.Task;
        }

        /// <summary>
        /// Waits asynchronously for the chart to complete its rendering process.
        /// </summary>
        /// <remarks>This method ensures that the chart has fully rendered by waiting for two consecutive
        /// render cycles. Use this method when subsequent operations depend on the chart's visual state being up to
        /// date.</remarks>
        /// <returns>A task that represents the asynchronous wait operation.</returns>
        private async Task WaitForChartRenderAsync()
        {
            // two frames to ensure layout + render
            await WaitForRenderAsync();
            await WaitForRenderAsync();
        }

        private static void PushHistory(ObservableCollection<double> col, double value)
        {
            col.Add(value);
            while (col.Count > 10) col.RemoveAt(0);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? p = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
