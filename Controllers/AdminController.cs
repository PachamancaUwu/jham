using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization; // 👈 Necesario para [Authorize]
using jhampro.Models;

namespace jhampro.Controllers
{
    [Authorize] // 🔐 Protege todo el controlador: requiere sesión activa
    public class AdminController : Controller
    {
        private readonly ILogger<AdminController> _logger;
        private readonly ApplicationDbContext _context;

        public AdminController(ILogger<AdminController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Admin()
        {
            // ✅ Obtener tipo de usuario desde los claims
            var tipoUsuario = User.FindFirst("TipoUsuario")?.Value;

            // 🚫 Si no es Administrador, lo redirigimos al login
            if (string.IsNullOrEmpty(tipoUsuario) || tipoUsuario != "Administrador")
            {
                return RedirectToAction("Login", "Login");
            }

            // ✅ Pasar el nombre del usuario autenticado a la vista
            ViewBag.UsuarioNombre = User.Identity?.Name ?? "Sin nombre";

            // 🔹 Obtener todos los servicios con su cliente relacionado
            var servicios = await _context.Servicios
                .Include(s => s.Cliente)
                .ToListAsync();

            return View(servicios);
        }

        // ✅ Método para actualizar estado de un servicio
        [HttpPost]
        [Authorize] // (redundante pero explícito)
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarEstado(int id, string nuevoEstado)
        {
            // ✅ Validar tipo de usuario antes de permitir cambios
            var tipoUsuario = User.FindFirst("TipoUsuario")?.Value;
            if (tipoUsuario != "Administrador")
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            var servicio = await _context.Servicios.FindAsync(id);
            if (servicio == null)
                return Json(new { success = false, message = "Servicio no encontrado" });

            servicio.Estado = nuevoEstado;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Estado actualizado correctamente" });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}