using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiveCenterManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreservePriorDiveHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PriorLastDiveDate",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriorLoggedDives",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.Sql("UPDATE Customers SET PriorLoggedDives = MAX(0, TotalLoggedDives - (SELECT COUNT(*) FROM DiverDiveLogs WHERE CustomerId = Customers.Id)), PriorLastDiveDate = CASE WHEN LastDiveDate > COALESCE((SELECT MAX(DiveLogs.Date) FROM DiverDiveLogs JOIN DiveLogs ON DiveLogs.Id = DiverDiveLogs.DiveLogId WHERE DiverDiveLogs.CustomerId = Customers.Id), '') THEN LastDiveDate ELSE NULL END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriorLastDiveDate",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PriorLoggedDives",
                table: "Customers");
        }
    }
}
