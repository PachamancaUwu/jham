using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using jhampro.Models;
using Microsoft.EntityFrameworkCore;

namespace jhampro.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            // Obtener retroalimentaciones públicas con servicio y cliente
            var todasLasValoraciones = await _context.Retroalimentaciones
                .Where(r => r.Publico == true)
                .OrderByDescending(r => r.Fecha)
                .ToListAsync();

            // Cargar las relaciones manualmente
            foreach (var valoracion in todasLasValoraciones)
            {
                if (valoracion.ServicioId > 0)
                {
                    valoracion.Servicio = await _context.Servicios
                        .Include(s => s.Cliente)
                        .FirstOrDefaultAsync(s => s.Id == valoracion.ServicioId);
                }
            }

            // Filtrar para obtener solo UN comentario por usuario (ClienteId único)
            var valoracionesUnicas = todasLasValoraciones
                .Where(r => r.Servicio?.Cliente != null)
                .GroupBy(r => r.Servicio.ClienteId)
                .Select(g => g.First()) // Tomar el primer comentario de cada usuario
                .Take(3)
                .ToList();

            return View(valoracionesUnicas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar las valoraciones");
            return View(new List<Retroalimentacion>());
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}