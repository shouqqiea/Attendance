using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LetsCheckIn.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusTypeAndUpdateLeaveRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Create StatusTypes table
            migrationBuilder.CreateTable(
                name: "StatusTypes",
                columns: table => new
                {
                    StatusId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatusName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusTypes", x => x.StatusId);
                });

            // Step 2: Seed StatusTypes
            migrationBuilder.InsertData(
                table: "StatusTypes",
                columns: new[] { "StatusId", "StatusName" },
                values: new object[,]
                {
                    { 1, "pending" },
                    { 2, "approved" },
                    { 3, "rejected" },
                    { 4, "emergency" },
                    { 5, "cancelled" }
                });

            // Step 3: Add new nullable columns to LeaveRequests
            migrationBuilder.AddColumn<string>(
                name: "LeaveReason",
                table: "LeaveRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedReason",
                table: "LeaveRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int?>(
                name: "TempStatusId",
                table: "LeaveRequests",
                type: "int",
                nullable: true);

            // Step 4: Copy data to new columns
            migrationBuilder.Sql(@"
                UPDATE lr
                SET lr.TempStatusId = st.StatusId,
                    lr.LeaveReason = lr.ManagerRemark
                FROM LeaveRequests lr
                CROSS APPLY (
                    SELECT TOP 1 StatusId
                    FROM StatusTypes st
                    WHERE LOWER(st.StatusName) = LOWER(lr.Status)
                ) st");

            // Step 5: Add non-nullable StatusId column
            migrationBuilder.AddColumn<int>(
                name: "StatusId",
                table: "LeaveRequests",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Step 6: Copy data from temporary column
            migrationBuilder.Sql(@"
                UPDATE LeaveRequests
                SET StatusId = ISNULL(TempStatusId, 1)");

            // Step 7: Drop temporary and old columns
            migrationBuilder.DropColumn(
                name: "TempStatusId",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "ManagerRemark",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LeaveRequests");

            // Step 8: Add foreign key constraint
            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_StatusId",
                table: "LeaveRequests",
                column: "StatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_StatusTypes_StatusId",
                table: "LeaveRequests",
                column: "StatusId",
                principalTable: "StatusTypes",
                principalColumn: "StatusId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove foreign key
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_StatusTypes_StatusId",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_StatusId",
                table: "LeaveRequests");

            // Add back old columns
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "LeaveRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "pending");

            migrationBuilder.AddColumn<string>(
                name: "ManagerRemark",
                table: "LeaveRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Copy data back
            migrationBuilder.Sql(@"
                UPDATE lr
                SET lr.Status = st.StatusName,
                    lr.ManagerRemark = lr.LeaveReason
                FROM LeaveRequests lr
                INNER JOIN StatusTypes st ON lr.StatusId = st.StatusId");

            // Drop new columns
            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "LeaveReason",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "RejectedReason",
                table: "LeaveRequests");

            // Drop StatusTypes table
            migrationBuilder.DropTable(
                name: "StatusTypes");
        }
    }
}
