namespace DiffMatchPatch;

internal static class Extensions
{
    internal static void Splice<T>(this List<T> input, int start, int count, params T[] objects)
        => input.Splice(start, count, (IEnumerable<T>)objects);

    /// <summary>
    /// replaces [count] entries starting at index [start] with the given [objects]
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="input"></param>
    /// <param name="start"></param>
    /// <param name="count"></param>
    /// <param name="objects"></param>
    internal static void Splice<T>(this List<T> input, int start, int count, IEnumerable<T> objects)
    {
        input.RemoveRange(start, count);
        input.InsertRange(start, objects);
    }

    internal static IEnumerable<T> Concat<T>(this IEnumerable<T> items, T item)
    {
        foreach (var i in items) yield return i;
        yield return item;
    }

    internal static IEnumerable<T> ItemAsEnumerable<T>(this T item)
    {
        yield return item;
    }

    internal static IEnumerable<string> SplitBy(this string s, char separator)
    {
        StringBuilder sb = new();
        foreach (var c in s)
        {
            if (c == separator)
            {
                yield return sb.ToString();
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        if (sb.Length > 0)
            yield return sb.ToString();
    }
}
