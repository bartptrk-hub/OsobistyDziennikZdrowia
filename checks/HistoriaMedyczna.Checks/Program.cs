using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using HistoriaMedyczna.Models;
using HistoriaMedyczna.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using HistoriaMedyczna.Components;

var count = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    count++;
    Console.WriteLine($"PASS {label}");
}
async Task<bool> Rejects(Func<Task> action)
{
    try { await action(); return false; }
    catch (InvalidOperationException) { return true; }
}
bool Valid(object value) => Validator.TryValidateObject(value, new ValidationContext(value), [], true);
var first = new DateOnly(2026, 10, 1);
var normalPatient = new Patient { Name="A" }; var mainPatient = new Patient { Name="Z", IsDefault=true };
Check(PatientRules.SelectInitial([normalPatient,mainPatient],null) == mainPatient.Id, "main patient is selected on a fresh login");
Check(PatientRules.SelectInitial([normalPatient,mainPatient],normalPatient.Id) == normalPatient.Id, "refresh preserves manually selected patient");
Check(PatientRules.SelectInitial([normalPatient],Guid.NewGuid()) == normalPatient.Id && PatientRules.SelectInitial([],null) is null, "profile selection handles missing default and empty accounts");
Check(!HistoryRanges.Options.Any(o => o.Key == "last1") && HistoryRanges.Options.Any(o => o.Key == "last14"), "range list keeps fourteen days and removes one day");
var todayForHistory = MedicalRules.Today;
var openSymptom = new Symptom { Name="Otwarty", StartDate=todayForHistory.AddDays(-2) };
Check(HistoryRules.SymptomVisibleOn(openSymptom,todayForHistory,todayForHistory) && !HistoryRules.SymptomVisibleOn(openSymptom,todayForHistory.AddDays(1),todayForHistory), "open symptoms stop at today without inventing an end date");
Check(openSymptom.EndDate is null, "clipping symptom display does not end the stored episode");
var planMed = new Medication { Name="Lek planu", Dose=2.5m, Unit="ml", StartDate=first, EndDate=first.AddDays(1), TimesOfDay=["rano (po posiłku)","przed snem"] };
var clockPlanMed = new Medication { Name="Godzinowy", ScheduleMode="clock", Dose=1, Unit="tabletka", StartDate=first.AddDays(1), TimesOfDay=["12:30"] };
var planRows = MedicationPlanRules.Build([planMed,clockPlanMed],first,first.AddDays(2),new HashSet<string>{"rano (po posiłku)","12:30"});
Check(planRows.Count == 4 && planRows.Count(r => r.Medication == "Lek planu") == 2 && planRows.All(r => r.Slot != "przed snem"), "printed plan respects therapy dates and selected slots");
Check(planRows.Single(r => r.Medication == "Lek planu" && r.Date == first).Dose == 2.5m && planRows.First().Unit == "ml", "printed plan copies exact dose and unit");
Check(MedicationPlanRules.Build([planMed],first.AddDays(2),first.AddDays(3),new HashSet<string>{"rano (po posiłku)"}).Count == 0, "ended therapies are excluded from future handoff plan");
try { MedicationPlanRules.Build([planMed],first.AddDays(1),first,new HashSet<string>{"rano (po posiłku)"}); throw new Exception("Expected invalid range"); }
catch (ArgumentException) { Check(true,"handoff plan rejects reversed dates"); }
Check(HistoryRanges.Get("last7", first) == (first.AddDays(-6), first) && HistoryRanges.Get("next14", first) == (first, first.AddDays(13)), "rolling ranges include today and the requested number of days");
Check(HistoryRanges.Get("previousWeek", new DateOnly(2026,11,2)) == (new DateOnly(2026,10,26),new DateOnly(2026,11,1)), "previous week crosses month boundary Monday to Sunday");
Check(HistoryRanges.Get("previousMonth", new DateOnly(2024,3,15)) == (new DateOnly(2024,2,1),new DateOnly(2024,2,29)), "calendar ranges handle leap February");
Check(HistoryRanges.Get("thisWeek", new DateOnly(2026,11,1)) == (new DateOnly(2026,10,26),new DateOnly(2026,11,1)), "Sunday remains in the current Monday-based week");
Check(Valid(new Medication { Name="Clock", ScheduleMode="clock", TimesOfDay=["08:00","20:30"] }) && !Valid(new Medication { Name="Clock", ScheduleMode="clock", TimesOfDay=["24:00"] }), "clock schedule validates canonical hours");
Check(!Valid(new Medication { Name="Mixed", ScheduleMode="clock", TimesOfDay=["rano (po posiłku)","08:00"] }), "clock and period modes cannot mix");
var orderA = new Medication { Name="A", DisplayOrder=0 }; var orderB = new Medication { Name="B", DisplayOrder=1 }; var orderC = new Medication { Name="C", DisplayOrder=2 };
Check(MedicationRules.MoveBefore([orderC,orderA,orderB],orderC.Id,orderA.Id).SequenceEqual(new[]{orderC.Id,orderA.Id,orderB.Id}), "dragging up preserves other medication positions");
Check(MedicationRules.MoveBefore([orderA,orderB,orderC],orderA.Id,orderC.Id).SequenceEqual(new[]{orderB.Id,orderA.Id,orderC.Id}), "dragging down inserts before target");
Check(MedicationRules.Slots([new Medication { TimesOfDay=["20:30","08:00"] }]).SequenceEqual(new[]{"08:00","20:30"}), "clock slots sort chronologically");
Check(MedicalRules.Active(first, first, first) && !MedicalRules.Active(first, first, first.AddDays(1)), "therapy dates include both boundaries");
Check(!Valid(new Medication { Name = "Lek", TimesOfDay = [] }), "medication requires schedule");
Check(!Valid(new Medication { Name = "Lek", Dose = 0, TimesOfDay = ["rano (po posiłku)"] }), "dose must be positive");
Check(!Valid(new Symptom { Name = "Kaszel", StartDate = first, EndDate = first.AddDays(-1) }), "symptom date ordering");
Check(!Valid(new Visit { Doctor = "  " }), "blank doctor rejected");
Check(Valid(new Medication { Name = "Lek", TimesOfDay = ["rano (po posiłku)"], StartDate = first, EndDate = first }), "valid one-day therapy");
Check(Valid(new Medication { Name = "Lek", Unit = "krople", TimesOfDay = ["rano (po posiłku)"] }), "custom dictionary unit accepted");
Check(!Valid(new DictionaryEntry { Kind = "unit", Value = new string('a', 51) }), "dictionary units respect dosage length limit");
Check(!Valid(new DictionaryEntry { Kind = "unknown", Value = "value" }), "unknown dictionary categories rejected");
Check(HistoryRules.Days(first, first.AddDays(2)).Count == 3 && HistoryRules.Days(first, first.AddDays(-1)).Count == 0 && HistoryRules.Days(first, first.AddDays(366)).Count == 0, "calendar date boundaries and large range guard");
Check(HistoryRules.Overlaps(first.AddDays(-3), first, first, first.AddDays(2)) && !HistoryRules.Overlaps(first.AddDays(-3), first.AddDays(-1), first, first.AddDays(2)), "history includes episodes touching the first day only");
var sourceSymptom = new Symptom { Name = "Katar", StartDate = first, EndDate = first.AddDays(1) };
var childSymptom = new Symptom { Name = "Kaszel", StartDate = first.AddDays(1), OriginId = sourceSymptom.Id };
var changedSymptom = new Symptom { Name = "Katar", Description = "Zmiana charakteru", StartDate = first.AddDays(1), OriginId = sourceSymptom.Id };
Check(HistoryRules.Status(sourceSymptom, [sourceSymptom, childSymptom]).Contains("Przeszło w: Kaszel") && HistoryRules.Status(sourceSymptom, [sourceSymptom]) == "Zakończony", "transition status follows persisted links and clears when child removed");
var grandchild = new Symptom { Name = "Dalszy objaw", StartDate = first.AddDays(2), OriginId = childSymptom.Id };
var independent = new Symptom { Name = "Niezależny objaw", StartDate = first };
var family = HistoryRules.OrderSymptoms([grandchild, independent, childSymptom, changedSymptom, sourceSymptom]);
var sourceIndex = family.ToList().FindIndex(r => r.Symptom.Id == sourceSymptom.Id);
Check(family[sourceIndex + 1].Symptom.Id == childSymptom.Id && family[sourceIndex + 2].Symptom.Id == grandchild.Id && family[sourceIndex + 2].Depth == 2, "child and grandchild are adjacent to their source despite input order");
Check(family.Count(r => r.Symptom.Name == "Katar") == 2 && family.Single(r => r.Symptom.Id == changedSymptom.Id).Depth == 1, "same-name transition remains a separate nested row");
var filteredFamily = HistoryRules.OrderSymptoms([grandchild, childSymptom]);
Check(filteredFamily[0].Symptom.Id == childSymptom.Id && filteredFamily[0].Depth == 0 && filteredFamily[1].Symptom.Id == grandchild.Id, "filtered-out source does not hide linked episodes");
var newestFamily = HistoryRules.OrderSymptoms([childSymptom, sourceSymptom, independent], newestRootsFirst: true);
Check(newestFamily.ToList().FindIndex(r => r.Symptom.Id == sourceSymptom.Id) < newestFamily.ToList().FindIndex(r => r.Symptom.Id == childSymptom.Id), "newest-first symptom list keeps child below its source");
var cycleA = new Symptom { Name = "A" }; var cycleB = new Symptom { Name = "B", OriginId = cycleA.Id }; cycleA.OriginId = cycleB.Id;
Check(HistoryRules.OrderSymptoms([cycleA, cycleB]).Select(r => r.Symptom.Id).Distinct().Count() == 2, "malformed relationship cycle does not loop or lose rows");

