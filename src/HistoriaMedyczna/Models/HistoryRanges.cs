namespace HistoriaMedyczna.Models;

public static class HistoryRanges
{
    public static readonly (string Key, string Label)[] Options =
    [("last7", "Ostatnie 7 dni"), ("last14", "Ostatnie 14 dni"), ("previousWeek", "Poprzedni tydzień"),
     ("thisWeek", "Bieżący tydzień"), ("previousMonth", "Poprzedni miesiąc"), ("thisMonth", "Obecny miesiąc"),
     ("next7", "Kolejne 7 dni"), ("next14", "Kolejne 14 dni"), ("custom", "Własne daty")];
    public static (DateOnly From, DateOnly To) Get(string key, DateOnly today)
    {
        var monday = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
        var month = new DateOnly(today.Year, today.Month, 1);
        return key switch {
            "last7" => (today.AddDays(-6), today), "last14" => (today.AddDays(-13), today),
            "previousWeek" => (monday.AddDays(-7), monday.AddDays(-1)), "thisWeek" => (monday, monday.AddDays(6)),
            "previousMonth" => (month.AddMonths(-1), month.AddDays(-1)), "thisMonth" => (month, month.AddMonths(1).AddDays(-1)),
            "next7" => (today, today.AddDays(6)), "next14" => (today, today.AddDays(13)),
            _ => throw new ArgumentException("Nieznany zakres dat.", nameof(key))
        };
    }
}
