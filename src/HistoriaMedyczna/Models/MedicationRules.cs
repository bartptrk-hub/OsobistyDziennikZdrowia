using System.Globalization;

namespace HistoriaMedyczna.Models;

public sealed record MedicationMove(Guid Source, Guid Target);

public static class MedicationRules
{
    public static readonly (string Key, string Label)[] Colors =
    [("sage", "Zielony"), ("blue", "Niebieski"), ("purple", "Fioletowy"), ("amber", "Bursztynowy"), ("rose", "Różowy"), ("slate", "Szary")];
    public static bool IsClock(string value) => TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    public static IEnumerable<Medication> Ordered(IEnumerable<Medication> meds) => meds.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name).ThenBy(m => m.Id);
    public static IEnumerable<string> Slots(IEnumerable<Medication> meds) => MedicalRules.Times.Where(t => meds.Any(m => m.TimesOfDay.Contains(t)))
        .Concat(meds.SelectMany(m => m.TimesOfDay).Where(IsClock).Distinct().OrderBy(t => t, StringComparer.Ordinal));
    public static IReadOnlyList<Guid> MoveBefore(IEnumerable<Medication> meds, Guid source, Guid target)
    {
        var ids = Ordered(meds).Select(m => m.Id).ToList();
        if (source == target || !ids.Contains(source) || !ids.Contains(target)) return ids;
        ids.Remove(source); ids.Insert(ids.IndexOf(target), source);
        return ids;
    }
}
