using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsCheckIn.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchAccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSuperAdminBranch",
                table: "Branch",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSuperAdminBranch",
                table: "Branch");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "AspNetUsers");
        }
    }
}
