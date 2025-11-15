using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using jhampro.Models;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace jhampro.Controllers
{
    [AllowAnonymous] // ✅ Acceso anónimo explícito (requerido por Codacy)
    public class LoginController : Controller
    {
        private readonly ILogger<LoginController> _logger;
        private readonly ApplicationDbContext _context;

        public LoginController(ILogger<LoginController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        // ✅ Vista principal del login
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string Email, string Password)
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ViewBag.Error = "Por favor, completa todos los campos.";
                return View();
            }

            // 1) Buscar usuario SOLO por correo
            var usuario = _context.Usuarios.FirstOrDefault(u => u.Correo == Email);

            if (usuario == null)
            {
                ViewBag.Error = "Correo o contraseña incorrectos.";
                return View();
            }

            // 2) Verificar contraseña con el hasher
            var hasher = new PasswordHasher<Usuario>();
            var result = hasher.VerifyHashedPassword(usuario, usuario.Contrasena, Password);

            if (result != PasswordVerificationResult.Success)
            {
                ViewBag.Error = "Correo o contraseña incorrectos.";
                return View();
            }

            // 3) Crear claims e iniciar sesión
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, usuario.Nombres),
                new Claim("UsuarioId", usuario.Id.ToString()),
                new Claim("TipoUsuario", usuario.TipoUsuario)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true }
            );

            return RedirectToAction("Index", "Home");
        }


        // ✅ Olvidé contraseña
        [HttpGet]
        public IActionResult OlvideContrasena()
        {
            return RedirectToAction("OlvideContrasena", "Recuperacion");
        }

        // ✅ Cierre de sesión
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Login");
        }

        // ✅ Manejo de errores
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}