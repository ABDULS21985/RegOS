using System.Security.Claims;
using FC.Engine.Application.Services;
using FC.Engine.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Infrastructure (shared layer — DB, repos, caching, validators)
builder.Services.AddInfrastructure(builder.Configuration);

// Application services (only the subset needed by the FI Portal)
builder.Services.AddScoped<IngestionOrchestrator>();
builder.Services.AddScoped<ValidationOrchestrator>();
builder.Services.AddScoped<TemplateService>();

// UI services
builder.Services.AddScoped<FC.Engine.Portal.Services.ToastService>();
builder.Services.AddScoped<FC.Engine.Portal.Services.DialogService>();

// Authentication — cookie-based for Blazor Server (separate cookie from Admin)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(4);
        options.SlidingExpiration = true;
        options.Cookie.Name = "FC.Portal.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("InstitutionUser", policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy("InstitutionAdmin", policy =>
        policy.RequireClaim(ClaimTypes.Role, "Admin"));

    options.AddPolicy("InstitutionMaker", policy =>
        policy.RequireClaim(ClaimTypes.Role, "Maker", "Admin"));

    options.AddPolicy("InstitutionChecker", policy =>
        policy.RequireClaim(ClaimTypes.Role, "Checker", "Admin"));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Login POST endpoint — authenticates institution users
app.MapPost("/account/login", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    // TODO: Replace with InstitutionAuthService in Prompt 2
    // For now, placeholder that follows the Admin cookie-auth pattern
    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
    {
        context.Response.Redirect("/login?error=invalid");
        return;
    }

    // Placeholder — will be replaced with InstitutionAuthService.ValidateLogin()
    context.Response.Redirect("/login?error=not-configured");
});

app.MapGet("/account/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    context.Response.Redirect("/login");
});

app.MapRazorComponents<FC.Engine.Portal.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
