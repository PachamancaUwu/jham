using Microsoft.EntityFrameworkCore;
using jhampro.Models;
using jhampro.Service;
using Amazon.S3;
using Amazon;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using System.IO;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// --- CONFIGURACIÓN DE BASE DE DATOS ---
var connectionString = builder.Configuration.GetConnectionString("PostgreSQLConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

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

// --- AWS ---
var awsOptions = new AWSOptions
{
    Credentials = new BasicAWSCredentials(
        builder.Configuration["AWS:AccessKey"],
        builder.Configuration["AWS:SecretKey"]
    ),
    Region = RegionEndpoint.GetBySystemName(builder.Configuration["AWS:Region"])
};
builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonS3>();

// --- FIREBASE ---
PdfSharpCore.Fonts.GlobalFontSettings.FontResolver = new CustomFontResolver();

var serviceAccountPath = Path.Combine(builder.Environment.ContentRootPath, "Properties", "jham-docs-firebase-adminsdk-fbsvc-ce7a548c39.json");

GoogleCredential credential;
try
{
    credential = GoogleCredential.FromFile(serviceAccountPath);
    FirebaseApp.Create(new AppOptions()
    {
        Credential = credential
    });
    Console.WriteLine("✅ Firebase Admin SDK inicializado correctamente.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"❌ Error al inicializar Firebase Admin SDK: {ex.Message}");
    throw;
}

// Registro de StorageClient
builder.Services.AddSingleton(StorageClient.Create(GoogleCredential.FromFile(serviceAccountPath)));

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
