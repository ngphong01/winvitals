namespace WinVitals.Core.Analytics;

public static class StreakCalculator
{
    public static int Compute(IEnumerable<DateTime> sessionCompletedTimesLocal, DateTime nowLocal)
    {
        var days = sessionCompletedTimesLocal
            .Select(d => d.Date).Distinct()
            .OrderByDescending(d => d).ToList();
        if (days.Count == 0) return 0;

        var today = nowLocal.Date;
        if (days[0] < today.AddDays(-1)) return 0;

        int streak = 1;
        for (int i = 1; i < days.Count; i++)
        {
            if ((days[i - 1] - days[i]).TotalDays == 1) streak++;
            else break;
        }
        return streak;
    }
}
