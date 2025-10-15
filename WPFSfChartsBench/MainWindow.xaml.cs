using Syncfusion.UI.Xaml.Charts;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFSfChartsBench
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public BenchmarkViewModel ViewModel { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = ViewModel;

            Loaded += (_, __) =>
            {
                ViewModel.Chart = Chart;
                CompositionTarget.Rendering += (_, __2) => ViewModel.OnRenderingTick();
            };
        }

        // Keep your adaptive behavior for restored window
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ActualHeight < 820) BottomExpander.IsExpanded = false;
            else BottomExpander.IsExpanded = true;
        }
    }
}