namespace HistoriaMedyczna.Models;

public static class HistoryRules
{
    public sealed record SymptomRow(Symptom Symptom, int Depth);

    // Keep each episode separate, even when a related episode has the same name.
    // Missing (filtered-out) parents become roots; visited also guards corrupt cycles.
    public static IReadOnlyList<SymptomRow> OrderSymptoms(IEnumerable<Symptom> symptoms, bool newestRootsFirst = false)
    {
        var items = symptoms.DistinctBy(s => s.Id).ToArray();
        var ids = items.Select(s => s.Id).ToHashSet();
        var children = items.Where(s => s.OriginId is not null).ToLookup(s => s.OriginId!.Value);
        var roots = items.Where(s => s.OriginId is null || !ids.Contains(s.OriginId.Value));
        var orderedRoots = newestRootsFirst ? roots.OrderByDescending(s => s.StartDate) : roots.OrderBy(s => s.StartDate);
        var result = new List<SymptomRow>();
        var visited = new HashSet<Guid>();
        foreach (var root in orderedRoots.ThenBy(s => s.Name).ThenBy(s => s.Id).Concat(items.OrderBy(s => s.StartDate).ThenBy(s => s.Id)))
        {
            var stack = new Stack<SymptomRow>();
            stack.Push(new(root, 0));
            while (stack.TryPop(out var row))
            {
                if (!visited.Add(row.Symptom.Id)) continue;
                result.Add(row);
                foreach (var child in children[row.Symptom.Id].OrderByDescending(s => s.StartDate).ThenByDescending(s => s.Name).ThenByDescending(s => s.Id))
                    stack.Push(new(child, row.Depth + 1));
            }
        }
        return result;
    }

    public const int MaxDays = 366;
    public static DateOnly SymptomEnd(Symptom symptom, DateOnly today) => symptom.EndDate ?? today;
    public static bool SymptomVisibleOn(Symptom symptom, DateOnly day, DateOnly today) => MedicalRules.Active(symptom.StartDate, SymptomEnd(symptom, today), day);
    public static bool Overlaps(DateOnly start, DateOnly? end, DateOnly from, DateOnly to) => from <= to && start <= to && (end is null || end >= from);
    public static IReadOnlyList<DateOnly> Days(DateOnly from, DateOnly to)
    {
        var count = to.DayNumber - from.DayNumber + 1;
        return count is < 1 or > MaxDays ? [] : Enumerable.Range(0, count).Select(from.AddDays).ToArray();
    }
    public static IEnumerable<Symptom> Children(Symptom source, IEnumerable<Symptom> all) => all.Where(s => s.OriginId == source.Id).OrderBy(s => s.StartDate).ThenBy(s => s.Name);
    public static string Status(Symptom symptom, IEnumerable<Symptom> all)
    {
        var children = Children(symptom, all).Select(s => s.Name).ToArray();
        return children.Length > 0 ? $"Przeszło w: {string.Join(", ", children)}" : symptom.EndDate is null ? "Trwa" : "Zakończony";
    }
}
