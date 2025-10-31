using System.ComponentModel.DataAnnotations;

namespace jhampro.Models.ViewModels
{
    public class ValoracionViewModel
    {
        [Required(ErrorMessage = "La calificación es requerida")]
        [Range(1, 5, ErrorMessage = "La calificación debe estar entre 1 y 5")]
        public int Calificacion { get; set; }

        public string Comentario { get; set; }
        
        public bool Publico { get; set; }
        
        [Required]
        public int ServicioId { get; set; }
    }
}
