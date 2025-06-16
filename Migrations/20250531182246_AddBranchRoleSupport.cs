using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsCheckIn.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchRoleSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DynamicRoles_Branch_BranchId",
                table: "DynamicRoles");

            migrationBuilder.DropIndex(
                name: "IX_DynamicRoles_BranchId",
                table: "DynamicRoles");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "DynamicRoles");

            migrationBuilder.CreateTable(
                name: "BranchRoles",
                columns: table => new
                {
                    BranchRoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    AssignedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchRoles", x => x.BranchRoleId);
                    table.ForeignKey(
                        name: "FK_BranchRoles_Branch_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branch",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BranchRoles_DynamicRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "DynamicRoles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BranchRoles_BranchId_RoleId",
                table: "BranchRoles",
                columns: new[] { "BranchId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchRoles_RoleId",
                table: "BranchRoles",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchRoles");

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "DynamicRoles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DynamicRoles_BranchId",
                table: "DynamicRoles",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_DynamicRoles_Branch_BranchId",
                table: "DynamicRoles",
                column: "BranchId",
                principalTable: "Branch",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
