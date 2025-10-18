using Microsoft.AspNetCore.Mvc;
using jhampro.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
// using Amazon.S3; // Comentar si ya no usas S3 para nada en este controlador
// using Amazon.S3.Transfer; // Comentar si ya no usas S3 para nada en este controlador

using Google.Cloud.Storage.V1; // Necesitas este using para Firebase Storage
using System.IO;
using System.Threading.Tasks;
using System; // Necesario para Guid
using System.Net.Mime; // Necesario para ContentDisposition
using System.Linq;
using System.Text.RegularExpressions;
using System.Text; // Para Encoding
using System.Net; // Para WebUtility

namespace jhampro.Controllers
{
    public class DocumentoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        // private readonly IAmazonS3 _s3Client; // Si ya no usas S3 en este controlador, puedes quitar esto.

        private readonly StorageClient _storageClient;
        // private readonly string _bucketName; // Esta variable no se usa y se elimina.


        // Constructor
        public DocumentoController(ApplicationDbContext context, IConfiguration configuration, /* IAmazonS3 s3Client, */ StorageClient storageClient)
        {
            _context = context;
            _configuration = configuration;
            // _s3Client = s3Client; // Si quitas IAmazonS3 del controlador, quita también esta línea.
            _storageClient = storageClient;
        }

        // Vista Perfil con acceso a Ver y Gestionar
        public IActionResult Perfil()
        {
            return View();
        }

        // ✅ Ver documentos
        public async Task<IActionResult> Ver()
        {
            var documentos = await _context.Documentos.OrderByDescending(d => d.FechaSubida).ToListAsync();
            return View(documentos);
        }

        // ✅ Vista para subir documentos (GET)
        public IActionResult Gestionar()
        {
            return View();
        }

        // ✅ Subida de documentos (POST) - Modificado para Firebase Storage
        [HttpPost]
        public async Task<IActionResult> Gestionar(Documento model, IFormFile Archivo)
        {
            // Aquí debes implementar la lógica para asegurar que solo un administrador puede subir archivos.
            // Ejemplo: if (!User.Identity.IsAuthenticated || !User.IsInRole("Admin")) { return Forbid(); }

            if (Archivo == null || Archivo.Length == 0)
            {
                ModelState.AddModelError("Archivo", "Por favor, selecciona un archivo válido.");
                return View(model);
            }

            var firebaseBucketName = _configuration["Firebase:StorageBucketName"] ?? "jham-docs.firebasestorage.app"; // Obtiene de config o usa fallback

            // Generar un nombre único para el archivo y la ruta en Firebase Storage
            var uniqueFileName = $"{Guid.NewGuid()}_{Archivo.FileName}";
            var objectName = $"admin_documents/{uniqueFileName}"; // Ruta completa en Firebase Storage

            string? fileStoragePath = null; // Guardará el 'objectName'

            try
            {
                using (var newMemoryStream = new MemoryStream())
                {
                    await Archivo.CopyToAsync(newMemoryStream);
                    newMemoryStream.Position = 0; // Reinicia la posición del stream

                    // Opciones de subida para Firebase Storage
                    var uploadOptions = new UploadObjectOptions
                    {
                        //PredefinedObjectAcl = PredefinedObjectAcl.BucketOwnerFullControl,
                    };

                    // Subir el archivo a Firebase Cloud Storage
                    var uploadedObject = await _storageClient.UploadObjectAsync(
                        bucket: firebaseBucketName,
                        objectName: objectName,
                        contentType: Archivo.ContentType,
                        source: newMemoryStream,
                        options: uploadOptions
                    );

                    fileStoragePath = objectName; // ¡Guardamos el objectName completo en la base de datos!
                }

                // Guardar metadatos en la base de datos local
                model.NombreArchivo = Archivo.FileName;
                model.RutaArchivo = fileStoragePath; // Ruta del objeto en Firebase Storage
                model.FechaSubida = DateTime.UtcNow;
                model.ContentType = Archivo.ContentType; // **Asumiendo que has añadido ContentType a tu modelo Documento**

                _context.Documentos.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Documento subido correctamente a Firebase Storage.";
                return RedirectToAction("Ver");
            }
            catch (Google.GoogleApiException gapiEx)
            {
                Console.Error.WriteLine($"Error de Google API al subir: {gapiEx.Message}");
                if (gapiEx.Error != null)
                {
                    Console.Error.WriteLine($"Detalles del error: {gapiEx.Error.Code} - {gapiEx.Error.Message}");
                }
                ModelState.AddModelError("Archivo", $"Error al subir el archivo: {gapiEx.Message}");
                return View(model);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error general al subir el archivo: {ex.Message}");
                ModelState.AddModelError("Archivo", $"Error general al subir el archivo: {ex.Message}");
                return View(model);
            }
        }

