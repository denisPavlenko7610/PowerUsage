using System;
using System.Globalization;
using System.IO;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using LibreHardwareMonitor.Hardware;
using Timer = System.Timers.Timer;

namespace PowerMonitorWPF
{
    public partial class MainWindow : Window
    {
        private readonly Computer _computer;
        private readonly Timer _timer;

        public MainWindow()
        {
            InitializeComponent();
            LoadWindowPosition();

            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true
            };
            _computer.Open();

            _timer = new Timer(5000);
            _timer.Elapsed += UpdatePowerUsage;
            _timer.Start();

            MouseLeftButtonDown += (_, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    DragMove();
            };

            Closing += (_, e) => SaveWindowPosition();
        }

        private void UpdatePowerUsage(object sender, ElapsedEventArgs e)
        {
            double totalWatts = 0.0;

            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue)
                        totalWatts += sensor.Value.Value;
                }
            }

            Dispatcher.Invoke(() => PowerLabel.Content = $"Power usage: {totalWatts:F1} W");
        }

        private void LoadWindowPosition()
        {
            const string path = "window_position.txt";
            if (!File.Exists(path)) return;

            var lines = File.ReadAllLines(path);
            if (lines.Length < 2) return;

            if (double.TryParse(lines[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var left) &&
                double.TryParse(lines[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var top) &&
                IsPositionVisible(left, top))
            {
                Left = left;
                Top = top;
            }
        }

        private static bool IsPositionVisible(double left, double top)
        {
            return left >= SystemParameters.VirtualScreenLeft &&
                   left <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
                   top >= SystemParameters.VirtualScreenTop &&
                   top <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
        }

        private void SaveWindowPosition()
        {
            const string path = "window_position.txt";
            var lines = new[]
            {
                Left.ToString(CultureInfo.InvariantCulture),
                Top.ToString(CultureInfo.InvariantCulture)
            };
            File.WriteAllLines(path, lines);
        }
    }
}