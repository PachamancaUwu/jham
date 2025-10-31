using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jham.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleRetroalimentaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_retroalimentacion_servicio_ServicioId",
                table: "retroalimentacion");

            migrationBuilder.DropIndex(
                name: "IX_retroalimentacion_ServicioId",
                table: "retroalimentacion");

            migrationBuilder.CreateIndex(
                name: "IX_retroalimentacion_ServicioId",
                table: "retroalimentacion",
                column: "ServicioId");

            migrationBuilder.AddForeignKey(
                name: "FK_retroalimentacion_servicio_ServicioId",
                table: "retroalimentacion",
                column: "ServicioId",
                principalTable: "servicio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_retroalimentacion_servicio_ServicioId",
                table: "retroalimentacion");

            migrationBuilder.DropIndex(
                name: "IX_retroalimentacion_ServicioId",
                table: "retroalimentacion");

            migrationBuilder.CreateIndex(
                name: "IX_retroalimentacion_ServicioId",
                table: "retroalimentacion",
                column: "ServicioId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_retroalimentacion_servicio_ServicioId",
                table: "retroalimentacion",
                column: "ServicioId",
                principalTable: "servicio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