        // ✅ Método para descargar/ver documento mediante Proxy Seguro
        // Este método será llamado desde tu interfaz de usuario
        public async Task<IActionResult> DescargarSeguro(int id)
        {

            // Aquí debes implementar la lógica para asegurar que solo un administrador
            // AUTENTICADO y AUTORIZADO puede descargar el archivo.
            // Ejemplo:
            // if (!User.Identity.IsAuthenticated || !User.IsInRole("Admin"))
            // {
            //     return Forbid(); // HTTP 403 Forbidden - O redirige a la página de login
            // }

            var documento = await _context.Documentos.FindAsync(id);
            if (documento == null)
            {
                return NotFound();
            }

            var firebaseBucketName = _configuration["Firebase:StorageBucketName"] ?? "jham-docs.firebasestorage.app";
            var objectNameInStorage = documento.RutaArchivo; // Esto contiene el 'objectName' de Firebase Storage

            if (string.IsNullOrEmpty(objectNameInStorage))
            {
                return BadRequest("La ruta del archivo en Storage no es válida.");
            }

            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    // Descarga el archivo de Firebase Storage a tu servidor
                    await _storageClient.DownloadObjectAsync(
                        bucket: firebaseBucketName,
                        objectName: objectNameInStorage,
                        destination: memoryStream
                    );

                    memoryStream.Position = 0; // Reinicia la posición del stream

                    // Determina el Content-Type. Si lo guardaste en DB, úsalo.
                    string contentType = documento.ContentType ?? "application/octet-stream"; // Usa el guardado o un default

                    // 1. Sanitiza el nombre del archivo para eliminar cualquier carácter de control o no válido.
                    //    Mantenemos el SanitizeFileNameForHeader porque es una buena práctica para limpiar chars invisibles
                    //    que no son tildes.
                    string originalFileName = documento.NombreArchivo;
                    string sanitizedFileName = SanitizeFileNameForHeader(originalFileName);

                    // 2. Codifica el nombre del archivo usando URL encoding y RFC 5987.
                    //    Esta es la forma correcta de manejar caracteres no-ASCII en Content-Disposition.
                    //    Ejemplo: filename*=utf-8''nombre%20con%C3%B1.pdf
                    string encodedFileName = WebUtility.UrlEncode(sanitizedFileName);

                    // Construye el Content-Disposition manualmente para asegurar la codificación RFC 5987.
                    // Esto es más robusto que depender de ContentDisposition.ToString() para caracteres especiales.
                    string contentDispositionHeader = $"attachment; filename*=UTF-8''{encodedFileName}";

                    // Use el indexador para setear el header (Add puede lanzar si ya existe)
                    Response.Headers["Content-Disposition"] = contentDispositionHeader;

                    // Envía el archivo al navegador del usuario
                    return File(memoryStream.ToArray(), contentType);
                }
            }
            catch (Google.GoogleApiException gapiEx)
            {
                Console.Error.WriteLine($"Error de Google API al descargar: {gapiEx.Message}");
                if (gapiEx.Error != null)
                {
                    Console.Error.WriteLine($"Detalles del error: {gapiEx.Error.Code} - {gapiEx.Error.Message}");
                }
                TempData["Error"] = $"Error al descargar el documento: {gapiEx.Message}";
                return RedirectToAction("Ver");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error general al descargar el archivo: {ex.Message}");
                TempData["Error"] = $"Error general al descargar el documento: {ex.Message}";
                return RedirectToAction("Ver");
            }
        }

        private string SanitizeFileNameForHeader(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "downloaded_file";
            }

            // Eliminar caracteres de control (incluyendo 0x000D, 0x000A, etc.)
            string sanitized = Regex.Replace(fileName, @"[\p{C}]", string.Empty);

            // Reemplazar caracteres no permitidos en nombres de archivo (Windows/Linux)
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c.ToString(), "_");
            }
            sanitized = sanitized.Replace(":", "_").Replace("/", "_").Replace("\\", "_");

            // Recortar espacios y manejar casos de nombres vacíos
            sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();
            if (sanitized.StartsWith("."))
            {
                sanitized = sanitized.Substring(1);
            }
            if (string.IsNullOrEmpty(sanitized))
            {
                return "downloaded_file";
            }

            // Opcional: Limitar la longitud (si es un nombre muy largo)
            if (sanitized.Length > 200)
            {
                int lastDotIndex = sanitized.LastIndexOf('.');
                if (lastDotIndex != -1 && lastDotIndex > (sanitized.Length - 10))
                {
                    string extension = sanitized.Substring(lastDotIndex);
                    sanitized = sanitized.Substring(0, 200 - extension.Length) + extension;
                }
                else
                {
                    sanitized = sanitized.Substring(0, 200);
                }
            }

            return sanitized;
        }


        // ✅ Eliminar documento - Adaptado para Firebase Storage
        [HttpPost]
        public async Task<IActionResult> Eliminar(int id)
        {
            // Aquí debes implementar la lógica para asegurar que solo un administrador
            // AUTENTICADO y AUTORIZADO puede eliminar el archivo.
            // Ejemplo:
            // if (!User.Identity.IsAuthenticated || !User.IsInRole("Admin")) { return Forbid(); }

            var documento = await _context.Documentos.FindAsync(id);
            if (documento == null)
            {
                return NotFound();
            }

            var firebaseBucketName = _configuration["Firebase:StorageBucketName"] ?? "jham-docs.firebasestorage.app";
            var objectNameToDelete = documento.RutaArchivo; // Esto contiene el 'objectName' de Firebase Storage

            if (string.IsNullOrEmpty(objectNameToDelete))
            {
                return BadRequest("La ruta del archivo en Storage no es válida para eliminar.");
            }

            try
            {
                // Eliminar del bucket de Firebase Storage
                await _storageClient.DeleteObjectAsync(firebaseBucketName, objectNameToDelete);

                // Eliminar de la base de datos
                _context.Documentos.Remove(documento);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Documento eliminado correctamente de Firebase Storage.";
                return RedirectToAction("Ver");
            }
            catch (Google.GoogleApiException gapiEx)
            {
                Console.Error.WriteLine($"Error de Google API al eliminar: {gapiEx.Message}");
                if (gapiEx.Error != null)
                {
                    Console.Error.WriteLine($"Detalles del error: {gapiEx.Error.Code} - {gapiEx.Error.Message}");
                }
                TempData["Error"] = $"Error al eliminar el documento de Firebase Storage: {gapiEx.Message}";
                return RedirectToAction("Ver");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error general al eliminar el archivo: {ex.Message}");
                TempData["Error"] = $"Error general al eliminar el documento: {ex.Message}";
                return RedirectToAction("Ver");
            }
        }

        // GET: Mostrar formulario de actualización
        public async Task<IActionResult> Actualizar(int id)
        {
            var documento = await _context.Documentos.FindAsync(id);
            if (documento == null)
            {
                return NotFound();
            }

            return View(documento);
        }

        // POST: Procesar actualización del documento (reemplazo opcional de archivo)
        [HttpPost]
        public async Task<IActionResult> Actualizar(Documento model, IFormFile Archivo)
        {
            // **IMPORTANTE: AUTORIZACIÓN** - aplicar según necesidad

            var documento = await _context.Documentos.FindAsync(model.Id);
            if (documento == null)
            {
                return NotFound();
            }

            // Actualizar campos editables
            documento.Observacion = model.Observacion;
            documento.ServicioId = model.ServicioId;

            var firebaseBucketName = _configuration["Firebase:StorageBucketName"] ?? "jham-docs.firebasestorage.app";

            try
            {
                if (Archivo != null && Archivo.Length > 0)
                {
                    // Subir nuevo archivo
                    var uniqueFileName = $"{Guid.NewGuid()}_{Archivo.FileName}";
                    var objectName = $"admin_documents/{uniqueFileName}";
                    string? fileStoragePath = null; // Guardará el 'objectName'

                    using (var newMemoryStream = new MemoryStream())
                    {
                        await Archivo.CopyToAsync(newMemoryStream);
                        newMemoryStream.Position = 0;

                        await _storageClient.UploadObjectAsync(
                            bucket: firebaseBucketName,
                            objectName: objectName,
                            contentType: Archivo.ContentType,
                            source: newMemoryStream
                        );
                        fileStoragePath = objectName;
                    }

                    // Intentar eliminar el archivo anterior en Storage (si existe)
                    if (!string.IsNullOrEmpty(documento.RutaArchivo))
                    {
                        try
                        {
                            await _storageClient.DeleteObjectAsync(firebaseBucketName, documento.RutaArchivo);
                        }
                        catch (Exception exDel)
                        {
                            // No bloqueamos la actualización si la eliminación falla; simplemente lo registramos
                            Console.Error.WriteLine($"No se pudo eliminar el objeto antiguo de Storage: {exDel.Message}");
                        }
                    }

                    // Actualizar metadatos en la entidad
                    documento.NombreArchivo = Archivo.FileName;
                    documento.RutaArchivo = fileStoragePath;
                    documento.ContentType = Archivo.ContentType;
                    documento.FechaSubida = DateTime.UtcNow;
                }

                _context.Documentos.Update(documento);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Documento actualizado correctamente.";
                // Después de actualizar, redirigimos a la lista para que el usuario pueda descargar manualmente
                return RedirectToAction("Ver");
            }
            catch (Google.GoogleApiException gapiEx)
            {
                Console.Error.WriteLine($"Error de Google API al actualizar: {gapiEx.Message}");
                ModelState.AddModelError("Archivo", $"Error al procesar el archivo: {gapiEx.Message}");
                return View(documento);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error general al actualizar el archivo: {ex.Message}");
                ModelState.AddModelError("Archivo", $"Error general al actualizar el documento: {ex.Message}");
                return View(documento);
            }
        }
    }
}