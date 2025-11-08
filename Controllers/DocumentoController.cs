using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using jhampro.Models;
using Microsoft.EntityFrameworkCore;
using Google.Cloud.Storage.V1;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace jhampro.Controllers
{
    [Authorize] // ✅ Requiere login por cookie (LoginController)
    public class DocumentoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly StorageClient _storageClient;

        public DocumentoController(ApplicationDbContext context, IConfiguration configuration, StorageClient storageClient)
        {
            _context = context;
            _configuration = configuration;
            _storageClient = storageClient;
        }

        // Vista principal
        public IActionResult Perfil() => View();

        // ✅ Ver documentos
        [Authorize] // Solo usuarios autenticados pueden ver documentos
        public async Task<IActionResult> Ver()
        {
            var documentos = await _context.Documentos.OrderByDescending(d => d.FechaSubida).ToListAsync();
            return View(documentos);
        }

        // ✅ Vista para subir documentos (solo administradores)
        [HttpGet]
        [Authorize]
        public IActionResult Gestionar()
        {
            var tipoUsuario = User.FindFirst("TipoUsuario")?.Value;
            if (tipoUsuario != "Administrador" && tipoUsuario != "Abogado")
                return Forbid(); // 403 si no es admin

            return View();
        }

        // ✅ Subida de documentos (solo administradores)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Gestionar(Documento model, IFormFile Archivo)
        {
            var tipoUsuario = User.FindFirst("TipoUsuario")?.Value;
            if (tipoUsuario != "Administrador" && tipoUsuario != "Abogado")
                return Forbid();

            if (Archivo == null || Archivo.Length == 0)
            {
                ModelState.AddModelError("Archivo", "Por favor, selecciona un archivo válido.");
                return View(model);
            }

            var firebaseBucketName = _configuration["Firebase:StorageBucketName"] ?? "jham-docs.firebasestorage.app";
            var uniqueFileName = $"{Guid.NewGuid()}_{Archivo.FileName}";
            var objectName = $"admin_documents/{uniqueFileName}";

            try
            {
                using (var stream = new MemoryStream())
                {
                    await Archivo.CopyToAsync(stream);
                    stream.Position = 0;

                    await _storageClient.UploadObjectAsync(firebaseBucketName, objectName, Archivo.ContentType, stream);
                }

                model.NombreArchivo = Archivo.FileName;
                model.RutaArchivo = objectName;
                model.FechaSubida = DateTime.UtcNow;
                model.ContentType = Archivo.ContentType;

                _context.Documentos.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Documento subido correctamente.";
                return RedirectToAction("Ver");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error al subir archivo: {ex.Message}");
                ModelState.AddModelError("Archivo", $"Error al subir el archivo: {ex.Message}");
                return View(model);
            }
        }

        // ✅ Descarga de documentos
        [Authorize]
        public async Task<IActionResult> DescargarSeguro(int id)
        {
            var tipoUsuario = User.FindFirst("TipoUsuario")?.Value;
            if (tipoUsuario != "Administrador" && tipoUsuario != "Abogado")
                return Forbid();

            var documento = await _context.Documentos.FindAsync(id);
            if (documento == null)
                return NotFound();

            var firebaseBucketName = _configuration["Firebase:StorageBucketName"] ?? "jham-docs.firebasestorage.app";

            using var memoryStream = new MemoryStream();
            await _storageClient.DownloadObjectAsync(firebaseBucketName, documento.RutaArchivo, memoryStream);
            memoryStream.Position = 0;

            string fileName = SanitizeFileNameForHeader(documento.NombreArchivo);
            string encodedFileName = WebUtility.UrlEncode(fileName);
            Response.Headers["Content-Disposition"] = $"attachment; filename*=UTF-8''{encodedFileName}";

            return File(memoryStream.ToArray(), documento.ContentType ?? "application/octet-stream");
        }

        // ✅ Eliminar documento
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Eliminar(int id)
        {
            var tipoUsuario = User.FindFirst("TipoUsuario")?.Value;
            if (tipoUsuario != "Administrador" && tipoUsuario != "Abogado")
                return Forbid();

            var documento = await _context.Documentos.FindAsync(id);
            if (documento == null)
                return NotFound();

            var firebaseBucketName = _configuration["Firebase:StorageBucketName"] ?? "jham-docs.firebasestorage.app";
            await _storageClient.DeleteObjectAsync(firebaseBucketName, documento.RutaArchivo);

            _context.Documentos.Remove(documento);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Documento eliminado correctamente.";
            return RedirectToAction("Ver");
        }

        // ✅ Actualizar documento
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Actualizar(int id)
        {
            var tipoUsuario = User.FindFirst("TipoUsuario")?.Value;
            if (tipoUsuario != "Administrador" && tipoUsuario != "Abogado")
                return Forbid();

            var documento = await _context.Documentos.FindAsync(id);
            return documento == null ? NotFound() : View(documento);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Actualizar(Documento model, IFormFile Archivo)
        {
            var tipoUsuario = User.FindFirst("TipoUsuario")?.Value;
            if (tipoUsuario != "Administrador" && tipoUsuario != "Abogado")
                return Forbid();

            var documento = await _context.Documentos.FindAsync(model.Id);
            if (documento == null)
                return NotFound();

            documento.Observacion = model.Observacion;
            documento.ServicioId = model.ServicioId;

            var firebaseBucketName = _configuration["Firebase:StorageBucketName"] ?? "jham-docs.firebasestorage.app";

            if (Archivo != null && Archivo.Length > 0)
            {
                var uniqueFileName = $"{Guid.NewGuid()}_{Archivo.FileName}";
                var objectName = $"admin_documents/{uniqueFileName}";

                using var stream = new MemoryStream();
                await Archivo.CopyToAsync(stream);
                stream.Position = 0;

                await _storageClient.UploadObjectAsync(firebaseBucketName, objectName, Archivo.ContentType, stream);

                if (!string.IsNullOrEmpty(documento.RutaArchivo))
                {
                    await _storageClient.DeleteObjectAsync(firebaseBucketName, documento.RutaArchivo);
                }

                documento.NombreArchivo = Archivo.FileName;
                documento.RutaArchivo = objectName;
                documento.ContentType = Archivo.ContentType;
                documento.FechaSubida = DateTime.UtcNow;
            }

            _context.Update(documento);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Documento actualizado correctamente.";
            return RedirectToAction("Ver");
        }

        // Limpieza del nombre de archivo
        private string SanitizeFileNameForHeader(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "downloaded_file";
            string sanitized = Regex.Replace(fileName, @"[\p{C}]", string.Empty);
            sanitized = sanitized.Replace("/", "_").Replace("\\", "_");
            return sanitized;
        }
    }
}