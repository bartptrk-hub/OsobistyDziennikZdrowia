using System.ComponentModel.DataAnnotations;

namespace HistoriaMedyczna.Models;

public static class MedicalRules
{
    public static readonly string[] Times = ["rano (na czczo)", "rano (po posiłku)", "przed obiadem", "w trakcie obiadu", "po obiedzie", "do kolacji", "przed snem"];
    public static readonly string[] Units = ["tabletka", "ml", "mg", "g", "pompek", "psiknięć", "psiknięć w dziurkę"];
    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Europe/Warsaw").DateTime);
    public static bool Active(DateOnly start, DateOnly? end, DateOnly day) => start <= day && (end is null || day <= end);
}

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

public sealed class Patient : Entity
{
    public Guid OwnerId { get; set; }
    public bool IsDefault { get; set; }
    [Required(ErrorMessage = "Podaj imię pacjenta."), StringLength(120)]
    public string Name { get; set; } = "";
}

public abstract class PatientEntry : Entity
{
    public Guid PatientId { get; set; }
}

public sealed class Visit : PatientEntry
{
    public DateOnly Date { get; set; } = MedicalRules.Today;
    [Required(ErrorMessage = "Podaj lekarza."), StringLength(200)]
    public string Doctor { get; set; } = "";
    [StringLength(200)] public string Clinic { get; set; } = "";
    [StringLength(500)] public string Diagnosis { get; set; } = "";
    [StringLength(10000)] public string Recommendations { get; set; } = "";
}

public sealed class Symptom : PatientEntry, IValidatableObject
{
    public Guid? OriginId { get; set; }
    [Required(ErrorMessage = "Podaj nazwę objawu."), StringLength(200)]
    public string Name { get; set; } = "";
    [StringLength(200)] public string Area { get; set; } = "";
    [StringLength(10000)] public string Description { get; set; } = "";
    public DateOnly StartDate { get; set; } = MedicalRules.Today;
    public DateOnly? EndDate { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (EndDate < StartDate) yield return new("Data zakończenia nie może poprzedzać początku.", [nameof(EndDate)]);
    }
}

public sealed class Medication : PatientEntry, IValidatableObject
{
    public string Color { get; set; } = "sage";
    public int DisplayOrder { get; set; }
    public string ScheduleMode { get; set; } = "period";
    [Required(ErrorMessage = "Podaj nazwę leku."), StringLength(200)]
    public string Name { get; set; } = "";
    [Range(typeof(decimal), "0.001", "1000000", ParseLimitsInInvariantCulture = true, ErrorMessage = "Dawka musi być większa od zera.")]
    public decimal Dose { get; set; } = 1;
    [Required(ErrorMessage = "Podaj jednostkę."), StringLength(50)] public string Unit { get; set; } = "ml";
    public DateOnly StartDate { get; set; } = MedicalRules.Today;
    public DateOnly? EndDate { get; set; }
    public string[] TimesOfDay { get; set; } = [];
    public Guid? VisitId { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (EndDate < StartDate) yield return new("Data zakończenia nie może poprzedzać początku.", [nameof(EndDate)]);
        if (TimesOfDay.Length == 0 || TimesOfDay.Length > 24 || TimesOfDay.Distinct().Count() != TimesOfDay.Length ||
            TimesOfDay.Any(t => ScheduleMode == "clock" ? !MedicationRules.IsClock(t) : !MedicalRules.Times.Contains(t)))
            yield return new("Wybierz przynajmniej jedną porę lub prawidłową godzinę (HH:mm), bez powtórzeń.", [nameof(TimesOfDay)]);
        if (ScheduleMode is not ("period" or "clock")) yield return new("Wybierz sposób podawania.", [nameof(ScheduleMode)]);
        if (!MedicationRules.Colors.Any(c => c.Key == Color)) yield return new("Wybierz kolor.", [nameof(Color)]);
    }
}

public sealed class DoseLog : PatientEntry
{
    public Guid MedicationId { get; set; }
    public DateOnly Date { get; set; }
    public string TimeOfDay { get; set; } = "";
    public DateTimeOffset GivenAt { get; set; } = DateTimeOffset.UtcNow;
}
