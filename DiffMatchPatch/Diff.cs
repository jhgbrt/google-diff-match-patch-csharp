using System;
using System.Collections.Generic;
using System.Threading;

namespace DiffMatchPatch
{
    public struct Diff
    {
        internal static Diff Create(Operation operation, string text) => new Diff(operation, text);
        internal static Diff Equal(string text) => Create(Operation.Equal, text);
        internal static Diff Insert(string text) => Create(Operation.Insert, text);
        internal static Diff Delete(string text) => Create(Operation.Delete, text);

        public readonly Operation Operation;
        // One of: INSERT, DELETE or EQUAL.
        public readonly string Text;
        // The text associated with this diff operation.

        Diff(Operation operation, string text)
        {
            // Construct a diff with the specified operation and text.
            Operation = operation;
            Text = text;
        }

        /// <summary>
        /// Generate a human-readable version of this Diff.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var prettyText = Text.Replace('\n', '\u00b6');
            return "Diff(" + Operation + ",\"" + prettyText + "\")";
        }

        /// <summary>
        /// Is this Diff equivalent to another Diff?
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(Object obj) => !ReferenceEquals(obj, null) && Equals((Diff) obj);

        public bool Equals(Diff obj) => obj.Operation == Operation && obj.Text == Text;

        public static bool operator==(Diff left, Diff right) => left.Equals(right);

        public static bool operator !=(Diff left, Diff right) => !(left == right);


        public override int GetHashCode() => Text.GetHashCode() ^ Operation.GetHashCode();

        internal Diff Replace(string toString) => Create(Operation, toString);

        internal Diff Copy() => Create(Operation, Text);

        /// <summary>
        /// Find the differences between two texts.
        /// </summary>
        /// <param name="text1">Old string to be diffed</param>
        /// <param name="text2">New string to be diffed</param>
        /// <param name="timeoutInSeconds">if specified, certain optimizations may be enabled to meet the time constraint, possibly resulting in a less optimal diff</param>
        /// <param name="checklines">If false, then don't run a line-level diff first to identify the changed areas. If true, then run a faster slightly less optimal diff.</param>
        /// <returns></returns>
        public static List<Diff> Compute(string text1, string text2, float timeoutInSeconds = 0f, bool checklines = true)
        {
            using (var cts = timeoutInSeconds <= 0 
                ? new CancellationTokenSource() 
                : new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInSeconds))
                )
            {
                return Compute(text1, text2, checklines, cts.Token, timeoutInSeconds > 0);
            }
        }

        public static List<Diff> Compute(string text1, string text2, bool checkLines, CancellationToken token, bool optimizeForSpeed) 
            => DiffAlgorithm.Compute(text1, text2, checkLines, token, optimizeForSpeed);
    }
}