await using (var services = new ServiceCollection().AddLogging().BuildServiceProvider())
await using (var renderer = new HtmlRenderer(services, services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()))
{
    var reportHtml = await renderer.Dispatcher.InvokeAsync(async () =>
    {
        var component = await renderer.RenderComponentAsync<MedicationPlanReport>(ParameterView.FromDictionary(new Dictionary<string, object?> {
            [nameof(MedicationPlanReport.PatientName)] = "Zofia Testowa",
            [nameof(MedicationPlanReport.From)] = first,
            [nameof(MedicationPlanReport.To)] = first.AddDays(2),
            [nameof(MedicationPlanReport.Rows)] = planRows
        }));
        return WebUtility.HtmlDecode(component.ToHtmlString());
    });
    Check(reportHtml.Contains("Pacjent: Zofia Testowa") && !reportHtml.Contains("planPatient.Name"), "handoff report renders the supplied patient name");
    Check(reportHtml.Split("class=\"plan-day-date\"").Length - 1 == planRows.Select(r => r.Date).Distinct().Count() && !reportHtml.Contains("<th>Data</th>"), "handoff report groups medication rows under one heading per day");
    Check(reportHtml.Split("class=\"plan-check-box\"").Length - 1 == planRows.Count && reportHtml.Contains("Podano"), "handoff report includes one pen checkbox for every dose");
    var rendered = await renderer.Dispatcher.InvokeAsync(async () =>
    {
        var component = await renderer.RenderComponentAsync<HistoryCalendar>(ParameterView.FromDictionary(new Dictionary<string, object?> {
            [nameof(HistoryCalendar.From)] = first, [nameof(HistoryCalendar.To)] = first.AddDays(2),
            [nameof(HistoryCalendar.Visits)] = new List<Visit> { new() { Doctor = "Lekarz A", Date = first }, new() { Doctor = "Lekarz B", Date = first } },
            [nameof(HistoryCalendar.Symptoms)] = new List<Symptom> { sourceSymptom, childSymptom, changedSymptom },
            [nameof(HistoryCalendar.AllSymptoms)] = new List<Symptom> { sourceSymptom, childSymptom, changedSymptom }
        }));
        return WebUtility.HtmlDecode(component.ToHtmlString());
    });
    Check(rendered.Split("class=\"calendar-visit\"").Length - 1 == 2 && rendered.Contains("Lekarz A") && rendered.Contains("Lekarz B"), "calendar preserves separate previews for multiple visits on one day");
    Check(rendered.Contains("Powiązanie: Katar → Kaszel") && rendered.Contains("Zmiana charakteru") && rendered.Contains("symptom-shade-1"), "calendar shows relations and separate overlapping episodes with the same name");
    Check(rendered.IndexOf($"data-symptom-id=\"{sourceSymptom.Id}\"", StringComparison.Ordinal) < rendered.IndexOf($"data-symptom-id=\"{childSymptom.Id}\"", StringComparison.Ordinal) && rendered.Split("data-symptom-id=").Length - 1 == 3, "rendered calendar keeps source before linked symptoms in individual rows");
    var previewHtml = await renderer.Dispatcher.InvokeAsync(async () =>
    {
        var component = await renderer.RenderComponentAsync<HistoryPreview>(ParameterView.FromDictionary(new Dictionary<string, object?> {
            [nameof(HistoryPreview.Selected)] = sourceSymptom, [nameof(HistoryPreview.Symptoms)] = new List<Symptom> { sourceSymptom, childSymptom }
        }));
        return WebUtility.HtmlDecode(component.ToHtmlString());
    });
    Check(previewHtml.Contains("Przeszło w: Kaszel") && previewHtml.Contains("Edytuj wpis"), "history preview includes relationship and edit action");
    var futureCalendar = await renderer.Dispatcher.InvokeAsync(async () =>
    {
        var component = await renderer.RenderComponentAsync<HistoryCalendar>(ParameterView.FromDictionary(new Dictionary<string, object?> {
            [nameof(HistoryCalendar.From)] = todayForHistory, [nameof(HistoryCalendar.To)] = todayForHistory.AddDays(1),
            [nameof(HistoryCalendar.Symptoms)] = new List<Symptom> { openSymptom },
            [nameof(HistoryCalendar.Visits)] = new List<Visit> { new() { Doctor="Przyszły lekarz", Date=todayForHistory.AddDays(1) } }
        }));
        return WebUtility.HtmlDecode(component.ToHtmlString());
    });
    Check(futureCalendar.Contains("Przyszły lekarz") && futureCalendar.Split("class=\"calendar-bar symptom-shade-").Length - 1 == 1, "calendar preserves future visits and draws open symptom only through today");
}

