using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsCheckIn.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BranchName",
                table: "Branch",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "BranchType",
                table: "Branch",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "ParentBranchId",
                table: "Branch",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE Branch 
                SET BranchType = 1 
                WHERE IsSuperAdminBranch = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Branch_BranchType",
                table: "Branch",
                column: "BranchType");

            migrationBuilder.CreateIndex(
                name: "IX_Branch_ParentBranchId",
                table: "Branch",
                column: "ParentBranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Branch_Branch_ParentBranchId",
                table: "Branch",
                column: "ParentBranchId",
                principalTable: "Branch",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Branch_Branch_ParentBranchId",
                table: "Branch");

            migrationBuilder.DropIndex(
                name: "IX_Branch_BranchType",
                table: "Branch");

            migrationBuilder.DropIndex(
                name: "IX_Branch_ParentBranchId",
                table: "Branch");

            migrationBuilder.DropColumn(
                name: "BranchType",
                table: "Branch");

            migrationBuilder.DropColumn(
                name: "ParentBranchId",
                table: "Branch");

            migrationBuilder.AlterColumn<string>(
                name: "BranchName",
                table: "Branch",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
