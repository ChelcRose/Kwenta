using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Kwenta.Components;
using Kwenta.Components.Account;
using Kwenta.Data;
using Kwenta.Services;

var builder = WebApplication.CreateBuilder(args);

if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port) &&
    port is > 0 and <= 65535)
{
    builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(port));
}

if (builder.Environment.IsProduction())
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto |
            ForwardedHeaders.XForwardedHost;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.Configure<ResendOptions>(
    builder.Configuration.GetSection(ResendOptions.SectionName));
builder.Services.AddHttpClient<IEmailSender<ApplicationUser>, ResendEmailSender>(client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<TransactionAnalysisService>();
builder.Services.AddScoped<BudgetAnalysisService>();
builder.Services.AddScoped<SavingsPerformanceAnalysisService>();
builder.Services.AddScoped<SpendingComparisonAnalysisService>();
builder.Services.AddScoped<AffordabilityAnalysisService>();
builder.Services.AddScoped<PurchasePlanAnalysisService>();
builder.Services.AddScoped<ExpenseDraftParserService>();
builder.Services.AddScoped<WeeklyAllowanceService>();
builder.Services.AddScoped<TransferService>();
builder.Services.AddScoped<ReimbursementService>();
builder.Services.AddHttpClient<IFinancialChatAiService, GeminiFinancialChatService>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.UseForwardedHeaders();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
