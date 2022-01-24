using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using Tesseract;

namespace NW_Image_Analysis
{
    public class TesseractOcrEngine : OcrEngine
    {
        private TesseractEngine tesseractEngineForDecimals;
        private TesseractEngine tesseractOcrForLetters;
        private TesseractEngine tesseractEngineForFastAnything;

        public TesseractOcrEngine()
        {
            tesseractEngineForDecimals = new TesseractEngine(Path.Combine(Directory.GetCurrentDirectory(), "tessdata/normal"), "eng", EngineMode.Default);
            tesseractEngineForDecimals.SetVariable("tessedit_char_whitelist", "0123456789");
            tesseractEngineForDecimals.SetVariable("classify_bln_numeric_mode", "1");
            tesseractEngineForDecimals.DefaultPageSegMode = PageSegMode.SingleBlock;

            tesseractOcrForLetters = new TesseractEngine(Path.Combine(Directory.GetCurrentDirectory(), "tessdata/best"), "eng", EngineMode.Default);
            tesseractOcrForLetters.SetVariable("tessedit_char_blacklist", "`~!@#$%^&*()-_=+[{]}\\|;:\"<.>/?");

            tesseractEngineForFastAnything = new TesseractEngine(Path.Combine(Directory.GetCurrentDirectory(), "tessdata/normal"), "eng", EngineMode.Default);
            tesseractEngineForFastAnything.SetVariable("tessedit_char_blacklist", "`~!@#$%^&*()-_=+[{]}\\|;:\"<.>/?");
        }

        public List<OcrTextArea> ExtractTextAreas(string path)
        {
            List<OcrTextArea> textAreas = new List<OcrTextArea>();
            textAreas.AddRange(RunTesseractOcr(path, OcrKind.NUMBERS, tesseractEngineForDecimals));
            textAreas.AddRange(RunTesseractOcr(path, OcrKind.LETTERS, tesseractOcrForLetters));
            return textAreas;
        }

        public string ExtractText(Image<Rgba32> bitmap, Rectangle area)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bitmap.Save(memoryStream, PngFormat.Instance);
                using (Pix image = Pix.LoadFromMemory(memoryStream.ToArray()))
                {
                    return ExtractText(image, area);
                }
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

            using (Page page = tesseractEngineForFastAnything.Process(image, ocrArea))
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
                                    Width = bounds.Width,
                                    Height = bounds.Height,
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
}
