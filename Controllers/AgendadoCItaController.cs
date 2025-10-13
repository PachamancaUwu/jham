using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using jhampro.Models;
using PayPalCheckoutSdk.Orders;
using PayPalHttp;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;

namespace jhampro.Controllers
{
    public class AgendadoCitaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PayPalClientFactory _payPalClientFactory;

        public AgendadoCitaController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _payPalClientFactory = new PayPalClientFactory(configuration);
        }

        [HttpGet]
        public IActionResult Agendado()
        {
            int? clienteId = HttpContext.Session.GetInt32("UsuarioId");

            var model = new AgendadoCitaViewModel
            {
                Abogados = _context.Usuarios
                    .Where(u => u.TipoUsuario == "Abogado")
                    .ToList(),
                Servicios = _context.Servicios
                    .Where(s => s.ClienteId == clienteId)
                    .Include(s => s.Pago)
                    .ToList()
            };
            return View(model);
        }

    [HttpPost]
        public IActionResult RegistrarCita(int AbogadoId, DateTime Fecha, string Hora)
        {
            int? clienteId = HttpContext.Session.GetInt32("UsuarioId");
            if (clienteId == null)
                return RedirectToAction("Login", "Login");

            // Convertir hora peruana a UTC antes de guardar (PostgreSQL exige UTC)
            // Parsear la hora tipo "09:00"
            var horaSplit = Hora.Split(':');
            int horaInt = int.Parse(horaSplit[0]);
            int minutoInt = int.Parse(horaSplit[1]);

            var fechaLocal = new DateTime(Fecha.Year, Fecha.Month, Fecha.Day, horaInt, minutoInt, 0, DateTimeKind.Unspecified);
            var peruTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            var fechaInicio = TimeZoneInfo.ConvertTimeToUtc(fechaLocal, peruTimeZone);
            var fechaFin = fechaInicio.AddHours(1);

            var servicio = new Servicio
            {
                Estado = "EnEspera",
                FechaInicio = fechaInicio,
                FechaFin = fechaFin,
                TipoServicio = "Cita",
                ClienteId = clienteId.Value
            };

            _context.Servicios.Add(servicio);
            _context.SaveChanges();

            var abogadoServicio = new AbogadoServicio
            {
                UsuarioId = AbogadoId,
                ServicioId = servicio.Id
            };
            _context.AbogadoServicio.Add(abogadoServicio);
            _context.SaveChanges();
            TempData["MensajeExito"] = "La cita fue agendada exitosamente.";
            return RedirectToAction("Agendado");
        }

        [HttpGet]
        public IActionResult EditarCita(int servicioId)
        {
            var servicio = _context.Servicios
                .Include(s => s.AbogadoServicios)
                .FirstOrDefault(s => s.Id == servicioId && s.Estado == "EnEspera");
            if (servicio == null)
                return NotFound();

            var abogados = _context.Usuarios.Where(u => u.TipoUsuario == "Abogado").ToList();
            ViewBag.Abogados = abogados;
            return View(servicio);
        }

        [HttpPost]
        public IActionResult EditarCita(int Id, int AbogadoId, DateTime Fecha, string Hora)
        {
            var servicio = _context.Servicios
                .Include(s => s.AbogadoServicios)
                .FirstOrDefault(s => s.Id == Id && s.Estado == "EnEspera");
            if (servicio == null)
                return NotFound();

            // Validar formato de hora
            int horaInt = 0, minutoInt = 0;
            if (!string.IsNullOrEmpty(Hora) && Hora.Contains(":"))
            {
                var horaSplit = Hora.Split(':');
                if (horaSplit.Length == 2)
                {
                    horaInt = int.Parse(horaSplit[0]);
                    minutoInt = int.Parse(horaSplit[1]);
                }
                else
                {
                    // Valor inválido, puedes manejar el error aquí
                    ModelState.AddModelError("Hora", "Hora inválida.");
                    // Recarga la vista con los datos actuales
                    var abogados = _context.Usuarios.Where(u => u.TipoUsuario == "Abogado").ToList();
                    ViewBag.Abogados = abogados;
                    return View(servicio);
                }
            }
            else
            {
                ModelState.AddModelError("Hora", "Debe seleccionar una hora.");
                var abogados = _context.Usuarios.Where(u => u.TipoUsuario == "Abogado").ToList();
                ViewBag.Abogados = abogados;
                return View(servicio);
            }

            var fechaLocal = new DateTime(Fecha.Year, Fecha.Month, Fecha.Day, horaInt, minutoInt, 0, DateTimeKind.Unspecified);
            var peruTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            var fechaInicio = TimeZoneInfo.ConvertTimeToUtc(fechaLocal, peruTimeZone);
            var fechaFin = fechaInicio.AddHours(1);

            servicio.FechaInicio = fechaInicio;
            servicio.FechaFin = fechaFin;

            // Actualizar abogado (eliminar y crear nuevo)
            var abogadoServicio = servicio.AbogadoServicios.FirstOrDefault();
            if (abogadoServicio != null)
            {
                _context.AbogadoServicio.Remove(abogadoServicio);
                _context.SaveChanges();
            }
            var nuevoAbogadoServicio = new AbogadoServicio
            {
                UsuarioId = AbogadoId,
                ServicioId = servicio.Id
            };
            _context.AbogadoServicio.Add(nuevoAbogadoServicio);

            _context.SaveChanges();
            TempData["MensajeExito"] = "Cita editada correctamente.";
            return RedirectToAction("Agendado");
        }

    public async Task<IActionResult> EditarEstado(int id)
    {
        var servicio = await _context.Servicios.FindAsync(id);
        if (servicio == null) return NotFound();

        return View(servicio);
    }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarEstado(int id, IFormCollection form)
        {
            var servicio = await _context.Servicios.FindAsync(id);
            if (servicio == null)
            {
                return NotFound();
            }

            var nuevoEstado = form["Estado"]; // Coincide con el name="Estado" del <select>

            if (string.IsNullOrEmpty(nuevoEstado))
            {
                ModelState.AddModelError("", "El estado no puede estar vacío.");
                return View(servicio);
            }

            servicio.Estado = nuevoEstado;
        
            try
            {
                await _context.SaveChangesAsync();
                return RedirectToAction("Citas", "Historial");
            }
            catch (Exception ex)
            {
                // Opcional: registrar el error
                ModelState.AddModelError("", "Error al guardar los cambios.");
                return View(servicio);
            }
    }

        [HttpPost]
        public IActionResult CancelarCita(int servicioId)
        {
            var servicio = _context.Servicios.FirstOrDefault(s => s.Id == servicioId && s.Estado == "EnEspera");
            if (servicio == null)
            {
                TempData["MensajeError"] = "No se pudo cancelar la cita.";
                return RedirectToAction("Agendado");
            }

            servicio.Estado = "Cancelado";
            _context.SaveChanges();

            TempData["MensajeExito"] = "La cita fue cancelada correctamente.";
            return RedirectToAction("Agendado");
        }

        [HttpPost]
        public async Task<IActionResult> PagarCita(int servicioId)
        {
            var servicio = _context.Servicios
                .Include(s => s.Cliente)
                .FirstOrDefault(s => s.Id == servicioId);

            if (servicio == null || servicio.Estado != "EnEspera")
            {
                return NotFound();
            }

            var request = new OrdersCreateRequest();
            request.Prefer("return=representation");
            request.RequestBody(new OrderRequest
            {
                CheckoutPaymentIntent = "CAPTURE",
                PurchaseUnits = new List<PurchaseUnitRequest>
                {
                    new PurchaseUnitRequest
                    {
                        AmountWithBreakdown = new AmountWithBreakdown
                        {
                            CurrencyCode = "USD",
                            Value = "50.00"
                        }
                    }
                },
                ApplicationContext = new ApplicationContext
                {
                    ReturnUrl = Url.Action("ConfirmarPago", "AgendadoCita", new { servicioId }, Request.Scheme),

                    CancelUrl = Url.Action("CancelarPago", "AgendadoCita", new { servicioId }, Request.Scheme)
                }

            });

            var client = _payPalClientFactory.CreateClient();
            var response = await client.Execute(request);
            var result = response.Result<Order>();

            var approvalLink = result.Links.FirstOrDefault(link => link.Rel == "approve")?.Href;

            return Redirect(approvalLink);
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmarPago(int servicioId, string token)
        {
            var client = _payPalClientFactory.CreateClient();

            var request = new OrdersCaptureRequest(token);
            request.RequestBody(new OrderActionRequest());

            var response = await client.Execute(request);
            var result = response.Result<Order>();

            if (result.Status == "COMPLETED")
            {
                var servicio = _context.Servicios.FirstOrDefault(s => s.Id == servicioId);
                if (servicio != null)
                {
                    servicio.Estado = "Pagado";
                    servicio.Pago = new Pago
                    {
                        FechaPago = servicio.FechaInicio,
                        Monto = 50.00m,
                        Metodo = "PayPal",
                        ServicioId = servicio.Id
                    };
                    _context.SaveChanges();
                }
                TempData["MensajeExito"] = "Pago realizado exitosamente.";
                return RedirectToAction("Agendado");
            }

            TempData["MensajeError"] = "Error al procesar el pago.";
            return RedirectToAction("Agendado");
        }

        [HttpGet]
        public IActionResult CancelarPago(int servicioId)
        {
            TempData["MensajeError"] = "El pago fue cancelado.";
            return RedirectToAction("Agendado");
        }

        [HttpGet]
        public IActionResult GenerarComprobante(int servicioId)
        {
            var servicio = _context.Servicios
                .Include(s => s.Cliente)
                .FirstOrDefault(s => s.Id == servicioId && s.Estado == "Pagado");

            if (servicio == null)
                return NotFound("Servicio no encontrado o aún no pagado.");

            using (var doc = new PdfDocument())
            {
                var page = doc.AddPage();
                var gfx = XGraphics.FromPdfPage(page);

                var fontTitle = new XFont("Arial", 18, XFontStyle.Bold);
                var fontText = new XFont("Arial", 12, XFontStyle.Regular);

                gfx.DrawString("COMPROBANTE DE PAGO", fontTitle, XBrushes.Black,
                    new XRect(0, 30, page.Width, 40), XStringFormats.TopCenter);

                int y = 100;
                int spacing = 30;

                gfx.DrawString($"Servicio ID: {servicio.Id}", fontText, XBrushes.Black, 50, y); y += spacing;
                gfx.DrawString($"Fecha de Pago: {servicio.FechaInicio:yyyy-MM-dd HH:mm}", fontText, XBrushes.Black, 50, y); y += spacing;
                gfx.DrawString($"Monto: $50.00", fontText, XBrushes.Black, 50, y); y += spacing;
                gfx.DrawString($"Metodo: PayPal", fontText, XBrushes.Black, 50, y); y += spacing;
                gfx.DrawString($"Cliente ID: {servicio.ClienteId}", fontText, XBrushes.Black, 50, y);

                using (var stream = new System.IO.MemoryStream())
                {
                    doc.Save(stream, false);
                    stream.Seek(0, System.IO.SeekOrigin.Begin);
                    return File(stream.ToArray(), "application/pdf", $"Comprobante_{servicio.Id}.pdf");
                }
            }
        }
        
        [HttpGet]
        public IActionResult MisEstadisticas(DateTime? desde, DateTime? hasta)
        {
            desde = desde.HasValue ? DateTime.SpecifyKind(desde.Value, DateTimeKind.Utc) : null;
            hasta = hasta.HasValue ? DateTime.SpecifyKind(hasta.Value, DateTimeKind.Utc).AddDays(1).AddSeconds(-1) : null;

            int? clienteId = HttpContext.Session.GetInt32("UsuarioId");
            if (clienteId == null)
                return RedirectToAction("Login", "Login");

            var query = _context.Servicios
                .Where(s => s.ClienteId == clienteId && s.TipoServicio == "Cita");

            if (desde.HasValue)
                query = query.Where(s => s.FechaInicio >= desde.Value);

            if (hasta.HasValue)
                query = query.Where(s => s.FechaInicio <= hasta.Value);

            var estadisticas = query
                .GroupBy(s => s.Estado)
                .Select(g => new
                {
                    Estado = g.Key,
                    Total = g.Count()
                })
                .ToDictionary(x => x.Estado, x => x.Total);

            ViewBag.Desde = desde;
            ViewBag.Hasta = hasta;

            return View("~/Views/AgendadoCita/Estadisticas.cshtml", estadisticas);
        }

        [HttpGet]
        public IActionResult Estadisticas(DateTime? desde, DateTime? hasta)
        {
            int? clienteId = HttpContext.Session.GetInt32("UsuarioId");
            if (clienteId == null)
                return RedirectToAction("Login", "Login");

            // Rango por defecto: últimos 30 días si no se envían fechas
            DateTime fechaInicio = desde ?? DateTime.UtcNow.AddDays(-30);
            DateTime fechaFin = hasta?.AddDays(1).AddSeconds(-1) ?? DateTime.UtcNow;

            // Guardar fechas para mostrarlas en los inputs
            ViewBag.Desde = fechaInicio;
            ViewBag.Hasta = fechaFin;

            // Consulta de estadísticas
            var servicios = _context.Servicios
                .Where(s => s.ClienteId == clienteId &&
                            s.FechaInicio >= fechaInicio &&
                            s.FechaInicio <= fechaFin)
                .ToList();

            var estadisticas = servicios
                .GroupBy(s => s.Estado)
                .ToDictionary(g => g.Key, g => g.Count());

            // Asegurar que siempre haya datos para cada estado
            string[] estados = { "EnEspera", "Cancelado", "Pagado" };
            foreach (var estado in estados)
            {
                if (!estadisticas.ContainsKey(estado))
                    estadisticas[estado] = 0;
            }

            return View(estadisticas);
        }
    }
}