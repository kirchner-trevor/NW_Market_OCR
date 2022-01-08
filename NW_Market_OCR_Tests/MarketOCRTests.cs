using Microsoft.VisualStudio.TestTools.UnitTesting;
using NW_Market_Model;
using NW_Market_OCR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NW_Market_OCR_Tests
{
    [TestClass]
    public class MarketOCRTests
    {
        [TestMethod]
        public async Task UpdateDatabaseWithMarketListings_Clean_753x487_Image()
        {
            MarketDatabase marketDatabase = new MarketDatabase(Directory.GetCurrentDirectory());
            DateTime captureTime = DateTime.UtcNow;
            await MarketOCR.Initialize(null);
            MarketOCR.UpdateDatabaseWithMarketListings(marketDatabase, Path.Combine(Directory.GetCurrentDirectory(), "TestImages", "753x487.png"), captureTime);

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

            AssertListings(marketDatabase, expectedListings);
        }

        [TestMethod]
        public async Task UpdateDatabaseWithMarketListings_Clean_1130x730_Image()
        {
            MarketDatabase marketDatabase = new MarketDatabase(Directory.GetCurrentDirectory());
            DateTime captureTime = DateTime.UtcNow;
            await MarketOCR.Initialize(null);
            MarketOCR.UpdateDatabaseWithMarketListings(marketDatabase, Path.Combine(Directory.GetCurrentDirectory(), "TestImages", "1130x730.png"), captureTime);

            MarketListing[] expectedListings = new[]
            {
                new MarketListing
                {
                    Name = "Shockbulb Stem",
                    Price = 0.07f,
                    Location = "Everfall",
                    Instances = new List<MarketListingInstance>
                    {
                        new MarketListingInstance
                        {
                            Available = 236,
                            TimeRemaining = TimeSpan.FromHours(10),
                            Time = captureTime,
                        },
                    },
                },
                new MarketListing
                {
                    Name = "Mushroom",
                    Price = 0.07f,
                    Location = "Windsward",
                    Instances = new List<MarketListingInstance>
                    {
                        new MarketListingInstance
                        {
                            Available = 162,
                            TimeRemaining = TimeSpan.FromHours(15),
                            Time = captureTime,
                        },
                    },
                },
                new MarketListing
                {
                    Name = "Orichalcum Ore",
                    Price = 0.07f,
                    Location = "Everfall",
                    Instances = new List<MarketListingInstance>
                    {
                        new MarketListingInstance
                        {
                            Available = 285,
                            TimeRemaining = TimeSpan.FromHours(16),
                            Time = captureTime,
                        },
                    },
                },
                new MarketListing
                {
                    Name = "Orichalcum Ore",
                    Price = 0.07f,
                    Location = "Everfall",
                    Instances = new List<MarketListingInstance>
                    {
                        new MarketListingInstance
                        {
                            Available = 1438,
                            TimeRemaining = TimeSpan.FromHours(22),
                            Time = captureTime,
                        },
                    },
                },
                new MarketListing
                {
                    Name = "Dragonglory Stem",
                    Price = 0.07f,
                    Location = "Monarch's Bluffs",
                    Instances = new List<MarketListingInstance>
                    {
                        new MarketListingInstance
                        {
                            Available = 348,
                            TimeRemaining = TimeSpan.FromDays(1),
                            Time = captureTime,
                        },
                    },
                },
                new MarketListing
                {
                    Name = "Hearty Meal",
                    Price = 0.07f,
                    Location = "Mountainrise",
                    Instances = new List<MarketListingInstance>
                    {
                        new MarketListingInstance
                        {
                            Available = 100,
                            TimeRemaining = TimeSpan.FromDays(3),
                            Time = captureTime,
                        },
                    },
                },
                new MarketListing
                {
                    Name = "Orichalcum Ore",
                    Price = 0.07f,
                    Location = "Windsward",
                    Instances = new List<MarketListingInstance>
                    {
                        new MarketListingInstance
                        {
                            Available = 307,
                            TimeRemaining = TimeSpan.FromDays(3),
                            Time = captureTime,
                        },
                    },
                },
                new MarketListing
                {
                    Name = "Platinum Ore",
                    Price = 0.07f,
                    Location = "Windsward",
                    Instances = new List<MarketListingInstance>
                    {
                        new MarketListingInstance
                        {
                            Available = 215,
                            TimeRemaining = TimeSpan.FromDays(3),
                            Time = captureTime,
                        },
                    },
                },
                new MarketListing
                {
                    Name = "Small Quartz Crystal",
                    Price = 0.07f,
                    Location = "Windsward",
                    Instances = new List<MarketListingInstance>
                    {
                        new MarketListingInstance
                        {
                            Available = 75,
                            TimeRemaining = TimeSpan.FromDays(4),
                            Time = captureTime,
                        },
                    },
                },
            };

            AssertListings(marketDatabase, expectedListings);
        }

        private static void AssertListings(MarketDatabase marketDatabase, MarketListing[] expectedListings)
        {
            Assert.AreEqual(expectedListings.Length, marketDatabase.Listings.Count);

            for (int i = 0; i < expectedListings.Length; i++)
            {
                MarketListing expected = expectedListings[i];
                MarketListing actual = marketDatabase.Listings[i];

                AssertMarketListingsAreEqual(expected, actual);
            }
        }

        private static void AssertMarketListingsAreEqual(MarketListing expected, MarketListing actual)
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