var patient = Guid.NewGuid();
var owner = Guid.NewGuid();
var handler = new FakeHandler();
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
    ["Supabase:Url"] = "https://test.supabase.co", ["Supabase:PublishableKey"] = "sb_publishable_test"
}).Build();
var service = new SupabaseService(config, handler);
var otherCircuit = new SupabaseService(config, handler);
Check(await Rejects(() => service.List<Patient>()) && handler.Requests.Count == 0, "unauthenticated requests never reach data API");
handler.Enqueue(JsonSerializer.Serialize(new { access_token = "first-token", refresh_token = "refresh-token", expires_in = 0, user = new { id = owner } }));
await service.SignIn("test@example.com", "password");
Check(service.UserId == owner && !otherCircuit.IsSignedIn, "auth JSON mapping and isolation between circuits");
handler.Enqueue(JsonSerializer.Serialize(new { access_token = "renewed-token", refresh_token = "renewed-refresh", expires_in = 3600, user = new { id = owner } }));
handler.Enqueue($$"""[{"id":"{{Guid.NewGuid()}}","patient_id":"{{patient}}","date":"2026-10-01","doctor":"Lekarz","clinic":"","diagnosis":"","recommendations":""}]""");
var visits = await service.List<Visit>(patient);
Check(visits.Count == 1 && visits[0].PatientId == patient && visits[0].Date == first, "snake_case and DateOnly decoding");
Check(handler.Requests[1].Path.Contains("grant_type=refresh_token") && handler.Requests[1].Body.Contains("refresh-token"), "expired token refreshed before data access");
Check(handler.Requests[^1].Authorization == "Bearer renewed-token" && handler.Requests[^1].Path.Contains($"patient_id=eq.{patient}"), "patient filter and user token sent to data API");

