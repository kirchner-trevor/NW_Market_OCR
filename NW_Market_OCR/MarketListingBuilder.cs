using NW_Image_Analysis;
using NW_Market_Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace NW_Market_OCR
{
    public class MarketListingBuilder
    {
        private readonly MarketColumnMappings ColumnMappings = new MarketColumnMappings();

        public List<MarketListing> BuildFromMany(List<OcrTextArea> ocrTextAreas, int areaWidth, int areaHeight)
        {
            if (ocrTextAreas == null)
            {
                return new List<MarketListing>();
            }

            ColumnMappings.SetSize(new Point(areaWidth, areaHeight));

            Dictionary<int, List<OcrTextArea>> ocrTextAreaLines = GroupTextAreasIntoLines(ocrTextAreas);

            return ocrTextAreaLines.OrderBy(_ => _.Key).Select(_ => BuildFrom(_.Value)).ToList();
        }

        private MarketListing BuildFrom(List<OcrTextArea> ocrTextAreas)
        {
            MarketListing marketListing = new MarketListing();

            foreach (OcrTextArea word in ocrTextAreas.Where(_ => _.Kind == OcrKind.NUMBERS))
            {
                if (ColumnMappings.PRICE_TEXT_X_RANGE.Contains(word.X))
                {
                    marketListing.OriginalPrice = word.Text;
                    if (float.TryParse(word.Text, out float price))
                    {
                        marketListing.Price = price / 100f;
                    }
                }
                else if (ColumnMappings.AVAILABLE_TEXT_X_RANGE.Contains(word.X))
                {
                    marketListing.Latest.OriginalAvailable = word.Text;

                    string cleanedWord = word.Text;

                    // Ocr combined available and owned together so split them
                    if(word.X + word.Width > ColumnMappings.AVAILABLE_TEXT_X_RANGE.End)
                    {
                        float widthRatio = ColumnMappings.AVAILABLE_TEXT_X_RANGE.Size * 1f / word.Width;
                        int numCharactersInsideRange = (int)Math.Ceiling(widthRatio * word.Text.Length);
                        cleanedWord = word.Text.Substring(0, numCharactersInsideRange);
                    }

                    if (int.TryParse(cleanedWord, out int available))
                    {
                        marketListing.Latest.Available = available;
                    }
                }
            }

            foreach (OcrTextArea word in ocrTextAreas.Where(_ => _.Kind == OcrKind.LETTERS))
            {
                if (ColumnMappings.NAME_TEXT_X_RANGE.Contains(word.X))
                {
                    // Sometimes the name gets split into pieces
                    marketListing.OriginalName = marketListing.OriginalName == null ? word.Text : marketListing.OriginalName + " " + word.Text;
                    marketListing.Name = marketListing.OriginalName;
                }     
                else if (ColumnMappings.TIME_REMAINING_TEXT_X_RANGE.Contains(word.X))
                {
                    marketListing.Latest.OriginalTimeRemaining = word.Text;
                    string cleanedWord = word.Text.Replace(" ", "");

                    // Manual corrections
                    if (cleanedWord.EndsWith("4") || cleanedWord.EndsWith("n"))
                    {
                        cleanedWord = cleanedWord.Substring(0, cleanedWord.Length - 1) + "h";
                    }
                    if (cleanedWord.Length == 2 && (cleanedWord.StartsWith("a") || cleanedWord.StartsWith("g")))
                    {
                        cleanedWord = "6" + cleanedWord.Substring(1, cleanedWord.Length - 1);
                    }
                    if (cleanedWord.Length == 2 && cleanedWord.StartsWith("S"))
                    {
                        cleanedWord = "9" + cleanedWord.Substring(1, cleanedWord.Length - 1);
                    }

                    if (cleanedWord.EndsWith("h"))
                    {
                        string amountText = cleanedWord.Replace("h", "");
                        if (int.TryParse(amountText, out int timeAmount))
                        {
                            marketListing.Latest.TimeRemaining = TimeSpan.FromHours(timeAmount);
                        }
                    }
                    else if (cleanedWord.EndsWith("d"))
                    {
                        string amountText = cleanedWord.Replace("d", "");
                        if (int.TryParse(amountText, out int timeAmount))
                        {
                            marketListing.Latest.TimeRemaining = TimeSpan.FromDays(timeAmount);
                        }
                    }
                }
                else if (ColumnMappings.LOCATION_TEXT_X_RANGE.Contains(word.X))
                {
                    marketListing.OriginalLocation = marketListing.OriginalLocation == null ? word.Text : marketListing.OriginalLocation + word.Text;
                    marketListing.Location = marketListing.OriginalLocation;
                }
            }

            return marketListing;
        }

        private Dictionary<int, List<OcrTextArea>> GroupTextAreasIntoLines(List<OcrTextArea> ocrTextAreas)
        {
            Dictionary<int?, List<OcrTextArea>> wordBucketsByHeight = new();

            foreach (OcrTextArea word in ocrTextAreas)
            {
                try
                {
                    // Find the group within N of the words Y value
                    int? yGroupKey = wordBucketsByHeight.Keys.FirstOrDefault(yGroup => Math.Abs(yGroup.Value - word.Y) < ColumnMappings.WORD_BUCKET_Y_GROUPING_THRESHOLD);
                    if (yGroupKey.HasValue && wordBucketsByHeight.ContainsKey(yGroupKey))
                    {
                        wordBucketsByHeight[yGroupKey].Add(word);
                    }
                    else
                    {
                        wordBucketsByHeight.Add(word.Y, new List<OcrTextArea> { word });
                    }
                }
                catch (Exception e)
                {
                    Trace.TraceError($"Failed to add word with Y {word.Y} to buckets. Existing keys: {wordBucketsByHeight.Keys}\n{e.Message}");
                }
            }

            return wordBucketsByHeight.Where(_ => _.Key.HasValue).ToDictionary(_ => _.Key.Value, _ => _.Value);
        }

        private class MarketColumnMappings
        {
            // Pixels
            private static Point DEFAULT_SIZE = new Point(1130, 730);

            // Pixel Coordinates (1px to either side)
            private Range DEFAULT_NAME_TEXT_X_RANGE = new Range(0, 272); // Originally 4, 272
            private Range DEFAULT_PRICE_TEXT_X_RANGE = new Range(314, 385);
            private Range DEFAULT_TEIR_TEXT_X_RANGE = new Range(426, 443);
            private Range DEFAULT_GEARSCORE_TEXT_X_RANGE = new Range(485, 512);
            private Range DEFAULT_QUALITY_TEXT_X_RANGE = new Range(732, 804);
            private Range DEFAULT_AVAILABLE_TEXT_X_RANGE = new Range(823, 867);
            private Range DEFAULT_TIME_REMAINING_TEXT_X_RANGE = new Range(954, 985);
            private Range DEFAULT_LOCATION_TEXT_X_RANGE = new Range(1019, 1130); // Originally 1019, 1114

            private int DEFAULT_WORD_BUCKET_Y_GROUPING_THRESHOLD = 15;

            public Range NAME_TEXT_X_RANGE { get; private set; }
            public Range PRICE_TEXT_X_RANGE { get; private set; }
            public Range TEIR_TEXT_X_RANGE { get; private set; }
            public Range GEARSCORE_TEXT_X_RANGE { get; private set; }
            public Range QUALITY_TEXT_X_RANGE { get; private set; }
            public Range AVAILABLE_TEXT_X_RANGE { get; private set; }
            public Range TIME_REMAINING_TEXT_X_RANGE { get; private set; }
            public Range LOCATION_TEXT_X_RANGE { get; private set; }

            public int WORD_BUCKET_Y_GROUPING_THRESHOLD { get; private set; }

            public MarketColumnMappings()
            {
                SetSize(DEFAULT_SIZE);
            }

            public void SetSize(Point size)
            {
                float xRatio = (1f * size.X) / DEFAULT_SIZE.X;
                float yRatio = (1f * size.Y) / DEFAULT_SIZE.Y;

                WORD_BUCKET_Y_GROUPING_THRESHOLD = (int)Math.Ceiling(DEFAULT_WORD_BUCKET_Y_GROUPING_THRESHOLD * yRatio);
                NAME_TEXT_X_RANGE = DEFAULT_NAME_TEXT_X_RANGE * xRatio;
                TEIR_TEXT_X_RANGE = DEFAULT_TEIR_TEXT_X_RANGE * xRatio;
                GEARSCORE_TEXT_X_RANGE = DEFAULT_GEARSCORE_TEXT_X_RANGE * xRatio;
                QUALITY_TEXT_X_RANGE = DEFAULT_QUALITY_TEXT_X_RANGE * xRatio;
                PRICE_TEXT_X_RANGE = DEFAULT_PRICE_TEXT_X_RANGE * xRatio;
                AVAILABLE_TEXT_X_RANGE = DEFAULT_AVAILABLE_TEXT_X_RANGE * xRatio;
                TIME_REMAINING_TEXT_X_RANGE = DEFAULT_TIME_REMAINING_TEXT_X_RANGE * xRatio;
                LOCATION_TEXT_X_RANGE = DEFAULT_LOCATION_TEXT_X_RANGE * xRatio;
            }
        }

        private class Range
        {
            public Range(int start, int end)
            {
                Start = start;
                End = end;
            }

            public int Start;
            public int End;
            public int Size
            {
                get
                {
                    return End - Start;
                }
            }

            public bool Contains(int value)
            {
                return Start <= value && value < End;
            }

            public static Range operator *(Range range, float scale)
            {
                return new Range((int)Math.Floor(range.Start * scale), (int)Math.Ceiling(range.End * scale));
            }
        }
    }
}
