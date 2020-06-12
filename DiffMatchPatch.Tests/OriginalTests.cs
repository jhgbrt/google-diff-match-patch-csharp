using Xunit;
using Original;

namespace DiffMatchPatch.Original.Tests
{
    
    public class OriginalTests
    {
        [Fact]
        public void AllOriginalTestsPass()
        {
            diff_match_patchTest.OriginalMain(null);
        }
    }
}