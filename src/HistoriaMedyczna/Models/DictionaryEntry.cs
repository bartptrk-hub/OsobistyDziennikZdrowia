using System.ComponentModel.DataAnnotations;

namespace HistoriaMedyczna.Models;

public sealed class DictionaryEntry : Entity, IValidatableObject
{
    public Guid OwnerId { get; set; }
    public string Kind { get; set; } = "medication";
    [Required(ErrorMessage = "Podaj nazwę."), StringLength(200)]
    public string Value { get; set; } = "";

    public static readonly (string Key, string Label)[] Categories =
    [ ("medication", "Lista leków"), ("unit", "Jednostki"), ("doctor", "Lista lekarzy"),
      ("clinic", "Lista placówek"), ("symptom", "Lista objawów"), ("area", "Obszary rozpoznania / objawów") ];

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!Categories.Any(c => c.Key == Kind)) yield return new("Wybierz kategorię słownika.", [nameof(Kind)]);
        if (Kind == "unit" && Value.Trim().Length > 50) yield return new("Nazwa jednostki może mieć do 50 znaków.", [nameof(Value)]);
    }
}
