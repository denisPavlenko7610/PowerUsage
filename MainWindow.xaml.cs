using System;
using System.Globalization;
using System.IO;
using System.Management;
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
                IsMotherboardEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true,
                IsControllerEnabled = true,
                IsNetworkEnabled = true
            };
            _computer.Open();

            _timer = new Timer(10000);
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
            double measured = 0;
            double cpuPower = 0, gpuPower = 0, mbPower = 0;

            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue && sensor.Value.Value > 0.5)
                    {
                        // CPU
                        if (hw.HardwareType == HardwareType.Cpu && (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) || sensor.Name.Contains("CPU Total", StringComparison.OrdinalIgnoreCase)))
                            cpuPower += sensor.Value.Value;
                        // GPU
                        else if ((hw.HardwareType == HardwareType.GpuNvidia || hw.HardwareType == HardwareType.GpuAmd) &&
                                 (sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase) || sensor.Name.Contains("Power", StringComparison.OrdinalIgnoreCase)))
                            gpuPower += sensor.Value.Value;
                        // Motherboard
                        else if (hw.HardwareType == HardwareType.Motherboard)
                            mbPower += sensor.Value.Value;
                        // Other power sensors
                        else
                            measured += sensor.Value.Value;
                    }
                }
            }

            // If CPU/GPU/Motherboard sensors are not found, fallback to sum of all power sensors
            double mainMeasured = cpuPower + gpuPower + mbPower;
            if (mainMeasured < 1.0)
                mainMeasured = measured;

            double memoryPower = EstimateMemoryPower();
            double diskPower = EstimateDiskPower();
            int fanCount = GetFanCount();
            double fanPower = fanCount * 2.0;

            double miscPower = 10; // Chipset, USB, LEDs, etc.

            double totalRaw = mainMeasured + memoryPower + diskPower + fanPower + miscPower;
            double psuCompensated = totalRaw / 0.85;
            double errorCompensation = psuCompensated * 1.10;

            Dispatcher.Invoke(() =>
            {
                PowerLabel.Content =
                    //$"CPU: {cpuPower:F1} W\n" +
                    //$"GPU: {gpuPower:F1} W\n" +
                    //$"MB: {mbPower:F1} W\n" +
                    //$"RAM: {memoryPower:F1} W\n" +
                    //$"Disk: {diskPower:F1} W\n" +
                    //$"Fans: {fanPower:F1} W ({fanCount} fans)\n" +
                    //$"Misc: {miscPower:F1} W\n" +
                    //$"-------------------\n" +
                    $"Power usage: {errorCompensation:F1}W";
            });
        }

        private int GetFanCount()
        {
            int count = 0;
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.SensorType == SensorType.Fan)
                        count++;
                }
            }
            return count > 0 ? count : 3;
        }

        private double EstimateMemoryPower()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
                int count = 0;
                foreach (var _ in searcher.Get()) count++;
                return count * 3.0;
            }
            catch
            {
                return 6.0;
            }
        }

        private double EstimateDiskPower()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                double total = 0;
                foreach (ManagementObject drive in searcher.Get())
                {
                    string mediaType = (drive["MediaType"] ?? "").ToString().ToLower();
                    if (mediaType.Contains("ssd"))
                        total += 3.0;
                    else
                        total += 6.0;
                }
                return total > 0 ? total : 6.0;
            }
            catch
            {
                return 6.0;
            }
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
    }
}