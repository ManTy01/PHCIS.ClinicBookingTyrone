// =============================================================================
// ClinicBooking.Api — composition root (the PDF "ASP.NET Core Web API" host)
//
// This file wires everything. It does not contain booking rules.
// Controllers send MediatR commands; handlers in Application own the rules.
//
// PDF stack covered here:
//   JWT Tokens for secure API access
//   Serilog for logging
//   Swagger/OpenAPI
//   CORS so Blazor WASM (another origin) can call this API
//   SignalR /hubs/slots so other browsers see a taken cell
//   EF migrate + seed (LocalDB in Visual Studio, SQL in Docker)
// =============================================================================

using System.Security.Claims;
using System.Text;
using ClinicBooking.Api.Hubs;
using ClinicBooking.Api.Middleware;
using ClinicBooking.Api.Realtime;
using ClinicBooking.Api.Swagger;
using Microsoft.AspNetCore.Authorization;
using ClinicBooking.Application;
using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Auth;
using ClinicBooking.Infrastructure;
using ClinicBooking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog (PDF: Serilog or NLog). Console sink is enough for LocalDB / Docker logs.
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' not found.");

// JwtOptions binds the "Jwt" section (Issuer, Audience, Key, ExpiryMinutes)
// so JwtTokenService can mint tokens with the same values this middleware validates.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

// CQRS + FluentValidation + AutoMapper (Application) and EF + Identity (Infrastructure).
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

// ProblemDetails is the standard JSON error shape. ApiExceptionHandler maps
// ValidationException → 400, NotFound → 404, Conflict → 409 (double-book).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// PDF optional extra: SignalR. Hub is anonymous (status only, no PII).
// Booking still requires JWT on HTTP POST /api/appointments.
// Singleton notifier: IHubContext is thread-safe; handlers just call Notify after SaveChanges.
builder.Services.AddSignalR();
builder.Services.AddSingleton<ISlotBoardNotifier, SignalRSlotBoardNotifier>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums as "Booked" not 1 — Swagger and Blazor stay readable.
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PHCIS Clinic Booking",
        Version = "v1",
        Description =
            "Public: GET /api/clinics and GET /api/clinics/{id}/slots (no token).\n" +
            "1. POST /api/auth/login with { \"email\": \"patient@phcis.local\", \"password\": \"Password123!\" }\n" +
            "2. Copy the token string only (not the word Bearer).\n" +
            "3. Click Authorize, paste the token, then call /api/appointments.\n" +
            "Staff diary needs staff@phcis.local. Admin POST/PUT /api/clinics needs admin@phcis.local.\n" +
            "Wrong role on a protected call returns 403 Not authorised."
    });

    // PDF: JWT Tokens. Swagger "Authorize" lock sends Authorization: Bearer <token>.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT from POST /api/auth/login. Do not type the word Bearer."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
    // Without this, Try it out on anonymous login/clinics would send an empty Bearer → 401.
    options.OperationFilter<AnonymousOperationFilter>();
});

// ----- JWT bearer (PDF: Authentication & Authorization — ASP.NET Identity + JWT) -----
// The API does not use cookies. Blazor WASM stores the JWT and sends it on each call.
// We validate issuer, audience, lifetime, and HMAC signature against Jwt:Key.
var jwt = builder.Configuration.GetSection("Jwt");
var jwtKey = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");

// Wrong role (patient hitting Admin POST /api/clinics, staff hitting Patient book, …) → 403 JSON "Not authorised".
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenAuthorizationResultHandler>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Docker / local HTTP: do not demand HTTPS for token transport in Development.
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        // Map "sub"/"role" onto ClaimTypes so [Authorize(Roles = "Patient")] works.
        options.MapInboundClaims = true;
        options.IncludeErrorDetails = builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// CORS is required because Blazor WASM is a different origin from this API (7276 vs 7088,
// or Docker 8081 vs 8080). CQRS is not a replacement for CORS — we need both.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["https://localhost:7276", "http://localhost:5018", "http://localhost:8081"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // SignalR negotiate from WASM sends credentials. AllowCredentials cannot be used with AllowAnyOrigin —
            // that is why we list Blazor ports above instead of "*".
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseExceptionHandler();

// Swagger in Development always; Docker sets EnableSwagger=true so /swagger works on :8080.
var enableSwagger = app.Environment.IsDevelopment()
    || app.Configuration.GetValue("EnableSwagger", false);
if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Visual Studio F5 (Development) migrates LocalDB. Docker sets MigrateOnStartup=true
// and retries because SQL Server is still booting when the API container starts.
var migrateOnStartup = app.Environment.IsDevelopment()
    || app.Configuration.GetValue("MigrateOnStartup", false);
if (migrateOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    const int maxAttempts = 20;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch when (attempt < maxAttempts)
        {
            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
    }

    DatabaseSeeder.SeedAsync(scope.ServiceProvider).GetAwaiter().GetResult();
}

app.UseSerilogRequestLogging();

// Docker serves HTTP only (ASPNETCORE_URLS=http://+:8080). Redirecting to HTTPS would 307-loop.
if (!app.Configuration.GetValue("DisableHttpsRedirection", false))
{
    app.UseHttpsRedirection();
}

app.UseCors();
// Authentication must run before Authorization: JWT is parsed, then [Authorize] is checked.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// WASM SlotBoardClient connects here (ws://.../hubs/slots). JoinClinic / LeaveClinic live on the hub.
app.MapHub<SlotBoardHub>("/hubs/slots");
app.Run();

// Partial so WebApplicationFactory in tests can start this same Program.
public partial class Program;
