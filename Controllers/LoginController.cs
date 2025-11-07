using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using jhampro.Models;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System;
using System.Threading.Tasks;
using jhampro.Service;


namespace jhampro.Controllers
{
    public class LoginController : Controller
    {
        private readonly ILogger<LoginController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public LoginController(ILogger<LoginController> logger, ApplicationDbContext context, IEmailService emailService)
        {
            _logger = logger;
            _context = context;
            _emailService = emailService;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string Email, string Password)
        {
            var usuario = _context.Usuarios
                .FirstOrDefault(u => u.Correo == Email && u.Contrasena == Password);

            if (usuario != null)
            {
                HttpContext.Session.SetString("UsuarioNombre", usuario.Nombres);
                HttpContext.Session.SetString("apellidosAbogado", usuario.Apellidos);
                HttpContext.Session.SetString("Celular", usuario.Celular);
                HttpContext.Session.SetString("Correo", usuario.Correo);
                HttpContext.Session.SetString("TipoUsuario", usuario.TipoUsuario);
                HttpContext.Session.SetInt32("UsuarioId", usuario.Id);

                if (usuario.TipoUsuario == "Administrador")
                {
                    return RedirectToAction("Admin", "Admin"); // Asegúrate de tener esta vista/controlador
                }
                else if (usuario.TipoUsuario == "Abogado")
                {
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    _logger.LogInformation("Ingresando CLIENTE ✅");
                    return RedirectToAction("Index", "Home");
                }
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
        public async Task<IActionResult> RecuperarContrasena(RecuperarContrasenaViewModel model)
        {
            // Mantener compatibilidad con la vista existente: si vienen en la fase 'verificar', generamos token y enviamos enlace.
            if (Request.Form["fase"] == "verificar")
            {
                return await RequestPasswordReset(model.CorreoElectronico);
            }

            // Fase 'cambiar' (flujo legado): cambiar contraseña directamente
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
        [HttpPost]
        public async Task<IActionResult> RequestPasswordReset(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
            {
                ViewBag.Error = "Debe ingresar un correo.";
                return View("RecuperarContrasena");
            }

            var usuario = _context.Usuarios.FirstOrDefault(u => u.Correo == correo);
            if (usuario == null)
            {
                ViewBag.Error = "El correo no está registrado.";
                return View("RecuperarContrasena");
            }

            var token = Guid.NewGuid().ToString();
            var prt = new PasswordResetToken
            {
                UsuarioId = usuario.Id,
                Token = token,
                Expiration = DateTime.UtcNow.AddHours(1),
                Used = false
            };

            _context.PasswordResetTokens.Add(prt);
            _context.SaveChanges();

            var url = Url.Action("ResetPassword", "Login", new { token = token }, Request.Scheme);
            var subject = "Restablecer contraseña - Jham";
            var body = $"Hola {usuario.Nombres},<br/><br/>Haz clic en el siguiente enlace para restablecer tu contraseña (válido 1 hora): <a href=\"{url}\">Restablecer contraseña</a>";

            try
            {
                await _emailService.SendEmailAsync(usuario.Correo, subject, body);
                ViewBag.Exito = "Te hemos enviado un enlace para restablecer la contraseña.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando correo de restablecimiento");
                ViewBag.Error = "Ocurrió un error enviando el correo. Contacta al administrador.";
            }

            return View("RecuperarContrasena");
        }

        [HttpGet]
        public IActionResult ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                ViewBag.Error = "Token inválido.";
                return RedirectToAction("RecuperarContrasena");
            }

            var prt = _context.PasswordResetTokens.FirstOrDefault(t => t.Token == token && !t.Used && t.Expiration > DateTime.UtcNow);
            if (prt == null)
            {
                ViewBag.Error = "Token inválido o expirado.";
                return RedirectToAction("RecuperarContrasena");
            }

            var model = new ResetPasswordViewModel { Token = token };
            return View(model);
        }

        [HttpPost]
        public IActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var prt = _context.PasswordResetTokens.FirstOrDefault(t => t.Token == model.Token && !t.Used && t.Expiration > DateTime.UtcNow);
            if (prt == null)
            {
                ViewBag.Error = "Token inválido o expirado.";
                return View(model);
            }

            var user = _context.Usuarios.FirstOrDefault(u => u.Id == prt.UsuarioId);
            if (user == null)
            {
                ViewBag.Error = "Usuario no encontrado.";
                return View(model);
            }

            if (model.NuevaContrasena == user.Contrasena)
            {
                ViewBag.Error = "La nueva contraseña no puede ser igual a la anterior.";
                return View(model);
            }

            user.Contrasena = model.NuevaContrasena!;
            prt.Used = true;
            _context.SaveChanges();

            ViewBag.MostrarModal = true;
            return RedirectToAction("Login");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // Elimina todas las variables de sesión
            return RedirectToAction("Index", "Home");
        }
    }

}