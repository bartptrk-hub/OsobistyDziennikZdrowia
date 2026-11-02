using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HistoriaMedyczna.Models;

namespace HistoriaMedyczna.Services;

// Scoped per Blazor circuit. Tokens stay in server memory, never in localStorage.
public sealed class SupabaseService(IConfiguration configuration, IHttpClientFactory clients)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private readonly string url = configuration["Supabase:Url"]?.TrimEnd('/') ?? "";
    private readonly string key = configuration["Supabase:PublishableKey"] ?? "";
    private AuthSession? session;
    private DateTimeOffset expiresAt;
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    public bool IsConfigured => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && !string.IsNullOrWhiteSpace(key);
    public bool IsSignedIn => session is not null;
    public Guid UserId => session?.User.Id ?? throw new InvalidOperationException("Zaloguj się ponownie.");

    public async Task SignIn(string email, string password)
    {
        if (!IsConfigured) throw new InvalidOperationException("Najpierw skonfiguruj Supabase zgodnie z README.");
        using var response = await SendRaw(HttpMethod.Post, "/auth/v1/token?grant_type=password", new { email, password });
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Logowanie nie powiodło się. Sprawdź e-mail, hasło i potwierdzenie konta.");
        session = await response.Content.ReadFromJsonAsync<AuthSession>(Json) ?? throw new InvalidOperationException("Brak sesji logowania.");
        expiresAt = DateTimeOffset.UtcNow.AddSeconds(session.ExpiresIn);
    }

    public async Task SignOut()
    {
        try
        {
            if (session is not null)
            {
                using var response = await SendRaw(HttpMethod.Post, "/auth/v1/logout", token: session.AccessToken);
                response.EnsureSuccessStatusCode();
            }
        }
        finally { session = null; }
    }

    private async Task<string> Token()
    {
        await refreshLock.WaitAsync();
        try
        {
            if (session is null) throw new InvalidOperationException("Zaloguj się ponownie.");
            if (expiresAt <= DateTimeOffset.UtcNow.AddSeconds(60))
            {
                using var response = await SendRaw(HttpMethod.Post, "/auth/v1/token?grant_type=refresh_token", new { refresh_token = session.RefreshToken });
                if (!response.IsSuccessStatusCode)
                {
                    session = null;
                    throw new InvalidOperationException("Sesja wygasła. Zaloguj się ponownie.");
                }
                session = await response.Content.ReadFromJsonAsync<AuthSession>(Json) ?? throw new InvalidOperationException("Brak sesji.");
                expiresAt = DateTimeOffset.UtcNow.AddSeconds(session.ExpiresIn);
            }
            return session.AccessToken;
        }
        finally { refreshLock.Release(); }
    }

    private async Task<HttpResponseMessage> SendRaw(HttpMethod method, string path, object? body = null, string? token = null, string? prefer = null)
    {
        using var request = new HttpRequestMessage(method, url + path);
        request.Headers.Add("apikey", key);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (prefer is not null) request.Headers.Add("Prefer", prefer);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        return await clients.CreateClient("Supabase").SendAsync(request);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null, string? prefer = null)
    {
        var response = await SendRaw(method, path, body, await Token(), prefer);
        if (response.IsSuccessStatusCode) return response;
        var status = response.StatusCode;
        string? databaseCode = null;
        try
        {
            using var failure = await response.Content.ReadFromJsonAsync<JsonDocument>();
            if (failure?.RootElement.ValueKind == JsonValueKind.Object && failure.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String) databaseCode = code.GetString();
        }
        catch (JsonException) { /* An upstream proxy can return a non-JSON error. */ }
        response.Dispose();
        if (status == HttpStatusCode.Unauthorized) throw new InvalidOperationException("Sesja nie jest ważna. Wyloguj się i zaloguj ponownie.");
        if (status == HttpStatusCode.Conflict || databaseCode == "23001") throw new InvalidOperationException("Nie można wykonać operacji: nazwa już istnieje w słowniku lub wpis ma powiązania. Przed usunięciem objawu źródłowego usuń powiązane objawy; przed usunięciem wizyty odłącz leki. Lek z podaniami zakończ datą.");
        if (status == HttpStatusCode.BadRequest) throw new InvalidOperationException("Baza odrzuciła dane. Sprawdź daty, powiązania i harmonogram. Przy zmianie kolejności odśwież listę leków. Upewnij się, że wykonano migracje 002, 003 i 004.");
        if (status == HttpStatusCode.NotFound) throw new InvalidOperationException("Nie znaleziono elementu API. Sprawdź, czy w Supabase wykonano migracje 002, 003 i 004, a następnie spróbuj ponownie.");
        throw new InvalidOperationException($"Supabase odrzucił operację ({(int)status}). Sprawdź konfigurację, schemat bazy i uprawnienia. Zmiany nie zostały potwierdzone.");
    }

    public async Task<List<T>> List<T>(Guid? patientId = null) where T : Entity
    {
        var results = new List<T>();
        // Explicit pagination avoids silently dropping records at the PostgREST row limit.
        for (var offset = 0; ; offset += 500)
        {
            var filter = patientId is null ? "" : $"&patient_id=eq.{patientId}";
            using var response = await Send(HttpMethod.Get, $"/rest/v1/{Table<T>()}?select=*&order=id&limit=500&offset={offset}{filter}");
            var page = await response.Content.ReadFromJsonAsync<List<T>>(Json) ?? [];
            results.AddRange(page);
            if (page.Count < 500) return results;
        }
    }

    public async Task Save<T>(T entity) where T : Entity
    {
        using var response = await Send(HttpMethod.Post, $"/rest/v1/{Table<T>()}?on_conflict=id", entity, "resolution=merge-duplicates,return=representation");
        var saved = await response.Content.ReadFromJsonAsync<List<T>>(Json);
        if (saved is not { Count: 1 }) throw new InvalidOperationException("Nie otrzymano potwierdzenia zapisu.");
    }

    public async Task Delete<T>(Guid id) where T : Entity
    {
        using var response = await Send(HttpMethod.Delete, $"/rest/v1/{Table<T>()}?id=eq.{id}", prefer: "return=representation");
        var deleted = await response.Content.ReadFromJsonAsync<List<T>>(Json);
        if (deleted is not { Count: 1 }) throw new InvalidOperationException("Wpis został już usunięty lub nie masz do niego dostępu. Odśwież dane.");
    }

    public async Task MarkDose(DoseLog dose)
    {
        using var response = await Send(HttpMethod.Post, "/rest/v1/dose_logs?on_conflict=medication_id,date,time_of_day", dose, "resolution=ignore-duplicates");
    }

    public async Task InitializeDictionaries()
    {
        using var response = await Send(HttpMethod.Post, "/rest/v1/rpc/initialize_dictionaries", new { });
    }

    public async Task ReorderMedications(Guid patientId, IReadOnlyList<Guid> order)
    {
        using var response = await Send(HttpMethod.Post, "/rest/v1/rpc/reorder_medications", new { p_patient_id = patientId, p_order = order });
    }

    public async Task SetDefaultPatient(Guid patientId)
    {
        using var response = await Send(HttpMethod.Post, "/rest/v1/rpc/set_default_patient", new { p_patient_id = patientId });
    }

    public async Task TransitionSymptom(Symptom symptom, bool closeSource)
    {
        if (symptom.OriginId is null) throw new InvalidOperationException("Wybierz objaw źródłowy.");
        using var response = await Send(HttpMethod.Post, "/rest/v1/rpc/transition_symptom", new { p_symptom = symptom, p_close_source = closeSource });
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var result = document?.RootElement ?? default;
        // PostgREST can expose a composite return value as a one-row array.
        if (result.ValueKind == JsonValueKind.Array && result.GetArrayLength() == 1) result = result[0];
        if (result.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Nie otrzymano potwierdzenia powiązania objawów. Odśwież dane przed ponowieniem.");
        var saved = result.Deserialize<Symptom>(Json);
        if (saved?.Id != symptom.Id) throw new InvalidOperationException("Nie otrzymano potwierdzenia powiązania objawów.");
    }

    private static string Table<T>() => typeof(T).Name switch
    {
        nameof(Patient) => "patients", nameof(Visit) => "visits", nameof(Symptom) => "symptoms",
        nameof(Medication) => "medications", nameof(DoseLog) => "dose_logs", nameof(DictionaryEntry) => "dictionary_entries", _ => throw new NotSupportedException()
    };

    private sealed record AuthUser(Guid Id);
    private sealed record AuthSession(string AccessToken, string RefreshToken, int ExpiresIn, AuthUser User);
}
