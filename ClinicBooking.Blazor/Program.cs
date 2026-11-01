using ClinicBooking.Blazor;
using ClinicBooking.Blazor.Api;
using ClinicBooking.Blazor.Auth;
using ClinicBooking.Blazor.Realtime;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;

// Blazor WASM host (PDF: Blazor WebAssembly). This process has no EF and no SQL.
// It only calls the API with HttpClient. JWT lives in AuthSession / localStorage.

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is not configured.");

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// WASM is one browser user. Singleton so HttpClient's handler sees the same JWT
// as the pages (HttpClientFactory otherwise creates a second AuthSession — that
// was the 401 on My appointments).
builder.Services.AddSingleton<AuthSession>();
builder.Services.AddSingleton<JwtAuthStateProvider>();
builder.Services.AddSingleton<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddScoped<PendingBookingStore>();
builder.Services.AddScoped<BookingResume>();
// PDF SignalR: one HubConnection per browser circuit. Pages call WatchClinicAsync / StopWatchingAsync.
builder.Services.AddScoped<SlotBoardClient>();
builder.Services.AddTransient<BearerHandler>();
builder.Services.AddHttpClient<ClinicApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<BearerHandler>();

builder.Services.AddFluentUIComponents();

await builder.Build().RunAsync();
