using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace NMS_Reader.Services
{
    public class TrayIconService : IDisposable
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;

        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;

        public const int WM_TRAYICON = 0x0400 + 101;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        private readonly Window _window;
        private IntPtr _hWnd;
        private bool _isCreated = false;
        private NOTIFYICONDATA _nid;

        public event Action? OnRequestScan;
        public event Action? OnRequestExit;

        public TrayIconService(Window window)
        {
            _window = window;
        }

        public void Initialize()
        {
            var helper = new WindowInteropHelper(_window);
            _hWnd = helper.Handle;

            _nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = 1001,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = SystemIcons.Application.Handle,
                szTip = "NMS_Reader // NMS Lore Companion"
            };

            _isCreated = Shell_NotifyIcon(NIM_ADD, ref _nid);
        }

        public void HandleMessage(int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAYICON)
            {
                int mouseEvent = lParam.ToInt32();
                if (mouseEvent == WM_LBUTTONUP || mouseEvent == WM_LBUTTONDBLCLK)
                {
                    RestoreWindow();
                }
                else if (mouseEvent == WM_RBUTTONUP)
                {
                    ShowContextMenu();
                }
            }
        }

        public void RestoreWindow()
        {
            _window.Show();
            if (_window.WindowState == WindowState.Minimized)
            {
                _window.WindowState = WindowState.Normal;
            }
            _window.Activate();
            SetForegroundWindow(_hWnd);
        }

        private void ShowContextMenu()
        {
            SetForegroundWindow(_hWnd);

            var menu = new ContextMenu
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 27, 38)),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 40, 56)),
                BorderThickness = new Thickness(1),
                FontSize = 12
            };

            var openItem = new MenuItem { Header = "Открыть NMS_Reader", FontWeight = FontWeights.Bold };
            openItem.Click += (s, e) => RestoreWindow();

            var scanItem = new MenuItem { Header = "Запустить чтение (F4)" };
            scanItem.Click += (s, e) => OnRequestScan?.Invoke();

            var exitItem = new MenuItem { Header = "Выход" };
            exitItem.Click += (s, e) => OnRequestExit?.Invoke();

            menu.Items.Add(openItem);
            menu.Items.Add(scanItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(exitItem);

            menu.IsOpen = true;
        }

        public void Dispose()
        {
            if (_isCreated)
            {
                Shell_NotifyIcon(NIM_DELETE, ref _nid);
                _isCreated = false;
            }
        }
    }
}
