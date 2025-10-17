using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;


namespace jhampro.Models
{
    [Table("usuario")]
    public class Usuario
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; } // Clave primaria

        [Required(ErrorMessage = "Los nombres son obligatorios.")]
        public string Nombres { get; set; }

        [Required(ErrorMessage = "Los apellidos son obligatorios.")]
        public string Apellidos { get; set; }

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
        public string Correo { get; set; } // Correo electrónico

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [MinLength(6)]
        public string Contrasena { get; set; } // Contraseña del usuario

        [Required(ErrorMessage = "El número de celular es obligatorio.")]
        [RegularExpression(@"^\d{9}$", ErrorMessage = "El número de celular debe tener exactamente 9 dígitos.")]
        [Display(Name = "Celular")]
        public string Celular { get; set; } // Número de celular

        [Required(ErrorMessage = "El DNI es obligatorio.")]
        [RegularExpression(@"^\d{8}$", ErrorMessage = "El DNI debe tener 8 dígitos.")]
        public string Dni { get; set; } // DNI del usuario

        [Required(ErrorMessage = "Debe seleccionar un tipo de usuario.")]
        public string TipoUsuario { get; set; } = "Cliente";  //La creación de abogado se hace manualmente

        // Relación uno a muchos (Cliente a Servicio)
        public ICollection<Servicio> ServiciosComoCliente { get; set; } = new List<Servicio>();

        // Relación muchos a muchos (Abogado a Servicio)
        [JsonIgnore]
        public ICollection<AbogadoServicio> AbogadoServicios { get; set; } = new List<AbogadoServicio>();

        public string Especialidad { get; set; } = "ClienteNormal";
    }
}
