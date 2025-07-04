using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using WinRT.Interop;
using Windows.Graphics;
using System.Threading.Tasks;

namespace palew1n
{
    public sealed partial class MainWindow : Window
    {
        private readonly Random _random = new Random();

        public MainWindow()
        {
            InitializeComponent();

            // all of this to load the app properly oh my gooodd

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new SizeInt32 { Width = 672, Height = 468 });

            if (appWindow.Presenter is OverlappedPresenter presenter)
                presenter.IsResizable = false;

            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(null);

            var titleBar = appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.ButtonBackgroundColor = Colors.Transparent;

            Start();
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Colors.White;
        }

        private async void scan_devices()
        {
            await Task.Delay(2000);
            var root = new TreeViewNode() { Content = "Phones", IsExpanded = false };
            devices.RootNodes.Add(root);

            var phone = new TreeViewNode() { Content = "iPhone 7 Demo", IsExpanded = false };
            root.Children.Add(phone);

            await Task.Delay(5000);

            devices.RootNodes.Remove(root);
            devices.RootNodes.Add(phone);

            usbimg.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/usb.png"));
            ConnectText.Text = "USB Connected";
        ConnectText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.LightGreen); 
            InfoText.Text = phone.Content + " is connected via USB\nDo not unplug the device from this point on.";

            devices.SelectedNode = phone;
            phone.Content = phone.Content + " (EXAMPLE_QEMU_ID)";
        }

        private async void Palen1xButton_Click(object sender, RoutedEventArgs e)
        {
            var previous_qemu = QemuButton.IsEnabled;
            Palen1xButton.IsEnabled = false;
            QemuButton.IsEnabled = false;

            for (int i = 0; i <= 100; i++)
            {
                Palen1xProgress.Value = i;
                await System.Threading.Tasks.Task.Delay(10);
            }

            Palen1xButton.IsEnabled = false;
            QemuButton.IsEnabled = previous_qemu;

            CheckCompletion();
        }

        private async void QemuButton_Click(object sender, RoutedEventArgs e)
        {
            var previouspalen1x = Palen1xButton.IsEnabled;
            QemuButton.IsEnabled = false;
            Palen1xButton.IsEnabled = false;

            for (int i = 0; i <= 100; i++)
            {
                QemuProgress.Value = i;
                await System.Threading.Tasks.Task.Delay(10);
            }

            QemuButton.IsEnabled = false;
            Palen1xButton.IsEnabled = previouspalen1x;

            CheckCompletion();
        }

        private async void CheckCompletion()
        {
            if (!Palen1xButton.IsEnabled && !QemuButton.IsEnabled)
            {
                Palen1xButton.Visibility = Visibility.Collapsed;
                QemuButton.Visibility = Visibility.Collapsed;
                ManualFindText.Visibility = Visibility.Collapsed;
                CheckmarkText.Visibility = Visibility.Visible;

                await System.Threading.Tasks.Task.Delay(1000);

                FadeOutAnimation.Begin();
                await Task.Delay(1000);

                DownloadScreen.Visibility = Visibility.Collapsed;
                MainGrid.Visibility = Visibility.Visible;
                FadeInMainGridAnimation.Begin();

                scan_devices();
            }
        }

        private void update_message(string message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusTextBlock.Text = message;
                FadeInStatusAnimation.Begin();
            });
        }

        private async void Start()
        {
            DispatcherQueue.TryEnqueue(() => FadeOutStatusAnimation.Begin());
            await Task.Delay(500);

            update_message("Checking system architecture");
            var is64Bit = Environment.Is64BitOperatingSystem;
            await Task.Delay(50);
            update_message("Looking for required files");
            var requiredFiles = new List<string>
            {
                "palen1x-*.iso",
                "qemu.exe"
            };

            var files = Directory.GetFiles(Directory.GetCurrentDirectory());
            bool palen1xFound = files.Any(f => Path.GetFileName(f).IndexOf("pale", StringComparison.OrdinalIgnoreCase) >= 0);
            bool qemuFound = files.Any(f => Path.GetFileName(f).IndexOf("qemu", StringComparison.OrdinalIgnoreCase) >= 0);

            var missingFiles = new List<string>();
            
            if (!palen1xFound)
                missingFiles.Add("palen1x-v1.1.8-x86_64.iso");
            else if (palen1xFound)
                Palen1xButton.IsEnabled = false;
            if (!qemuFound)
                missingFiles.Add("qemu.exe");
            else if (qemuFound)
                QemuButton.IsEnabled = false;

            if (missingFiles.Any())
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    FadeOutLoadingScreenAnimation.Begin();
                });

                await Task.Delay(500);

                DispatcherQueue.TryEnqueue(() =>
                {
                    LoadingScreen.Visibility = Visibility.Collapsed;
                    DownloadScreen.Visibility = Visibility.Visible;
                    FadeInDownloadScreenAnimation.Begin();
                });
            }

            await Task.Delay(500);
        }
    }
}
