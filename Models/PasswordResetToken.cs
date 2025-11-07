using System;
using System.ComponentModel.DataAnnotations;

namespace jhampro.Models
{
    public class PasswordResetToken
    {
        [Key]
        public int Id { get; set; }

        public int UsuarioId { get; set; }

    // Token (GUID as string)
    public string? Token { get; set; }

        public DateTime Expiration { get; set; }

        public bool Used { get; set; }
    }
}
