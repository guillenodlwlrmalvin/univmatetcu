using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using UnivMate.Data;
using UnivMate.Hubs;
using UnivMate.Models;
using DinkToPdf;
using DinkToPdf.Contracts;
using System.IO;
using UnivMate.Services;
using UnivMate.Extensions;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.InteropServices;


var builder = WebApplication.CreateBuilder(args);

// Add SignalR
builder.Services.AddSignalR();

// Add PDF converter
var context = new CustomAssemblyLoadContext();
var architecture = RuntimeInformation.ProcessArchitecture;
var dllPath = Path.Combine(Directory.GetCurrentDirectory(), "Libs", "wkhtmltopdf", "libwkhtmltox.dll");

if (!File.Exists(dllPath))
{
    throw new FileNotFoundException("libwkhtmltox.dll not found at: " + dllPath);
}

context.LoadUnmanagedLibrary(dllPath);
builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
context.LoadUnmanagedLibrary(Path.Combine(Directory.GetCurrentDirectory(), "libwkhtmltox.dll"));
builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));

// Add services
builder.Services.AddScoped<PdfConverterService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpContextAccessor();
builder.WebHost.CaptureStartupErrors(true);  // Add this line
builder.WebHost.UseSetting("detailedErrors", "true");  // And this


// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddControllersWithViews();

// Configure cookie settings
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Strict;
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.Secure = CookieSecurePolicy.Always;
});

// Add authentication services with enhanced configuration
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "UnivMate.Auth";
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;

        // Security settings
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;

        // For environments behind a proxy
        options.Cookie.IsEssential = true;

        // Session store options to prevent infinite redirects
        options.SessionStore = new MemoryCacheTicketStore();
    });

// Add authorization with default policy
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAuthenticatedUser", policy =>
    {
        policy.RequireAuthenticatedUser();
    });
});

// Add session support (optional but recommended)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.MapHub<ReportHub>("/reportHub");
app.UseHttpsRedirection();
app.UseStaticFiles();

// Important: Cookie Policy must come before Authentication
app.UseCookiePolicy();
app.UseRouting();

// Session middleware should come after routing but before authentication
app.UseSession();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "staff",
    pattern: "StaffDashboard/{action=Index}/{id?}",
    defaults: new { controller = "StaffDashboard" });

app.Run();

// Implementation of ITicketStore for session store
public class MemoryCacheTicketStore : Microsoft.AspNetCore.Authentication.Cookies.ITicketStore
{
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private const string KeyPrefix = "AuthSessionStore-";

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var options = new MemoryCacheEntryOptions();
        var expiresUtc = ticket.Properties.ExpiresUtc;
        if (expiresUtc.HasValue)
        {
            options.SetAbsoluteExpiration(expiresUtc.Value);
        }
        options.SetSlidingExpiration(TimeSpan.FromMinutes(15));

        _cache.Set(key, ticket, options);
        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket> RetrieveAsync(string key)
    {
        _cache.TryGetValue(key, out AuthenticationTicket ticket);
        return Task.FromResult(ticket);
    }

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = KeyPrefix + Guid.NewGuid().ToString();
        RenewAsync(key, ticket);
        return Task.FromResult(key);
    }
}

// Custom Assembly Load Context for PDF conversion
public class CustomAssemblyLoadContext : AssemblyLoadContext
{
    public IntPtr LoadUnmanagedLibrary(string absolutePath)
    {
        return LoadUnmanagedDll(absolutePath);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        return LoadUnmanagedDllFromPath(unmanagedDllName);
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        throw new NotImplementedException();
    }
}