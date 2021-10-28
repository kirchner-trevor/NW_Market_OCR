using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace NW_Market_OCR
{
    class AutoInput
    {
        private const int MOUSEEVENTF_WHEEL = 0x0800;
        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;

        public static void ScrollDown(int lines)
        {
            int totalLinesScrolled = 0;
            while (totalLinesScrolled < lines)
            {
                int scrollLines = new Random().Next(5, 8);
                for (int i = 0; i < scrollLines; i++)
                {
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, -120, (IntPtr)0);
                    SmallScrollSleep();
                }
                BigScrollSleep();
                totalLinesScrolled += scrollLines;
            }
        }

        public static void Click()
        {
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, Cursor.Position.X, Cursor.Position.Y, 0, (IntPtr)0);
        }


        public static void MouseEntropy(Point originalMousePosition)
        {
            if (originalMousePosition == default)
            {
                originalMousePosition = Cursor.Position;
            }

            int X = Cursor.Position.X + (new Random().Next(0, 10) > 7 ? new Random().Next(-4, 4) : 0);
            int Y = Cursor.Position.Y + (new Random().Next(0, 10) > 7 ? new Random().Next(-4, 4) : 0);

            if (Math.Abs(originalMousePosition.X - X) > 10 || Math.Abs(originalMousePosition.X - X) > 10)
            {
                X = originalMousePosition.X + new Random().Next(-7, 7);
                Y = originalMousePosition.Y + new Random().Next(-7, 7);
            }

            Cursor.Position = new Point(X, Y);
        }

        private static void SmallScrollSleep()
        {
            Thread.Sleep(50 + new Random().Next(-30, 30));
        }

        private static void BigScrollSleep()
        {
            Thread.Sleep(150 + new Random().Next(-50, 50));
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, IntPtr dwExtraInfo);
    }
}
