using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jham.Migrations
{
    /// <inheritdoc />
    public partial class NuevaBDFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comentario",
                table: "servicio",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Comentario",
                table: "servicio");
        }
    }
}
