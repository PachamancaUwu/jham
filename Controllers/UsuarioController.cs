using Microsoft.AspNetCore.Mvc;
using jhampro.Models;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;

namespace jhampro.Controllers
{
    //Acceso público permitido
    public class UsuarioController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public UsuarioController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // ✅ Vista de registro
        [HttpGet]
        public IActionResult Registrarse()
        {
            var usuario = new Usuario { TipoUsuario = "Cliente" };
            return View(usuario);
        }

        // ✅ POST de registro con validaciones
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrarse(Usuario usuario)
        {
            // Forzamos tipo de usuario (seguridad)
            usuario.TipoUsuario = "Cliente";

            // Validación de duplicados (correo y DNI)
            if (_context.Usuarios.Any(u => u.Correo == usuario.Correo))
            {
                ModelState.AddModelError("Correo", "Este correo ya está registrado.");
            }
            if (!string.IsNullOrEmpty(usuario.Dni) && _context.Usuarios.Any(u => u.Dni == usuario.Dni))
            {
                ModelState.AddModelError("Dni", "Este DNI ya está registrado.");
            }

            if (!ModelState.IsValid)
            {
                // Diagnóstico opcional
                foreach (var key in ModelState.Keys)
                {
                    var errors = ModelState[key].Errors;
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"Error en {key}: {error.ErrorMessage}");
                    }
                }
                return View(usuario);
            }

            // 🔐 1. Hashear la contraseña antes de guardar
            var hasher = new PasswordHasher<Usuario>();
            usuario.Contrasena = hasher.HashPassword(usuario, usuario.Contrasena);

            // 🔐 2. ResetToken empieza en null (correcto)
            usuario.ResetToken = null;
            usuario.ResetTokenExpira = null;

            // 🔐 3. Guardar usuario
            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return RedirectToAction("RegistroExitoso");
        }


        // ✅ Vista de confirmación de registro
        public IActionResult RegistroExitoso()
        {
            return View();
        }

        // ✅ Endpoint para verificar DNI desde la API de apisperu
        [HttpGet]
        public async Task<JsonResult> VerificarDni(string dni)
        {
            // Token seguro obtenido desde appsettings.json
            var token = _config["ApiPeru:Token"];

            if (string.IsNullOrEmpty(token))
            {
                return Json(new { error = "Error de configuración: token de API no disponible." });
            }

            var url = $"https://dniruc.apisperu.com/api/v1/dni/{dni}?token={token}";

            using var httpClient = new HttpClient();

            try
            {
                var response = await httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { error = $"Error al consultar el DNI ({response.StatusCode})." });
                }

                var datos = JsonSerializer.Deserialize<JsonElement>(content);

                return Json(new
                {
                    nombres = datos.GetProperty("nombres").GetString(),
                    apellidoPaterno = datos.GetProperty("apellidoPaterno").GetString(),
                    apellidoMaterno = datos.GetProperty("apellidoMaterno").GetString()
                });
            }
            catch
            {
                return Json(new { error = "No se pudo verificar el DNI. Intente nuevamente más tarde." });
            }
        }
    }
}