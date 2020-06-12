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

using Xunit;

namespace DiffMatchPatch.Tests
{
    
    public class TextUtil_MatchPatternTests
    {
        [Fact]
        public void EqualStrings_FullMatch()
        {
            Assert.Equal(0, "abcdef".FindBestMatchIndex("abcdef", 1000));
        }

        [Fact]
        public void EmptyString_NoMatch()
        {
            Assert.Equal(-1, "".FindBestMatchIndex("abcdef", 1));
        }

        [Fact]
        public void EmptyPattern()
        {
            Assert.Equal(3, "abcdef".FindBestMatchIndex("", 3));
        }

        [Fact]
        public void ExactMatch()
        {
            Assert.Equal(3, "abcdef".FindBestMatchIndex("de", 3));
        }
        [Fact]
        public void MatchBeyondEnd()
        {
            Assert.Equal(3, "abcdef".FindBestMatchIndex("defy", 4));
        }
        [Fact]
        public void OversizedPattern()
        {
            Assert.Equal(0, "abcdef".FindBestMatchIndex("abcdefy", 0));
        }

        [Fact]
        public void ComplexMatch()
        {
            var input = "I am the very model of a modern major general.";
            var match = input.FindBestMatchIndex(" that berry ", 5, new MatchSettings(0.7f, 1000));
            Assert.Equal(4, match);
        }
    }
}
