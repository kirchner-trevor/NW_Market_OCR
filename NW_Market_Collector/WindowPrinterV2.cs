using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace NW_Market_Collector
{
    class WindowPrinterV2
    {
        public static Bitmap PrintWindow(IntPtr hwnd)
        {
            var rect = new User32.Rect();
            User32.GetWindowRect(hwnd, ref rect);

            int width = rect.right - rect.left;
            int height = rect.bottom - rect.top;

            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bmp))
            {
                graphics.CopyFromScreen(rect.left, rect.top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            }

            return bmp;
        }

        public static IntPtr GetHandleOfFocusedWindowWithName(string name)
        {
            Process process = Process
                .GetProcesses()
                .SingleOrDefault(x => x.MainWindowTitle.ToLowerInvariant().Equals(name.ToLowerInvariant()));

            IntPtr focus = User32.GetForegroundWindow();

            return process != null && process.MainWindowHandle == focus ? process.MainWindowHandle : IntPtr.Zero;
        }

        private class User32
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct Rect
            {
                public int left;
                public int top;
                public int right;
                public int bottom;
            }

            [DllImport("user32.dll")]
            public static extern IntPtr GetWindowRect(IntPtr hWnd, ref Rect rect);

            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();
        }
    }
}
