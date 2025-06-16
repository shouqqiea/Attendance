using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsCheckIn.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchAndDatesToLeaveType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "LeaveTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "LeaveTypes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedDate",
                table: "LeaveTypes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_BranchId",
                table: "LeaveTypes",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveTypes_Branch_BranchId",
                table: "LeaveTypes",
                column: "BranchId",
                principalTable: "Branch",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveTypes_Branch_BranchId",
                table: "LeaveTypes");

            migrationBuilder.DropIndex(
                name: "IX_LeaveTypes_BranchId",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "DeletedDate",
                table: "LeaveTypes");
        }
    }
}
