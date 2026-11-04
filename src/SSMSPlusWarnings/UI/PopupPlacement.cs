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

        /// <summary>Returns the height available below the popup in the owner, in device independent pixels (null when unknown).</summary>
        public static double? Place(IntPtr popup, IntPtr owner)
        {
            if (popup == IntPtr.Zero || owner == IntPtr.Zero || IsIconic(owner))
                return null;

            if (!GetVisibleBounds(owner, out RECT ownerBounds) || !GetWindowRect(popup, out RECT popupBounds))
                return null;

            double scale = GetScale(owner);
            int x = ownerBounds.Right - (popupBounds.Right - popupBounds.Left) - (int)Math.Round(RightMargin * scale);
            int y = ownerBounds.Top + (int)Math.Round(TopOffset * scale);
            if (x != popupBounds.Left || y != popupBounds.Top)
                SetWindowPos(popup, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

            return (ownerBounds.Bottom - y) / scale - BottomMargin;
        }

        /// <summary>Clicking the popup must not take the focus from the editor.</summary>
        public static void MakeNonActivating(IntPtr popup)
        {
            int style = GetWindowLong(popup, GWL_EXSTYLE);
            SetWindowLong(popup, GWL_EXSTYLE, style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
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

            return GetWindowRect(hwnd, out bounds);
        }

        private static double GetScale(IntPtr hwnd)
        {
            try
            {
                uint dpi = GetDpiForWindow(hwnd);
                return dpi > 0 ? dpi / 96.0 : 1.0;
            }
            catch (EntryPointNotFoundException)
            {
                return 1.0;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

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
