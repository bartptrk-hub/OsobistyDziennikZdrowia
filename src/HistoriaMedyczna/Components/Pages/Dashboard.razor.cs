using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HistoriaMedyczna.Models;
using Microsoft.AspNetCore.Components;

namespace HistoriaMedyczna.Components.Pages;

public partial class Dashboard
{
    private static readonly (string Key, string Icon, string Label)[] Tabs =
    [ ("daily", "◷", "Kalendarz podawania"), ("history", "▤", "Historia medyczna"), ("symptoms", "♡", "Objawy"), ("visits", "✚", "Wizyty i diagnozy"), ("medications", "◉", "Leki i suplementy"), ("admin", "⚙", "Słowniki danych") ];
    private string tab = "daily", search = "", error = "", message = "";
    private bool busy, dark, showFinished, showMedicationPlan;
    private Guid? patientId;
    private List<Patient> patients = [];
    private List<Visit> visits = [];
    private List<Symptom> symptoms = [];
    private List<Medication> medications = [];
    private List<DoseLog> doses = [];
    private List<DictionaryEntry> dictionaries = [];
    private string dictionaryKind = "medication", historyArea = "";
    private bool historyCalendar = true, closeTransitionSource, dictionariesInitialized, mobileMenuOpen;
    private Guid? transitionSourceId;
    private Guid? draggedMedication;
    private string historyPreset = "last14", clockTimeInput = "08:00";
    private Entity? preview;
    private Entity? editor, pendingDelete;
    private readonly LoginModel login = new();
    private DateOnly selectedDate = MedicalRules.Today;
    private DateOnly historyFrom = MedicalRules.Today.AddDays(-13), historyTo = MedicalRules.Today;
    private Patient? CurrentPatient => patients.FirstOrDefault(p => p.Id == patientId);
    private IEnumerable<Medication> ActiveMeds => MedicationRules.Ordered(medications.Where(m => MedicalRules.Active(m.StartDate, m.EndDate, selectedDate)));
    private void ApplyHistoryPreset(ChangeEventArgs args)
    {
        historyPreset = args.Value?.ToString() ?? "custom";
        if (historyPreset != "custom") (historyFrom, historyTo) = HistoryRanges.Get(historyPreset, MedicalRules.Today);
    }
    private void ChangeSchedule(Medication med, ChangeEventArgs args)
    {
        med.ScheduleMode = args.Value?.ToString() ?? "period";
        med.TimesOfDay = [];
    }
    private void AddClockTime(Medication med)
    {
        if (MedicationRules.IsClock(clockTimeInput) && !med.TimesOfDay.Contains(clockTimeInput))
            med.TimesOfDay = med.TimesOfDay.Append(clockTimeInput).OrderBy(t => t, StringComparer.Ordinal).ToArray();
    }
    private Task DropMedication(Guid target)
    {
        var source = draggedMedication; draggedMedication = null;
        return source is Guid id ? MoveMedication(id, target) : Task.CompletedTask;
    }
    private Task MoveMedication(Guid source, Guid target) => Run(async () =>
    {
        if (patientId is not Guid id || source == target) return;
        var order = MedicationRules.MoveBefore(medications, source, target);
        await Database.ReorderMedications(id, order);
        medications = await Database.List<Medication>(id);
        message = "Zapisano kolejność leków.";
    });
    private Task ShiftMedication(Guid id, int direction)
    {
        var ordered = MedicationRules.Ordered(medications).Select(m => m.Id).ToList();
        var index = ordered.IndexOf(id); var other = index + direction;
        if (index < 0 || other < 0 || other >= ordered.Count) return Task.CompletedTask;
        return direction < 0 ? MoveMedication(id, ordered[other]) : MoveMedication(ordered[other], id);
    }
    private int PlannedDoses => ActiveMeds.Sum(m => m.TimesOfDay.Length);
    private int GivenDoses => ActiveMeds.Sum(m => m.TimesOfDay.Count(t => FindDose(m.Id, t) is not null));
    private IEnumerable<Symptom> FilteredSymptoms => HistoryRules.OrderSymptoms(symptoms.Where(s => (showFinished || s.EndDate is null || s.EndDate >= MedicalRules.Today) && Matches(s.Name, s.Area, s.Description)), newestRootsFirst: true).Select(row => row.Symptom);
    private string Subtitle => tab switch { "daily" => "Sprawdź plan na wybrany dzień i odnotuj podane dawki.", "history" => "Prześledź wizyty, objawy i terapie w wybranym okresie.", "symptoms" => "Zapisuj obserwacje oraz czas trwania objawów.", "visits" => "Zachowaj rozpoznania i zalecenia z każdej wizyty.", "admin" => "Zarządzaj podpowiedziami wspólnymi dla wszystkich profili na Twoim koncie.", _ => "Wszystkie terapie i ich harmonogramy podawania." };
    private string AddLabel => tab switch { "visits" => "Dodaj wizytę", "symptoms" => "Dodaj objaw", _ => "Dodaj lek" };
    private string EditorTitle => editor switch { Patient => "Nowy profil pacjenta", DictionaryEntry d => DictionaryEntry.Categories.First(c => c.Key == d.Kind).Label, Visit => "Wizyta lekarska", Symptom when transitionSourceId is not null => "Przeszło w… — powiązany objaw", Symptom => "Objaw i jego przebieg", _ => "Lek i dawkowanie" };
    private static string Date(DateOnly date) => date.ToString("dd.MM.yyyy");
    private static string Period(DateOnly start, DateOnly? end) => $"{Date(start)} – {(end is null ? "bez daty zakończenia" : Date(end.Value))}";
    private bool Matches(params string[] values) => values.Any(v => v.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
    private DoseLog? FindDose(Guid medicationId, string time) => doses.FirstOrDefault(d => d.MedicationId == medicationId && d.Date == selectedDate && d.TimeOfDay == time);
    private void ChangeTab(string value) { tab = value; search = ""; message = ""; preview = null; mobileMenuOpen = false; showMedicationPlan = false; }
    private void NewPatient() { error = ""; transitionSourceId = null; editor = new Patient { OwnerId = Database.UserId }; }
    private void NewEntry()
    {
        if (patientId is not Guid id) return;
        error = ""; transitionSourceId = null;
        editor = tab switch { "visits" => new Visit { PatientId = id }, "symptoms" => new Symptom { PatientId = id }, _ => new Medication { PatientId = id, DisplayOrder = medications.Select(m => m.DisplayOrder).DefaultIfEmpty(-1).Max() + 1, Unit = DictionaryValues("unit").Contains("ml") ? "ml" : DictionaryValues("unit").FirstOrDefault() ?? "" } };
    }
    private void Edit<T>(T entity) where T : Entity { error = ""; transitionSourceId = null; preview = null; editor = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(entity)); }
    private void EditPreview(Entity entity)
    {
        switch (entity) { case Visit v: Edit(v); break; case Symptom s: Edit(s); break; case Medication m: Edit(m); break; }
    }
    private void CloseEditor() { if (!busy) { editor = null; transitionSourceId = null; } }
    private void AskDelete(Entity entity) { error = ""; pendingDelete = entity; }
    private void BeginTransition(Symptom source)
    {
        error = ""; preview = null; transitionSourceId = source.Id; closeTransitionSource = false;
        editor = new Symptom { PatientId = source.PatientId, OriginId = source.Id, Name = source.Name, Area = source.Area, StartDate = source.StartDate > MedicalRules.Today ? source.StartDate : MedicalRules.Today };
    }
    private void NewDictionaryEntry() { error = ""; transitionSourceId = null; editor = new DictionaryEntry { OwnerId = Database.UserId, Kind = dictionaryKind }; }
    private IEnumerable<string> DictionaryValues(string kind) => dictionaries.Where(d => d.Kind == kind).Select(d => d.Value).OrderBy(v => v, StringComparer.CurrentCultureIgnoreCase);
    private IEnumerable<string> UnitsFor(Medication m) => DictionaryValues("unit").Append(m.Unit).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase);
    private static void ToggleTime(Medication med, string time) => med.TimesOfDay = med.TimesOfDay.Contains(time) ? med.TimesOfDay.Where(t => t != time).ToArray() : [.. med.TimesOfDay, time];

    private async Task Run(Func<Task> action)
    {
        if (busy) return;
        busy = true; error = ""; message = "";
        try { await action(); }
        catch (InvalidOperationException ex) { error = ex.Message; }
        catch (HttpRequestException) { error = "Nie udało się połączyć z Supabase. Sprawdź połączenie i odśwież dane przed ponowieniem operacji."; }
        catch (TaskCanceledException) { error = "Przekroczono czas połączenia. Zapis mógł dotrzeć do bazy — odśwież dane przed ponowieniem."; }
        catch (JsonException) { error = "Odpowiedź bazy ma nieoczekiwany format. Sprawdź, czy wykonano migracje 002, 003 i 004, i odśwież dane przed ponowieniem zapisu."; }
        finally
        {
            if (!Database.IsSignedIn) { ClearData(); patientId = null; patients = []; dictionaries = []; dictionariesInitialized = false; editor = null; pendingDelete = null; preview = null; showMedicationPlan = false; }
            busy = false;
        }
    }
    private Task Login() => Run(async () =>
    {
        try { await Database.SignIn(login.Email.Trim(), login.Password); }
        finally { login.Password = ""; }
        await LoadPatients();
    });
    private Task Logout() => Run(async () =>
    {
        await Database.SignOut();
        ClearData(); patients = []; patientId = null; editor = null; pendingDelete = null;
    });
    private void ClearData() { visits = []; symptoms = []; medications = []; doses = []; }
    private async Task LoadPatients()
    {
        if (!dictionariesInitialized) { await Database.InitializeDictionaries(); dictionariesInitialized = true; }
        dictionaries = await Database.List<DictionaryEntry>();
        patients = (await Database.List<Patient>()).OrderBy(p => p.Name).ToList();
        var selectedPatient = PatientRules.SelectInitial(patients, patientId);
        if (selectedPatient != patientId) { patientId = selectedPatient; ClearData(); }
        await LoadEntries();
    }
    private Task SelectPatient(ChangeEventArgs args) => Run(async () =>
    {
        patientId = Guid.TryParse(args.Value?.ToString(), out var id) ? id : null;
        ClearData(); search = ""; historyArea = ""; preview = null;
        await LoadEntries();
    });
    private async Task LoadEntries()
    {
        if (patientId is not Guid id) { ClearData(); return; }
        var newVisits = await Database.List<Visit>(id);
        var newSymptoms = await Database.List<Symptom>(id);
        var newMedications = await Database.List<Medication>(id);
        var newDoses = await Database.List<DoseLog>(id);
        visits = newVisits; symptoms = newSymptoms; medications = newMedications; doses = newDoses;
    }
    private Task Refresh() => Run(LoadPatients);
    private Task SetMainPatient() => Run(async () =>
    {
        if (patientId is not Guid id) return;
        await Database.SetDefaultPatient(id);
        await LoadPatients();
        message = "Zapisano profil główny. Będzie wybierany po zalogowaniu.";
    });
    private Task SaveEditor() => Run(async () =>
    {
        switch (editor)
        {
            case DictionaryEntry d: d.Value = d.Value.Trim(); await Database.Save(d); break;
            case Patient p:
                p.Name = p.Name.Trim(); await Database.Save(p);
                if (!patients.Any(existing => existing.IsDefault)) await Database.SetDefaultPatient(p.Id);
                patientId = p.Id; ClearData(); break;
            case Visit v: v.Doctor = v.Doctor.Trim(); await Database.Save(v); break;
            case Symptom s:
                s.Name = s.Name.Trim();
                if (transitionSourceId is not null) await Database.TransitionSymptom(s, closeTransitionSource);
                else await Database.Save(s);
                break;
            case Medication m: m.Name = m.Name.Trim(); await Database.Save(m); break;
        }
        editor = null; transitionSourceId = null;
        message = "Zapisano w Supabase.";
        await LoadPatients();
    });
    private Task ConfirmDelete() => Run(async () =>
    {
        switch (pendingDelete)
        {
            case DictionaryEntry d: await Database.Delete<DictionaryEntry>(d.Id); break;
            case Visit v: await Database.Delete<Visit>(v.Id); break;
            case Symptom s: await Database.Delete<Symptom>(s.Id); break;
            case Medication m: await Database.Delete<Medication>(m.Id); break;
        }
        pendingDelete = null;
        await LoadPatients();
        message = "Usunięto wpis.";
    });
    private Task ToggleDose(Medication med, string time) => Run(async () =>
    {
        if (selectedDate > MedicalRules.Today) throw new InvalidOperationException("Nie można oznaczyć przyszłej dawki jako podanej.");
        var existing = FindDose(med.Id, time);
        if (existing is not null) await Database.Delete<DoseLog>(existing.Id);
        else await Database.MarkDose(new DoseLog { PatientId = med.PatientId, MedicationId = med.Id, Date = selectedDate, TimeOfDay = time });
        doses = await Database.List<DoseLog>(patientId);
    });
    private IEnumerable<Visit> HistoryVisits => visits.Where(v => v.Date >= historyFrom && v.Date <= historyTo && Matches(v.Doctor, v.Clinic, v.Diagnosis, v.Recommendations));
    private IEnumerable<Symptom> HistorySymptoms => symptoms.Where(s => HistoryRules.Overlaps(s.StartDate, HistoryRules.SymptomEnd(s, MedicalRules.Today), historyFrom, historyTo) && (historyArea == "" || s.Area == historyArea) && Matches(s.Name, s.Area, s.Description));
    private IEnumerable<Medication> HistoryMedications => medications.Where(m => HistoryRules.Overlaps(m.StartDate, m.EndDate, historyFrom, historyTo) && Matches(m.Name));
    private sealed record HistoryItem(DateOnly Date, string Kind, string Title, string Period, string Detail, Entity Entry);
    private IEnumerable<HistoryItem> HistoryEventsChronological => HistoryVisits
        .Select(v => new HistoryItem(v.Date, "Wizyta", v.Doctor, Date(v.Date), $"{v.Clinic}\n{v.Diagnosis}\n{v.Recommendations}", v))
        .Concat(HistorySymptoms.Select(s => new HistoryItem(s.StartDate, "Objaw", s.Name, Period(s.StartDate, s.EndDate), $"{s.Area}\n{HistoryRules.Status(s, symptoms)}\n{s.Description}", s)))
        .Concat(HistoryMedications.Select(m => new HistoryItem(m.StartDate, "Lek", m.Name, Period(m.StartDate, m.EndDate), $"{m.Dose} {m.Unit} · {string.Join(", ", m.TimesOfDay)}", m)))
        .OrderByDescending(e => e.Date);
    private IEnumerable<HistoryItem> HistoryEvents
    {
        get
        {
            var entries = HistoryEventsChronological.ToArray();
            var byId = entries.ToDictionary(e => e.Entry.Id);
            var families = new List<List<HistoryItem>>();
            foreach (var row in HistoryRules.OrderSymptoms(entries.Select(e => e.Entry).OfType<Symptom>()))
            {
                if (row.Depth == 0) families.Add([]);
                families[^1].Add(byId[row.Symptom.Id]);
            }
            families.AddRange(entries.Where(e => e.Entry is not Symptom).Select(e => new List<HistoryItem> { e }));
            return families.OrderByDescending(f => f[0].Date).SelectMany(f => f);
        }
    }
    private sealed class LoginModel
    {
        [Required(ErrorMessage = "Podaj e-mail."), EmailAddress(ErrorMessage = "Podaj prawidłowy adres e-mail.")]
        public string Email { get; set; } = "";
        [Required(ErrorMessage = "Podaj hasło.")]
        public string Password { get; set; } = "";
    }
}
