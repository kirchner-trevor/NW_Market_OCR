using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Tesseract;

namespace NW_Image_Analysis
{
    public interface OcrEngine : IDisposable
    {
        List<OcrTextArea> ExtractTextAreas(string path);
        string ExtractText(Bitmap bitmap, Rectangle area);
        string ExtractText(string path);
    }

    public static class OcrKind
    {
        public const string OCR_KIND_DECIMALS = "decimals";
        public const string OCR_KIND_LETTERS = "letters";
    }

    public class TesseractOcrEngine : OcrEngine
    {
        private TesseractEngine tesseractEngineForDecimals = new TesseractEngine(Path.Combine(Directory.GetCurrentDirectory(), "tessdata/normal"), "eng", EngineMode.TesseractOnly);
        private TesseractEngine tesseractOcrForLetters = new TesseractEngine(Path.Combine(Directory.GetCurrentDirectory(), "tessdata/best"), "eng", EngineMode.Default);

        public List<OcrTextArea> ExtractTextAreas(string path)
        {
            tesseractEngineForDecimals.SetVariable("whitelist", ".,0123456789");
            List<OcrTextArea> textAreas = new List<OcrTextArea>();
            textAreas.AddRange(RunTesseractOcr(path, OcrKind.OCR_KIND_DECIMALS, tesseractEngineForDecimals));
            textAreas.AddRange(RunTesseractOcr(path, OcrKind.OCR_KIND_LETTERS, tesseractOcrForLetters));
            return textAreas;
        }

        public string ExtractText(Bitmap bitmap, Rectangle area)
        {
            using (Pix image = PixConverter.ToPix(bitmap))
            {
                return ExtractText(image, area);
            }
        }

        public string ExtractText(string path)
        {
            using (Pix image = Pix.LoadFromFile(path))
            {
                return ExtractText(image, new Rectangle(0, 0, image.Width, image.Height));
            }
        }

        public string ExtractText(Pix image, Rectangle area)
        {
            Rect ocrArea = new Rect(area.X, area.Y, area.Width, area.Height);

            using (Page page = tesseractEngineForDecimals.Process(image, ocrArea))
            {
                return page.GetText();
            }
        }

        private List<OcrTextArea> RunTesseractOcr(string processedPath, string source, TesseractEngine tesseractEngine)
        {
            List<OcrTextArea> textAreas = new();

            using (Pix image = Pix.LoadFromFile(processedPath))
            {
                using (Page page = tesseractEngine.Process(image))
                {
                    using (ResultIterator resultIterator = page.GetIterator())
                    {
                        do
                        {
                            if (resultIterator.TryGetBoundingBox(PageIteratorLevel.Word, out Rect bounds))
                            {
                                textAreas.Add(new OcrTextArea
                                {
                                    Text = resultIterator.GetText(PageIteratorLevel.Word),
                                    X = bounds.X1,
                                    Y = bounds.Y1,
                                    Kind = source,
                                });
                            }
                        } while (resultIterator.Next(PageIteratorLevel.Word));
                    }
                }
            }

            return textAreas;
        }

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    tesseractEngineForDecimals.Dispose();
                    tesseractOcrForLetters.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~TesseractOcrEngine()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class OcrTextArea
    {
        public int X;
        public int Y;
        public string Text;
        public string Kind;
    }
}
