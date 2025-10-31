using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace jhampro.Models
{
    [Table("servicio")]
    public class Servicio
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Estado { get; set; } = "EnEspera";
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string TipoServicio { get; set; } = "Cita";  // "Cita" y "Caso"

        // Relación uno a muchos (Cliente a Servicio)
        public int ClienteId { get; set; }
        public Usuario Cliente { get; set; }

        // Relación muchos a muchos (Abogado a Servicio)
        public ICollection<AbogadoServicio> AbogadoServicios { get; set; }

        // Relación uno a uno (Pago)
        public Pago? Pago { get; set; }

        // Relación uno a muchos (Servicio a Retroalimentaciones)
        public ICollection<Retroalimentacion> Retroalimentaciones { get; set; } = new List<Retroalimentacion>();

        // Relación uno a muchos (Servicio a Documento)
        public ICollection<Documento> Documentos { get; set; }
    }
}