using System;

namespace DiffMatchPatch
{
    public class Diff
    {
        public static Diff Create(Operation operation, string text)
        {
            return new Diff(operation, text);
        }

        public static Diff EQUAL(string text)
        {
            return Create(Operation.EQUAL, text);
        }

        public static Diff INSERT(string text)
        {
            return Create(Operation.INSERT, text);
        }
        public static Diff DELETE(string text)
        {
            return Create(Operation.DELETE, text);
        }

        public readonly Operation operation;
        // One of: INSERT, DELETE or EQUAL.
        public readonly string text;
        // The text associated with this diff operation.

        /**
         * Constructor.  Initializes the diff with the provided values.
         * @param operation One of INSERT, DELETE or EQUAL.
         * @param text The text being applied.
         */
        Diff(Operation operation, string text)
        {
            // Construct a diff with the specified operation and text.
            this.operation = operation;
            this.text = text;
        }

        /**
         * Display a human-readable version of this Diff.
         * @return text version.
         */
        public override string ToString()
        {
            string prettyText = text.Replace('\n', '\u00b6');
            return "Diff(" + operation + ",\"" + prettyText + "\")";
        }

        /**
         * Is this Diff equivalent to another Diff?
         * @param d Another Diff to compare against.
         * @return true or false.
         */
        public override bool Equals(Object obj)
        {
            Diff p = obj as Diff;
            if (p == null)
            {
                return false;
            }
            return p.operation == operation && p.text == text;
        }

        public bool Equals(Diff obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // Return true if the fields match.
            return obj.operation == operation && obj.text == text;
        }

        public override int GetHashCode()
        {
            return text.GetHashCode() ^ operation.GetHashCode();
        }

        public Diff Replace(string toString)
        {
            return Create(operation, toString);
        }

        public Diff Copy()
        {
            return Create(operation, text);
        }
    }
}