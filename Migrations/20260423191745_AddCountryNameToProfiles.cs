using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataPersistentApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCountryNameToProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Profiles",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Gender",
                table: "Profiles",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CountryId",
                table: "Profiles",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "AgeGroup",
                table: "Profiles",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "CountryName",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_Age",
                table: "Profiles",
                column: "Age");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_AgeGroup",
                table: "Profiles",
                column: "AgeGroup");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_CountryId",
                table: "Profiles",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_CountryProbability",
                table: "Profiles",
                column: "CountryProbability");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_Gender",
                table: "Profiles",
                column: "Gender");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_GenderProbability",
                table: "Profiles",
                column: "GenderProbability");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_Name",
                table: "Profiles",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Profiles_Age",
                table: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_AgeGroup",
                table: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_CountryId",
                table: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_CountryProbability",
                table: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_Gender",
                table: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_GenderProbability",
                table: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_Name",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "CountryName",
                table: "Profiles");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Gender",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "CountryId",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "AgeGroup",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
