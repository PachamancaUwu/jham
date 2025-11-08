using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using jhampro.Models;

namespace jhampro.Controllers
{
    [Authorize]
    public class CasoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.Extensions.Logging.ILogger<CasoController> _logger;

        public CasoController(ApplicationDbContext context, Microsoft.Extensions.Logging.ILogger<CasoController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /caso/debuglist  -> endpoint temporal para verificar que las retroalimentaciones se guardan
        [HttpGet("caso/debuglist")]
        public IActionResult DebugList(int take = 10)
        {
            var list = _context.Set<Retroalimentacion>()
                .OrderByDescending(r => r.Id)
                .Take(take)
                .Select(r => new {
                    r.Id,
                    r.ServicioId,
                    r.Calificacion,
                    ComentarioLen = r.Comentario != null ? r.Comentario.Length : 0,
                    r.Publico,
                    r.Fecha
                })
                .ToList();

            return Json(list);
        }

        // GET: /caso/{id}/valoracion
        [HttpGet("caso/{id}/valoracion")]
        public IActionResult Valoracion(int id)
        {
            var clienteIdClaim = User.FindFirst("UsuarioId");
            if (clienteIdClaim == null)
            {
                return RedirectToAction("Login", "Login");
            }

            int clienteId = int.Parse(clienteIdClaim.Value);
            var servicio = _context.Servicios
                .FirstOrDefault(s => s.Id == id && s.TipoServicio == "Cita");
            if (servicio == null) return NotFound();

            // Usar el ViewModel en lugar del modelo de entidad
            var model = new jhampro.Models.ViewModels.ValoracionViewModel
            {
                ServicioId = servicio.Id
            };

            return View(model);
        }

        // POST: /caso/{id}/valoracion
        [HttpPost]
        [Route("caso/{id}/valoracion")]
        [ValidateAntiForgeryToken]
        public IActionResult Valoracion(int id, [Bind("Calificacion,Comentario,Publico")] jhampro.Models.ViewModels.ValoracionViewModel model)
        {
            // Obtener el UsuarioId desde las claims, no confiar en datos enviados por el cliente
            var clienteIdClaim = User.FindFirst("UsuarioId");
            if (clienteIdClaim == null)
            {
                return RedirectToAction("Login", "Login");
            }

            int clienteId = int.Parse(clienteIdClaim.Value);

            // Validar que el servicio exista y coincida con la ruta
            var servicio = _context.Servicios.FirstOrDefault(s => s.Id == id && s.TipoServicio == "Cita");
            if (servicio == null) return NotFound();

            // Asignar manualmente el ServicioId, ignorando cualquier intento de over-posting
            model.ServicioId = servicio.Id;

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                _logger.LogWarning("ModelState inválido en Valoracion: {errors}", errors);
                TempData["Debug"] = "ModelState inválido: " + errors;
                return View(model);
            }

            // Crear la entidad Retroalimentacion usando solo los campos permitidos
            var retro = new Retroalimentacion
            {
                ServicioId = servicio.Id,          // nunca confiar en datos del cliente
                Calificacion = model.Calificacion,
                Comentario = model.Comentario,
                Publico = model.Publico,
                Fecha = DateTime.UtcNow,           // fecha asignada por el servidor
                Servicio = servicio
            };

            try
            {
                _context.Set<Retroalimentacion>().Add(retro);
                _context.SaveChanges();

                _logger.LogInformation("Retroalimentacion guardada. IdServicio={id}, Calificacion={cal}", servicio.Id, retro.Calificacion);
                TempData["MensajeExito"] = "Gracias por tu valoración.";
                TempData["Debug"] = "Guardado OK";
                return RedirectToAction("Agendado", "AgendadoCita");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar retroalimentacion");
                ModelState.AddModelError("", "Ocurrió un error al guardar la valoración: " + ex.Message);
                TempData["Debug"] = "Excepción: " + ex.Message;

                // Devolver solo los campos permitidos
                var vm = new jhampro.Models.ViewModels.ValoracionViewModel
                {
                    ServicioId = servicio.Id,
                    Calificacion = model.Calificacion,
                    Comentario = model.Comentario,
                    Publico = model.Publico
                };
                return View(vm);
            }
        }
    }
}
