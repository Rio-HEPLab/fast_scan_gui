using System;
using System.Windows;
using System.Windows.Threading;

namespace FAST_Scan
{
    public partial class GraphWindow : Window
    {
        private DispatcherTimer timer;
        private Random rand = new Random();

        private double[] x;
        private double[] y;

        public GraphWindow()
        {
            InitializeComponent();

            // desativa interação do usuário
            PlotArea.UserInputProcessor.IsEnabled = false;

            int size = 1024;

            x = new double[size];
            y = new double[size];

            for (int i = 0; i < size; i++)
                x[i] = i;

            PlotArea.Plot.Add.Scatter(x, y);

            PlotArea.Plot.Title("Random Vector (1024 pontos)");
            PlotArea.Plot.XLabel("Index");
            PlotArea.Plot.YLabel("Value");

            PlotArea.Plot.Axes.SetLimits(
                left: 0,
                right: 1023,
                bottom: -100,
                top: 100);

            PlotArea.Refresh();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(2);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            for (int i = 0; i < y.Length; i++)
                y[i] = rand.Next(-100, 101);

            PlotArea.Refresh();
        }
    }
}