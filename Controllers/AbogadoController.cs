using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using jhampro.Models;
using Microsoft.AspNetCore.Authorization; // 👈 Necesario para [Authorize]

namespace jhampro.Controllers
{
    [Authorize] // 👈 Obliga a estar autenticado
    public class AbogadoController : Controller
    {
        private readonly ILogger<AbogadoController> _logger;
        private readonly ApplicationDbContext _context;

        public AbogadoController(ILogger<AbogadoController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Abogado()
        {
            // Obtener el tipo de usuario desde los claims
            var tipoUsuario = User.FindFirst("TipoUsuario")?.Value;

            // Si no es abogado, redirigir al login
            if (string.IsNullOrEmpty(tipoUsuario) || tipoUsuario != "Abogado")
            {
                return RedirectToAction("Login", "Login");
            }

            // Pasar los datos del usuario autenticado a la vista
            ViewBag.UsuarioNombre = User.Identity?.Name ?? "Sin nombre";
            ViewBag.TipoUsuario = tipoUsuario;

            // Cargar valoraciones
            var valoraciones = await _context.Retroalimentaciones
                .Include(r => r.Servicio)
                    .ThenInclude(s => s.Cliente)
                .OrderByDescending(r => r.Fecha)
                .ToListAsync();

            ViewBag.Valoraciones = valoraciones;

            return View();
        }

        // ✅ Método para mostrar todas las valoraciones
        public async Task<IActionResult> Valoraciones()
        {
            var tipoUsuario = User.FindFirst("TipoUsuario")?.Value;
            if (string.IsNullOrEmpty(tipoUsuario) || tipoUsuario != "Abogado")
            {
                return RedirectToAction("Login", "Login");
            }

            ViewBag.UsuarioNombre = User.Identity?.Name ?? "Sin nombre";

            var valoraciones = await _context.Retroalimentaciones
                .Include(r => r.Servicio)
                    .ThenInclude(s => s.Cliente)
                .OrderByDescending(r => r.Fecha)
                .ToListAsync();

            return View(valoraciones);
        }

        // ✅ Método para eliminar una valoración
        [HttpPost]
        public async Task<IActionResult> EliminarValoracion(int id)
        {
            var tipoUsuario = User.FindFirst("TipoUsuario")?.Value;
            if (string.IsNullOrEmpty(tipoUsuario) || tipoUsuario != "Abogado")
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            var retroalimentacion = await _context.Retroalimentaciones.FindAsync(id);
            if (retroalimentacion == null)
            {
                return Json(new { success = false, message = "Valoración no encontrada" });
            }

            _context.Retroalimentaciones.Remove(retroalimentacion);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Valoración eliminada correctamente" });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}
