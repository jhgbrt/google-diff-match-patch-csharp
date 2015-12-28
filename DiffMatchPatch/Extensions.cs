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
 * Diff Match and Patch
 * http://code.google.com/p/google-diff-match-patch/
 */

using System.Collections.Generic;

namespace DiffMatchPatch
{
    internal static class Extensions
    {
        // JScript splice function
        public static List<T> Splice<T>(this List<T> input, int start, int count, params T[] objects)
        {
            IEnumerable<T> enumerable = objects;
            return input.Splice(start, count, enumerable);
        }

        public static List<T> Splice<T>(this List<T> input, int start, int count, IEnumerable<T> objects)
        {
            var deletedRange = input.GetRange(start, count);
            input.RemoveRange(start, count);
            input.InsertRange(start, objects);
            return deletedRange;
        }

        // Java substring function
        public static string JavaSubstring(this string s, int begin, int end)
        {
            return s.Substring(begin, end - begin);
        }
    }

    /**-
     * The data structure representing a diff is a List of Diff objects:
     * {Diff(Operation.DELETE, "Hello"), Diff(Operation.INSERT, "Goodbye"),
     *  Diff(Operation.EQUAL, " world.")}
     * which means: delete "Hello", add "Goodbye" and keep " world."
     */


    /**
     * Class representing one diff operation.
     */


    /**
     * Class representing one patch operation.
     */


    /**
     * Class containing the diff, match and patch methods.
     * Also Contains the behaviour settings.
     */
}
