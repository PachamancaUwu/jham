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
using Google.Cloud.Storage.V1; // ¡Necesitamos este using para StorageClient!

var builder = WebApplication.CreateBuilder(args);

// Configurar la conexión a la base de datos
var connectionString = builder.Configuration.GetConnectionString("PostgreSQLConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

// 🧩 Agregar servicios necesarios
builder.Services.AddControllersWithViews();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

// ✅ Configurar credenciales AWS desde appsettings.json
var awsOptions = new AWSOptions
{
    Credentials = new BasicAWSCredentials(
        builder.Configuration["AWS:AccessKey"],
        builder.Configuration["AWS:SecretKey"]
    ),
    Region = RegionEndpoint.GetBySystemName(builder.Configuration["AWS:Region"])
};

builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonS3>(); // Ahora sí funcionará correctamente

// Registrar font resolver global
PdfSharpCore.Fonts.GlobalFontSettings.FontResolver = new CustomFontResolver();


// --- INICIALIZACIÓN DE FIREBASE ADMIN SDK Y REGISTRO DE STORAGECLIENT ---
// Ruta a tu archivo de clave de cuenta de servicio.
// Asegúrate de que esta ruta sea correcta y el archivo esté protegido.
var serviceAccountPath = Path.Combine(builder.Environment.ContentRootPath, "Properties", "jham-docs-firebase-adminsdk-fbsvc-ce7a548c39.json");

GoogleCredential credential = null; // Declaramos la credencial aquí para usarla más adelante

try
{
    credential = GoogleCredential.FromFile(serviceAccountPath); // Cargamos la credencial
    FirebaseApp.Create(new AppOptions()
    {
        Credential = credential // Usamos la credencial cargada para FirebaseApp
    });

    Console.WriteLine("Firebase Admin SDK inicializado correctamente.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error al inicializar Firebase Admin SDK: {ex.Message}");
    // Es crucial que la aplicación no continúe si no puede autenticarse con Firebase.
    // Lanza la excepción para que el inicio de la app falle si las credenciales no son válidas.
    throw;
}

// ¡IMPORTANTE! Registramos StorageClient con la credencial cargada.
// Esto permite que tus controladores inyecten StorageClient y se autentiquen correctamente.
builder.Services.AddSingleton(StorageClient.Create(credential));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();


app.UseSession();
app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
