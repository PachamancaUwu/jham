using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace jhampro.Models
{
    [Table("documento")]
    public class Documento
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string NombreArchivo { get; set; }

        public string RutaArchivo { get; set; }

        public DateTime FechaSubida { get; set; }

        public string ContentType { get; set; }

        public string? Observacion { get; set; }

        public int ServicioId { get; set; }
        public Servicio Servicio { get; set; }

        // Constructor para asignar fecha por defecto
        public Documento()
        {
            FechaSubida = DateTime.UtcNow;
        }
    }
}
