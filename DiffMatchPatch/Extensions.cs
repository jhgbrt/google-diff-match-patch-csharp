namespace DiffMatchPatch;

internal static class Extensions
{
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
