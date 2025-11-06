using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using jhampro.Models;

namespace jhampro.Controllers
{
    public class AbogadoController : Controller
    {
        private readonly ILogger<AdminController> _logger;
        private readonly ApplicationDbContext _context;

        public AbogadoController(ILogger<AdminController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Abogado()
        {
            // Validar si el usuario ha iniciado sesión y es Administrador
            var tipoUsuario = HttpContext.Session.GetString("TipoUsuario");

            if (string.IsNullOrEmpty(tipoUsuario) || tipoUsuario != "Abogado")
            {
                return RedirectToAction("Login", "Login");
            }

            // Puedes enviar datos a la vista con ViewBag o un modelo
            ViewBag.UsuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
            ViewBag.apellidosAbogado=HttpContext.Session.GetString("apellidosAbogado");
            ViewBag.Celular=HttpContext.Session.GetString("Celular");
            ViewBag.Correo=HttpContext.Session.GetString("Correo");

            // Cargar valoraciones para la pestaña de valoraciones
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
            // Validar sesión y rol
            var tipoUsuario = HttpContext.Session.GetString("TipoUsuario");
            if (string.IsNullOrEmpty(tipoUsuario) || tipoUsuario != "Abogado")
            {
                return RedirectToAction("Login", "Login");
            }

            // Enviar nombre del usuario logueado a la vista
            ViewBag.UsuarioNombre = HttpContext.Session.GetString("UsuarioNombre");

            // Obtener todas las valoraciones con datos del servicio y cliente
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
            // Validar sesión y rol
            var tipoUsuario = HttpContext.Session.GetString("TipoUsuario");
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