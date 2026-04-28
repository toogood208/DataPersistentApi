using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataPersistentApi.Migrations
{
    /// <inheritdoc />
    public partial class AlignUserSchemaAndCookieSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Users",
                newName: "LastLoginAt");

            migrationBuilder.RenameColumn(
                name: "GitHubLogin",
                table: "Users",
                newName: "Username");

            migrationBuilder.RenameIndex(
                name: "IX_Users_GitHubLogin",
                table: "Users",
                newName: "IX_Users_Username");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Username",
                table: "Users",
                newName: "GitHubLogin");

            migrationBuilder.RenameColumn(
                name: "LastLoginAt",
                table: "Users",
                newName: "UpdatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Username",
                table: "Users",
                newName: "IX_Users_GitHubLogin");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Users",
                type: "varchar(255)",
                nullable: true);
        }
    }
}
