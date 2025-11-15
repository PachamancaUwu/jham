using Microsoft.EntityFrameworkCore;
using jhampro.Models;
using jhampro.Service;
using Google.Apis.Auth.OAuth2;
using System.IO;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authentication.Cookies;
using FirebaseAdmin;
using System.Text.Json;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

// --- CONFIGURACIÓN DE BASE DE DATOS ---
var connectionString = builder.Configuration.GetConnectionString("PostgreSQLConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Cargar el archivo .env.local
Env.Load(".env.local");

// --- SERVICIOS PRINCIPALES ---
builder.Services.AddControllersWithViews();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

// --- AUTENTICACIÓN Y AUTORIZACIÓN ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Login"; // ruta al login
        options.AccessDeniedPath = "/Home/AccesoDenegado"; // opcional
    });
builder.Services.AddAuthorization();

var dotenvPath = Path.Combine(Directory.GetCurrentDirectory(), ".env.local");
if (File.Exists(dotenvPath))
{
    var lines = File.ReadAllLines(dotenvPath);
    foreach (var line in lines)
    {
        var parts = line.Split('=', 2);
        if (parts.Length == 2)
            Environment.SetEnvironmentVariable(parts[0], parts[1]);
    }
}

// --- FIREBASE: crear credenciales dinámicamente desde variables de entorno ---
var firebaseConfig = new
{
    type = Environment.GetEnvironmentVariable("FIREBASE_TYPE"),
    project_id = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID"),
    private_key_id = Environment.GetEnvironmentVariable("FIREBASE_PRIVATE_KEY_ID"),
    private_key = Environment.GetEnvironmentVariable("FIREBASE_PRIVATE_KEY")?.Replace("\\n", "\n"), // ✅ reemplazar \\n por saltos reales
    client_email = Environment.GetEnvironmentVariable("FIREBASE_CLIENT_EMAIL"),
};

var json = JsonSerializer.Serialize(firebaseConfig);

// Crear credencial de Firebase
var credential = GoogleCredential.FromJson(json);

// Inicializar Firebase
FirebaseApp.Create(new AppOptions
{
    Credential = credential
});
Console.WriteLine("✅ Firebase inicializado correctamente (variables de entorno)");

// Registrar StorageClient en DI
builder.Services.AddSingleton(StorageClient.Create(credential));

//API SendGrid
builder.Services.AddTransient<EmailSendService>();
Console.WriteLine("APIKEY: " + Environment.GetEnvironmentVariable("SENDGRID_API_KEY"));

var app = builder.Build();

// --- PIPELINE ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSession();          // ✅ Sesión primero
app.UseAuthentication();   // ✅ Luego autenticación
app.UseAuthorization();    // ✅ Luego autorización

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
