using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using jhampro.Models;
using jhampro.Models.ViewModels;
using jhampro.Service;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace jhampro.Controllers
{
    public class RecuperacionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailSendService _emailService;

        public RecuperacionController(ApplicationDbContext context, EmailSendService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult OlvideContrasena()
        {
            return View();
        }

        // =============================================
        // 2. Enviar enlace de recuperación vía correo
        // =============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarLinkRecuperacion(string correo)
        {
            if (string.IsNullOrEmpty(correo))
            {
                TempData["Error"] = "Debe ingresar un correo válido.";
                return RedirectToAction("OlvideContrasena");
            }

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo);

            // Por seguridad, no revelar si existe o no
            if (usuario == null)
            {
                TempData["Mensaje"] = "Si el correo existe, se enviará un enlace de recuperación.";
                return RedirectToAction("OlvideContrasena");
            }

            // Generar token seguro
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

            usuario.ResetToken = token;
            usuario.ResetTokenExpira = DateTime.UtcNow.AddHours(1);

            await _context.SaveChangesAsync();

            // Construir URL de recuperación
            var link = Url.Action(
                "RestablecerContrasena",
                "Recuperacion",
                new { token = token, correo = usuario.Correo },
                protocol: HttpContext.Request.Scheme
            );

            // Enviar correo
            string asunto = "Recuperación de contraseña - JHAM";
            string mensaje = $@"
                <h2>Restablecimiento de contraseña</h2>
                <p>Hola {usuario.Nombres},</p>
                <p>Haz solicitado restablecer tu contraseña.</p>
                <p><a href='{link}'>Haz clic aquí para cambiar tu contraseña</a></p>
                <p>Este enlace expirará en 1 hora.</p>
            ";

            var resultado = await _emailService.SendEmailAsync(usuario.Correo, asunto, mensaje);
            Console.WriteLine("Resultado envío: " + resultado);


            TempData["Mensaje"] = "Si el correo existe, se enviará un enlace de recuperación.";
            return RedirectToAction("OlvideContrasena");
        }

        // ==========================================
        // 3. Vista donde el usuario cambia contraseña
        // ==========================================
        public async Task<IActionResult> RestablecerContrasena(string token, string correo)
        {
            if (token == null || correo == null)
                return BadRequest("Token inválido.");

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u =>
                u.Correo == correo && u.ResetToken == token);

            if (usuario == null || usuario.ResetTokenExpira < DateTime.UtcNow)
                return BadRequest("El enlace ha expirado o es inválido.");

            var vm = new ResetPasswordViewModel
            {
                Token = token,
                Email = correo
            };

            return View(vm);
        }

        // ======================================
        // 4. Guardar nueva contraseña (POST)
        // ======================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestablecerContrasena(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Correo == model.Email && u.ResetToken == model.Token);

            if (usuario == null || usuario.ResetTokenExpira < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "El token ha expirado o es inválido.");
                return View(model);
            }

            // 🔐 Hashear contraseña usando PasswordHasher (MISMO ALGORITMO QUE REGISTRO)
            var hasher = new PasswordHasher<Usuario>();
            usuario.Contrasena = hasher.HashPassword(usuario, model.NewPassword);

            usuario.ResetToken = null;
            usuario.ResetTokenExpira = null;

            await _context.SaveChangesAsync();

            TempData["Exito"] = "Tu contraseña ha sido actualizada correctamente.";
            return RedirectToAction("Login", "Login");
        }

    }
}
