namespace HistoriaMedyczna.Models;

public static class MedicationPlanRules
{
    public sealed record Row(DateOnly Date, string Slot, string Medication, decimal Dose, string Unit);
    public static IReadOnlyList<Row> Build(IEnumerable<Medication> medications, DateOnly from, DateOnly to, IReadOnlySet<string> slots)
    {
        if (to < from || to.DayNumber - from.DayNumber >= HistoryRules.MaxDays)
            throw new ArgumentException($"Wybierz zakres od 1 do {HistoryRules.MaxDays} dni.");
        var meds = MedicationRules.Ordered(medications).ToArray();
        var result = new List<Row>();
        foreach (var day in HistoryRules.Days(from, to))
        {
            var active = meds.Where(m => MedicalRules.Active(m.StartDate, m.EndDate, day)).ToArray();
            foreach (var slot in MedicationRules.Slots(active).Where(slots.Contains))
            foreach (var med in active.Where(m => m.TimesOfDay.Contains(slot)))
                result.Add(new(day, slot, med.Name, med.Dose, med.Unit));
        }
        return result;
    }
}
