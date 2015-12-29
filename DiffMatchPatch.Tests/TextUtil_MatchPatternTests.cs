/*
 * Copyright 2008 Google Inc. All Rights Reserved.
 * Author: fraser@google.com (Neil Fraser)
 * Author: anteru@developer.shelter13.net (Matthaeus G. Chajdas)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * Diff Match and Patch -- Test Harness
 * http://code.google.com/p/google-diff-match-patch/
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffMatchPatch.Tests
{
    [TestClass]
    public class TextUtil_MatchPatternTests
    {
        [TestMethod]
        public void EqualStrings_FullMatch()
        {
            Assert.AreEqual(0, "abcdef".FindBestMatchIndex("abcdef", 1000), "match_main: Equality.");
        }

        [TestMethod]
        public void EmptyString_NoMatch()
        {
            Assert.AreEqual(-1, "".FindBestMatchIndex("abcdef", 1), "match_main: Null text.");
        }

        [TestMethod]
        public void EmptyPattern()
        {
            Assert.AreEqual(3, "abcdef".FindBestMatchIndex("", 3), "match_main: Null pattern.");
        }

        [TestMethod]
        public void ExactMatch()
        {
            Assert.AreEqual(3, "abcdef".FindBestMatchIndex("de", 3), "match_main: Exact match.");
        }
        [TestMethod]
        public void MatchBeyondEnd()
        {
            Assert.AreEqual(3, "abcdef".FindBestMatchIndex("defy", 4), "match_main: Beyond end match.");
        }
        [TestMethod]
        public void OversizedPattern()
        {
            Assert.AreEqual(0, "abcdef".FindBestMatchIndex("abcdefy", 0), "match_main: Oversized pattern.");
        }

        [TestMethod]
        public void ComplexMatch()
        {
            var input = "I am the very model of a modern major general.";
            var match = input.FindBestMatchIndex(" that berry ", 5, new MatchSettings(0.7f, 1000));
            Assert.AreEqual(4, match, "match_main: Complex match.");
        }
    }
}
