using Microsoft.VisualStudio.TestTools.UnitTesting;
using NW_Market_Model;
using NW_Stream_Collector;

namespace NW_Stream_Collector_Tests
{
    [TestClass]
    public class TwitchStreamMetadataTests
    {
        [TestMethod]
        public void TryExtractServer_OneWord_MatchesServerInMiddle()
        {
            TwitchStreamMetadata systemUnderTest = new TwitchStreamMetadata(new ConfigurationDatabase(Program.DATA_DIRECTORY));

            bool didExtractServer = systemUnderTest.TryExtractServer("this is my orofena stream my dudes", out string server);

            Assert.IsTrue(didExtractServer);
            Assert.AreEqual("orofena", server);
        }


        [TestMethod]
        public void TryExtractServer_OneWord_MatchesServerAtStart()
        {
            TwitchStreamMetadata systemUnderTest = new TwitchStreamMetadata(new ConfigurationDatabase(Program.DATA_DIRECTORY));

            bool didExtractServer = systemUnderTest.TryExtractServer("orofena stream my dudes", out string server);

            Assert.IsTrue(didExtractServer);
            Assert.AreEqual("orofena", server);
        }

        [TestMethod]
        public void TryExtractServer_OneWord_MatchesServerAtEnd()
        {
            TwitchStreamMetadata systemUnderTest = new TwitchStreamMetadata(new ConfigurationDatabase(Program.DATA_DIRECTORY));

            bool didExtractServer = systemUnderTest.TryExtractServer("stream my dudes on orofena", out string server);

            Assert.IsTrue(didExtractServer);
            Assert.AreEqual("orofena", server);
        }

        [TestMethod]
        public void TryExtractServer_WithSpace_MatchesServerInMiddle()
        {
            TwitchStreamMetadata systemUnderTest = new TwitchStreamMetadata(new ConfigurationDatabase(Program.DATA_DIRECTORY));

            bool didExtractServer = systemUnderTest.TryExtractServer("this is my el dorado stream my dudes", out string server);

            Assert.IsTrue(didExtractServer);
            Assert.AreEqual("el-dorado", server);
        }

        [TestMethod]
        public void TryExtractServer_WithSpace_MatchesServerAtStart()
        {
            TwitchStreamMetadata systemUnderTest = new TwitchStreamMetadata(new ConfigurationDatabase(Program.DATA_DIRECTORY));

            bool didExtractServer = systemUnderTest.TryExtractServer("el dorado stream my dudes", out string server);

            Assert.IsTrue(didExtractServer);
            Assert.AreEqual("el-dorado", server);
        }

        [TestMethod]
        public void TryExtractServer_WithSpace_MatchesServerAtEnd()
        {
            TwitchStreamMetadata systemUnderTest = new TwitchStreamMetadata(new ConfigurationDatabase(Program.DATA_DIRECTORY));

            bool didExtractServer = systemUnderTest.TryExtractServer("stream my dudes on el dorado", out string server);

            Assert.IsTrue(didExtractServer);
            Assert.AreEqual("el-dorado", server);
        }

        [TestMethod]
        public void TryExtractServer_WithSpace_MatchesNothing()
        {
            TwitchStreamMetadata systemUnderTest = new TwitchStreamMetadata(new ConfigurationDatabase(Program.DATA_DIRECTORY));

            bool didExtractServer = systemUnderTest.TryExtractServer("stream my dudes", out string server);

            Assert.IsFalse(didExtractServer);
            Assert.AreEqual(null, server);
        }
    }
}
