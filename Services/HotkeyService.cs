using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NMS_Reader.Services
{
    public class HotkeyService : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_NONE = 0x0000;

        private const uint VK_F3 = 0x72;
        private const uint VK_F4 = 0x73;

        private IntPtr _hWnd;
        private HwndSource? _source;
        private readonly Dictionary<int, Action> _callbacks = new();
        private int _currentId = 1;

        public event Action? OnDialogHotkeyPressed;
        public event Action? OnStopHotkeyPressed;

        public void Initialize(Window window, HwndSourceHook? extraHook = null)
        {
            var helper = new WindowInteropHelper(window);
            _hWnd = helper.Handle;
            _source = HwndSource.FromHwnd(_hWnd);
            _source?.AddHook(HwndHook);
            if (extraHook != null)
            {
                _source?.AddHook(extraHook);
            }

            Register(MOD_NONE, VK_F4, () => OnDialogHotkeyPressed?.Invoke());
            Register(MOD_NONE, VK_F3, () => OnStopHotkeyPressed?.Invoke());
        }

        private void Register(uint modifier, uint vk, Action callback)
        {
            int id = _currentId++;
            if (RegisterHotKey(_hWnd, id, modifier, vk))
            {
                _callbacks[id] = callback;
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_callbacks.TryGetValue(id, out var action))
                {
                    action?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            foreach (var id in _callbacks.Keys)
            {
                UnregisterHotKey(_hWnd, id);
            }
            _callbacks.Clear();
            _source?.RemoveHook(HwndHook);
            _source = null;
        }
    }
}
