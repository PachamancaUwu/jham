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
            // **IMPORTANTE: AUTORIZACIÓN**
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

            string fileStoragePath = null; // Guardará el 'objectName'

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
            // **IMPORTANTE: AUTORIZACIÓN**
            // Aquí debes implementar la lógica para asegurar que solo un administrador
            // AUTENTICADO y AUTORIZADO puede descargar el archivo.
            // Ejemplo:
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Admin"))
            {
                return Forbid(); // HTTP 403 Forbidden - O redirige a la página de login
            }

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

                    // Configura el encabezado Content-Disposition para forzar la descarga
                    var contentDisposition = new ContentDisposition
                    {
                        FileName = documento.NombreArchivo, // Nombre original del archivo para la descarga
                        Inline = false // Esto fuerza la descarga en lugar de intentar mostrarlo en el navegador
                    };
                    Response.Headers.Add("Content-Disposition", contentDisposition.ToString());

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


        // ✅ Eliminar documento - Adaptado para Firebase Storage
        [HttpPost]
        public async Task<IActionResult> Eliminar(int id)
        {
            // **IMPORTANTE: AUTORIZACIÓN**
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
    }
}