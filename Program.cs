using LifeLine.Data;
using LifeLine.Hubs;
using LifeLine.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Database: SQL Server by default, SQLite when DatabaseProvider=Sqlite (used for free cloud demo)
var useSqlite = builder.Configuration["DatabaseProvider"] == "Sqlite";
var connection = builder.Configuration.GetConnectionString(useSqlite ? "Sqlite" : "Default");
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (useSqlite) o.UseSqlite(connection); else o.UseSqlServer(connection);
});

// ---- Login + roles (Donor / Hospital / Admin) using ASP.NET Core Identity (cookie based)
builder.Services.AddIdentity<IdentityUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 6;
    o.Password.RequireDigit = false;
    o.Password.RequireUppercase = false;
    o.Password.RequireLowercase = false;
    o.Password.RequireNonAlphanumeric = false;
    o.User.RequireUniqueEmail = true;
}).AddEntityFrameworkStores<AppDbContext>();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Login";
});

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();                       // real-time channel
builder.Services.AddScoped<MatchingService>();       // who should be alerted?
builder.Services.AddScoped<RequestService>();        // create request, respond, notify

var app = builder.Build();

// Creates the database on first run and fills it with demo data
using (var scope = app.Services.CreateScope())
{
    await SeedData.InitAsync(scope.ServiceProvider);
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<AlertHub>("/hubs/alerts");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();
