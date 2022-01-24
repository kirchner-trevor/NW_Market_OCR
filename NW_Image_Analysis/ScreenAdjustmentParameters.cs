using SixLabors.ImageSharp;
using System;

namespace NW_Image_Analysis
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

        public float XMargin;
        public float TopMargin;
        public float BottomMargin;

        public Rectangle Adjust(Rectangle rect)
        {
            return new Rectangle
            {
                X = (int)Math.Floor(XPadding + (Scale * rect.X) + XMargin),
                Y = (int)Math.Floor((Scale * rect.Y) + TopMargin),
                Width = (int)Math.Ceiling(Scale * rect.Width),
                Height = (int)Math.Ceiling(Scale * rect.Height),
            };
        }
    }
}
