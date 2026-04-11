using DigitalSign.Web.Services;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ───────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/digitalsign-web-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

// ── Windows Authentication (SSO) ──────────────────────────────────────────────
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// ── Typed HttpClient: IDigitalSignService (Sign/Verify/PDF) ──────────────────
builder.Services.AddHttpClient<IDigitalSignService, DigitalSignService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DigitalSignApi:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(120);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseDefaultCredentials = true,
    PreAuthenticate = true
});

// ── Named HttpClient: "DigitalSignApi" สำหรับ SignatureRegistry ───────────────
// ใช้ API Key แทน Windows Auth — แก้ Double-Hop Problem
builder.Services.AddHttpClient("DigitalSignApi", (serviceProvider, client) =>
{
    // อ่าน config ผ่าน serviceProvider — วิธีที่ถูกต้องสำหรับ Named Client
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = config["DigitalSignApi:BaseUrl"]
        ?? throw new InvalidOperationException("DigitalSignApi:BaseUrl is not configured.");
    var apiKey = config["DigitalSignApi:ApiKey"]
        ?? throw new InvalidOperationException("DigitalSignApi:ApiKey is not configured.");

    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(60);

    // ── X-Api-Key ติดมากับทุก request อัตโนมัติ ──────────────────────────────
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
});
// ไม่ต้อง ConfigurePrimaryHttpMessageHandler — ไม่ใช้ Windows Auth

// ── MVC ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
