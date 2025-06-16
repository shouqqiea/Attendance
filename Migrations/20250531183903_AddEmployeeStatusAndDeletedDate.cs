using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsCheckIn.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeStatusAndDeletedDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "Employee",
                newName: "Status");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedDate",
                table: "Employee",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedDate",
                table: "Employee");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Employee",
                newName: "IsActive");
        }
    }
}
