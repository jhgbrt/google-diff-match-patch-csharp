using Microsoft.VisualStudio.TestTools.UnitTesting;
using Original;

namespace DiffMatchPatch.Original.Tests
{
    [TestClass]
    public class OriginalTests
    {
        [TestMethod]
        public void AllOriginalTestsPass()
        {
            diff_match_patchTest.Main(null);
        }
    }
}