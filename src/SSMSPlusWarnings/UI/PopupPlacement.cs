namespace SSMSPlusWarnings.UI
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>Places the popup at the top right corner of the SSMS main window, in screen pixels.</summary>
    internal static class PopupPlacement
    {
        /// <summary>Below the title bar, in device independent pixels.</summary>
        public const int TopOffset = 36;

        public const int RightMargin = 16;

        /// <summary>Kept free above the status bar.</summary>
        public const int BottomMargin = 40;

        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        /// <summary>
        /// Returns the height available below the popup in the owner, in device independent pixels (null when unknown).
        /// </summary>
        /// <remarks>
        /// Placed in physical pixels: when SSMS is not per monitor DPI aware ("Optimize rendering for screens with different
        /// pixel densities" off, monitors with different scaling), Windows scales the window coordinates of the thread but not the
        /// DWM frame bounds. The thread is switched to per monitor aware while placing, so that all the coordinates are physical.
        /// </remarks>
        public static double? Place(IntPtr popup, IntPtr owner)
        {
            if (popup == IntPtr.Zero || owner == IntPtr.Zero || IsIconic(owner))
                return null;

            var previousContext = SwitchToPerMonitorAware();
            try
            {
                if (!GetVisibleBounds(owner, out RECT ownerBounds) || !GetWindowRect(popup, out RECT popupWindow) || !GetVisibleBounds(popup, out RECT popupVisible))
                    return null;

                double scale = GetMonitorScale(owner);
                // the visible popup at the top right of the visible owner; SetWindowPos takes the window rectangle (with its invisible borders)
                int visibleWidth = popupVisible.Right - popupVisible.Left;
                int x = ownerBounds.Right - visibleWidth - (int)Math.Round(RightMargin * scale) - (popupVisible.Left - popupWindow.Left);
                int y = ownerBounds.Top + (int)Math.Round(TopOffset * scale) - (popupVisible.Top - popupWindow.Top);
                if (x != popupWindow.Left || y != popupWindow.Top)
                    SetWindowPos(popup, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

                return (ownerBounds.Bottom - y) / scale - BottomMargin;
            }
            finally
            {
                if (previousContext != IntPtr.Zero)
                    SetThreadDpiAwarenessContext(previousContext);
            }
        }

        /// <summary>Clicking the popup must not take the focus from the editor.</summary>
        public static void MakeNonActivating(IntPtr popup)
        {
            int style = GetWindowLong(popup, GWL_EXSTYLE);
            SetWindowLong(popup, GWL_EXSTYLE, style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        /// <summary>Returns the previous context to restore, IntPtr.Zero when unchanged (before Windows 10 1607).</summary>
        private static IntPtr SwitchToPerMonitorAware()
        {
            try
            {
                return SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            catch (EntryPointNotFoundException)
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>The window without its invisible resize borders (a maximized window overflows the screen).</summary>
        private static bool GetVisibleBounds(IntPtr hwnd, out RECT bounds)
        {
            try
            {
                if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out bounds, Marshal.SizeOf(typeof(RECT))) == 0)
                    return true;
            }
            catch (DllNotFoundException)
            {
            }

            if (!GetWindowRect(hwnd, out bounds))
                return false;

            // maximized: the borders are outside the screen, keep the screen part
            if (IsZoomed(hwnd))
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info))
                {
                    bounds.Left = Math.Max(bounds.Left, info.rcMonitor.Left);
                    bounds.Top = Math.Max(bounds.Top, info.rcMonitor.Top);
                    bounds.Right = Math.Min(bounds.Right, info.rcMonitor.Right);
                    bounds.Bottom = Math.Min(bounds.Bottom, info.rcMonitor.Bottom);
                }
            }

            return true;
        }

        /// <summary>Physical pixels per device independent pixel on the monitor of the window (its Windows scaling).</summary>
        private static double GetMonitorScale(IntPtr hwnd)
        {
            try
            {
                if (GetDpiForMonitor(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0)
                    return dpiX / 96.0;
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
            }

            return 1.0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);
    }
}
