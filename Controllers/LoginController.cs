using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using jhampro.Models;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace jhampro.Controllers
{
    public class LoginController : Controller
    {
        private readonly ILogger<LoginController> _logger;
        private readonly ApplicationDbContext _context;

        public LoginController(ILogger<LoginController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string Email, string Password)
        {
            var usuario = _context.Usuarios
                .FirstOrDefault(u => u.Correo == Email && u.Contrasena == Password);

            if (usuario != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, usuario.Nombres),
                    new Claim("UsuarioId", usuario.Id.ToString()),
                    new Claim("TipoUsuario", usuario.TipoUsuario)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties { IsPersistent = true });

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Correo o contraseña incorrectos.";
            return View();
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }

        [HttpGet]
        public IActionResult RecuperarContrasena()
        {
            ViewBag.Verificado = false;
            return View();
        }

        [HttpPost]
        public IActionResult RecuperarContrasena(RecuperarContrasenaViewModel model)
        {
            if (Request.Form["fase"] == "verificar")
            {
                var usuario = _context.Usuarios.FirstOrDefault(u => u.Correo == model.CorreoElectronico);
                if (usuario == null)
                {
                    ViewBag.Error = "El correo no está registrado.";
                    ViewBag.Verificado = false;
                    return View(model);
                }

                ViewBag.Exito = "Correo verificado. Ahora puedes ingresar la nueva contraseña.";
                ViewBag.Verificado = true;
                return View(model);
            }

            var user = _context.Usuarios.FirstOrDefault(u => u.Correo == model.CorreoElectronico);
            if (user == null)
            {
                ViewBag.Error = "Error interno. Usuario no encontrado.";
                ViewBag.Verificado = true;
                return View(model);
            }

            if (model.NuevaContrasena == user.Contrasena)
            {
                ViewBag.Error = "La nueva contraseña no puede ser igual a la anterior.";
                ViewBag.Verificado = true;
                return View(model);
            }

            user.Contrasena = model.NuevaContrasena;
            _context.SaveChanges();

            ViewBag.MostrarModal = true;
            return View("RecuperarContrasena", model);

        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Login");
        }
    }
}