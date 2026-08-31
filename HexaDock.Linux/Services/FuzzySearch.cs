namespace HexaDock.Linux.Services;

public static class FuzzySearch
{
    public static int Score(string query, string value)
    {
        query = query.Trim();
        if (query.Length == 0) return 1;
        var direct = value.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (direct >= 0) return 1000 - direct * 4 - Math.Max(0, value.Length - query.Length);
        var score = 0;
        var position = 0;
        var streak = 0;
        foreach (var wanted in query)
        {
            var found = value.IndexOf(wanted.ToString(), position, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return 0;
            streak = found == position ? streak + 1 : 0;
            score += 20 + streak * 8 - Math.Min(12, found - position);
            position = found + 1;
        }
        return score;
    }
}
