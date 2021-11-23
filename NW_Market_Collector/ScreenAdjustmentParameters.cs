using System;
using System.Drawing;

namespace NW_Market_Collector
{
    public class ScreenAdjustmentParameters
    {
        public ScreenAdjustmentParameters()
        {
            Scale = 1f;
            XPadding = 0f;
        }

        public float XPadding;
        public float Scale;

        public Rectangle Adjust(Rectangle rect)
        {
            return new Rectangle
            {
                X = (int)Math.Round(XPadding + (Scale * rect.X)),
                Y = (int)Math.Round(Scale * rect.Y),
                Width = (int)Math.Round(Scale * rect.Width),
                Height = (int)Math.Round(Scale * rect.Height),
            };
        }
    }
}
