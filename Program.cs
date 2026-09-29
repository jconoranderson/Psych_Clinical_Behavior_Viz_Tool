using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using MudBlazor.Services;
using PsychDashboard.Components;
using PsychDashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddSingleton<PatientHistoryService>();
builder.Services.AddSingleton<ExcelParsingService>();
builder.Services.AddHealthChecks();
builder.Services.AddCascadingAuthenticationState();

var requireAuthentication = builder.Configuration.GetValue<bool>("Security:RequireAuthentication");
if (requireAuthentication)
{
    var authority = builder.Configuration["Authentication:Authority"];
    var clientId = builder.Configuration["Authentication:ClientId"];
    if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(clientId))
    {
        throw new InvalidOperationException(
            "Authentication is required, but Authentication:Authority and Authentication:ClientId are not configured.");
    }

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.SlidingExpiration = true;
        })
        .AddOpenIdConnect(options =>
        {
            options.Authority = authority;
            options.ClientId = clientId;
            options.ClientSecret = builder.Configuration["Authentication:ClientSecret"];
            options.ResponseType = "code";
            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = true;
        });

    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
}

var app = builder.Build();
var configuredPathBase = builder.Configuration["PathBase"]?.Trim();
if (!string.IsNullOrWhiteSpace(configuredPathBase))
{
    app.UsePathBase($"/{configuredPathBase.Trim('/')}");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseAntiforgery();

app.UseStaticFiles();
app.MapHealthChecks("/health").AllowAnonymous();
if (requireAuthentication)
{
    app.UseAuthentication();
    app.UseAuthorization();
}
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