handler.Enqueue(JsonSerializer.Serialize(Enumerable.Range(0,500).Select(_ => new { id = Guid.NewGuid(), owner_id = owner, name = "Pacjent" })));
handler.Enqueue("[]");
Check((await service.List<Patient>()).Count == 500 && handler.Requests[^1].Path.Contains("offset=500"), "pagination fetches subsequent pages");

var med = new Medication { PatientId = patient, Name = "Lek", TimesOfDay = ["rano (po posiłku)"] };
handler.Enqueue($$"""[{"id":"{{med.Id}}","patient_id":"{{patient}}","name":"Lek","dose":1,"unit":"ml","start_date":"2026-10-01","times_of_day":["rano (po posiłku)"]}]""");
await service.Save(med);
Check(handler.Requests[^1].Body.Contains("times_of_day") && handler.Requests[^1].Prefer.Contains("resolution=merge-duplicates"), "save uses named columns and stable id upsert");
handler.Enqueue("", HttpStatusCode.NoContent);
await service.MarkDose(new DoseLog { PatientId = patient, MedicationId = med.Id, Date = first, TimeOfDay = "rano (po posiłku)" });
Check(handler.Requests[^1].Path.Contains("on_conflict=medication_id,date,time_of_day") && handler.Requests[^1].Prefer.Contains("ignore-duplicates"), "dose marking is idempotent per medication/day/slot");
handler.Enqueue("[]");
Check(await Rejects(() => service.Delete<Visit>(Guid.NewGuid())), "zero-row delete is not reported as success");
handler.Enqueue("{}", HttpStatusCode.Conflict);
Check(await Rejects(() => service.Delete<Medication>(med.Id)), "database constraint failure is surfaced");
handler.Enqueue("{\"code\":\"23001\"}", HttpStatusCode.BadRequest);
try { await service.Delete<Symptom>(sourceSymptom.Id); throw new Exception("Expected restrict error"); }
catch (InvalidOperationException ex) { Check(ex.Message.Contains("powiązane objawy"), "RESTRICT errors explain linked symptoms instead of date validation"); }
handler.Enqueue("", HttpStatusCode.NoContent);
await service.InitializeDictionaries();
Check(handler.Requests[^1].Path.EndsWith("/rpc/initialize_dictionaries") && handler.Requests[^1].Authorization == "Bearer renewed-token", "dictionary initialization uses authenticated RPC");
handler.Enqueue("[]");
await service.List<DictionaryEntry>();
Check(handler.Requests[^1].Path.StartsWith("/rest/v1/dictionary_entries?") && !handler.Requests[^1].Path.Contains("patient_id"), "dictionaries scoped to account through RLS, not a patient");
handler.Enqueue("", HttpStatusCode.NoContent);
await service.ReorderMedications(patient, [orderB.Id,orderA.Id]);
Check(handler.Requests[^1].Path.EndsWith("/rpc/reorder_medications") && handler.Requests[^1].Body.Contains("p_patient_id") && handler.Requests[^1].Body.IndexOf(orderB.Id.ToString(),StringComparison.Ordinal) < handler.Requests[^1].Body.IndexOf(orderA.Id.ToString(),StringComparison.Ordinal), "reordering persists full ordered ids through authenticated RPC");
handler.Enqueue("", HttpStatusCode.NoContent);
await service.SetDefaultPatient(patient);
Check(handler.Requests[^1].Path.EndsWith("/rpc/set_default_patient") && handler.Requests[^1].Body.Contains(patient.ToString()) && handler.Requests[^1].Authorization == "Bearer renewed-token", "main patient selection is saved with the user's token");
childSymptom.PatientId = patient;
handler.Enqueue(JsonSerializer.Serialize(childSymptom, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
await service.TransitionSymptom(childSymptom, false);
Check(handler.Requests[^1].Path.EndsWith("/rpc/transition_symptom") && handler.Requests[^1].Body.Contains("\"p_close_source\":false") && handler.Requests[^1].Body.Contains("origin_id"), "linked symptom and optional source closure sent as one atomic RPC");
handler.Enqueue(JsonSerializer.Serialize(new[] { childSymptom }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
await service.TransitionSymptom(childSymptom, true);
Check(true, "composite RPC accepts PostgREST one-row array response");
handler.Enqueue("{}", HttpStatusCode.BadRequest);
Check(await Rejects(() => service.TransitionSymptom(childSymptom, true)), "invalid transition leaves editor retryable with an error");
handler.Enqueue("", HttpStatusCode.NoContent);
await service.SignOut();
Check(!service.IsSignedIn && await Rejects(() => service.List<Patient>()), "logout clears session");
Console.WriteLine($"{count} checks passed. These use a fake HTTP server; live Supabase/RLS still requires integration testing.");

sealed class FakeHandler : HttpMessageHandler, IHttpClientFactory
{
    private readonly Queue<(string Body, HttpStatusCode Status)> responses = new();
    public List<(string Path, string Body, string Authorization, string Prefer)> Requests { get; } = [];
    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
    public void Enqueue(string body, HttpStatusCode status = HttpStatusCode.OK) => responses.Enqueue((body, status));
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request.RequestUri!.PathAndQuery, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken), request.Headers.Authorization?.ToString() ?? "", request.Headers.TryGetValues("Prefer", out var preference) ? string.Join(",", preference) : ""));
        var response = responses.Dequeue();
        return new HttpResponseMessage(response.Status) { Content = new StringContent(response.Body, Encoding.UTF8, "application/json") };
    }
}
