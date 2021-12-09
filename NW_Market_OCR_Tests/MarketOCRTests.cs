using Microsoft.VisualStudio.TestTools.UnitTesting;
using NW_Market_Model;
using NW_Market_OCR;
using System;
using System.Collections.Generic;
using System.IO;

namespace NW_Market_OCR_Tests
{
    [TestClass]
    public class MarketOCRTests
    {
        [TestMethod]
        public void UpdateDatabaseWithMarketListings_Clean_753x487_Image()
        {
            MarketDatabase marketDatabase = new MarketDatabase();
            DateTime captureTime = DateTime.UtcNow;
            MarketOCR.UpdateDatabaseWithMarketListings(marketDatabase, Path.Combine(Directory.GetCurrentDirectory(), "TestImages", "753x487.png"), captureTime);

            Assert.AreEqual(9, marketDatabase.Listings.Count);

            MarketListing[] expectedListings = new[]
            {
                new MarketListing
                {
                    Name = "Immortal Leather Hat",
                    Price = 1999.00f,
                    Location = "Everfall",
                    Instances = new List<MarketListingInstance>
                    {
                        new MarketListingInstance
                        {
                            Available = 1,
                            TimeRemaining = TimeSpan.FromHours(2),
                            Time = captureTime,
                        },
                    },
                },
            };

            for (int i = 0; i < expectedListings.Length; i++)
            {
                MarketListing expected = expectedListings[i];
                MarketListing actual = marketDatabase.Listings[i];

                AsserMarketListingsAreEqual(expected, actual);
            }
        }

        private static void AsserMarketListingsAreEqual(MarketListing expected, MarketListing actual)
        {
            Assert.AreEqual(expected.Latest.Time, actual.Latest.Time);
            Assert.AreEqual(expected.Name, actual.Name);
            Assert.AreEqual(expected.Price, actual.Price);
            Assert.AreEqual(expected.Latest.Available, actual.Latest.Available);
            Assert.AreEqual(expected.Latest.TimeRemaining, actual.Latest.TimeRemaining);
            Assert.AreEqual(expected.Location, actual.Location);
        }
    }
}
