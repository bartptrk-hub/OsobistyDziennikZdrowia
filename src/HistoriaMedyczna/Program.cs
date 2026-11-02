using HistoriaMedyczna.Components;
using HistoriaMedyczna.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Services.AddHttpClient("Supabase", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<SupabaseService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// Managed hosting can enforce public HTTPS before forwarding private HTTP.
// Keep local/self-hosted redirection unless the deployment explicitly opts in.
if (!builder.Configuration.GetValue<bool>("Hosting:HttpsAtEdge"))
{
    app.UseHttpsRedirection();
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
