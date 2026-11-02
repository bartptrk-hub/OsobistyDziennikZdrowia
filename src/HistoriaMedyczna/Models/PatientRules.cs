namespace HistoriaMedyczna.Models;

public static class PatientRules
{
    public static Guid? SelectInitial(IEnumerable<Patient> patients, Guid? current)
    {
        var items = patients.ToArray();
        if (items.Any(p => p.Id == current)) return current;
        return (items.FirstOrDefault(p => p.IsDefault) ?? items.FirstOrDefault())?.Id;
    }
}
