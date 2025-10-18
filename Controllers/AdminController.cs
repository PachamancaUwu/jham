using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using jhampro.Models;

namespace jhampro.Controllers
{
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
            // Validar sesión y rol
            var tipoUsuario = HttpContext.Session.GetString("TipoUsuario");
            if (string.IsNullOrEmpty(tipoUsuario) || tipoUsuario != "Administrador")
            {
                return RedirectToAction("Login", "Login");
            }

            // Enviar nombre del usuario logueado a la vista
            ViewBag.UsuarioNombre = HttpContext.Session.GetString("UsuarioNombre");

            // 🔹 Obtener todos los servicios con su cliente relacionado
            var servicios = await _context.Servicios
                .Include(s => s.Cliente)
                .ToListAsync();

            // Enviar lista a la vista
            return View(servicios);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
          // ✅ NUEVO MÉTODO PARA ACTUALIZAR ESTADO
    [HttpPost]
    public async Task<IActionResult> ActualizarEstado(int id, string nuevoEstado)
    {
        var servicio = await _context.Servicios.FindAsync(id);
        if (servicio == null)
            return NotFound();

        servicio.Estado = nuevoEstado;
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }
    }
}
