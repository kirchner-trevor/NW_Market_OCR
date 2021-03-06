using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;

namespace NW_Image_Analysis
{
    public interface OcrEngine : IDisposable
    {
        List<OcrTextArea> ExtractTextAreas(string path);
        string ExtractText(Image<Rgba32> bitmap, Rectangle area);
        string ExtractText(string path);
    }

    public static class OcrKind
    {
        public const string NUMBERS = "numbers";
        public const string LETTERS = "letters";
    }

    public class OcrTextArea
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public string Text;
        public string Kind;
    }
}
