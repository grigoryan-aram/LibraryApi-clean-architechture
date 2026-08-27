using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryApi.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanDueDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                table: "Loans",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // The column default is 0001-01-01, which would make every loan
            // already on the books instantly and permanently overdue. Give the
            // existing rows the due date they would have been given at the
            // time: fourteen days after they were borrowed.
            //
            // Rows whose BorrowedAt is itself 0001-01-01 are left alone. They
            // predate the fix to AddLoanAsync that stopped dropping the borrow
            // date, so there is no honest due date to infer for them, and
            // inventing one would hide that the row is junk.
            migrationBuilder.Sql(
                """
                UPDATE Loans
                SET DueAt = DATEADD(day, 14, BorrowedAt)
                WHERE DueAt = '0001-01-01'
                  AND BorrowedAt > '0001-01-01';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "Loans");
        }
    }
}
