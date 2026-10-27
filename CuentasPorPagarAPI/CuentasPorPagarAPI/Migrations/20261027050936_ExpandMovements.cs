using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CuentasPorPagarAPI.Migrations
{
    /// <inheritdoc />
    public partial class ExpandMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "CancelledOn",
                table: "PurchaseInvoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CancelledOn",
                table: "Payments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresCheckDetails",
                table: "PaymentMethods",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CheckDetails",
                columns: table => new
                {
                    PaymentId = table.Column<int>(type: "integer", nullable: false),
                    BankName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CheckNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ClearedOn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckDetails", x => x.PaymentId);
                    table.ForeignKey(
                        name: "FK_CheckDetails_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Series = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CancelledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierAdjustments", x => x.Id);
                    table.CheckConstraint("CK_SupplierAdjustment_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_SupplierAdjustments_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierAdjustments_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdjustmentAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierAdjustmentId = table.Column<int>(type: "integer", nullable: false),
                    PurchaseInvoiceId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjustmentAllocations", x => x.Id);
                    table.CheckConstraint("CK_AdjustmentAllocation_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_AdjustmentAllocations_PurchaseInvoices_PurchaseInvoiceId",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdjustmentAllocations_SupplierAdjustments_SupplierAdjustmen~",
                        column: x => x.SupplierAdjustmentId,
                        principalTable: "SupplierAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: 1,
                column: "RequiresCheckDetails",
                value: false);

            migrationBuilder.UpdateData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: 2,
                column: "RequiresCheckDetails",
                value: false);

            migrationBuilder.UpdateData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "RequiresCheckDetails",
                value: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentAllocations_PurchaseInvoiceId",
                table: "AdjustmentAllocations",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentAllocations_SupplierAdjustmentId_PurchaseInvoiceId",
                table: "AdjustmentAllocations",
                columns: new[] { "SupplierAdjustmentId", "PurchaseInvoiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckDetails_BankName_CheckNumber",
                table: "CheckDetails",
                columns: new[] { "BankName", "CheckNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdjustments_RecordedByUserId",
                table: "SupplierAdjustments",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdjustments_SupplierId_Date",
                table: "SupplierAdjustments",
                columns: new[] { "SupplierId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdjustments_SupplierId_Kind_Series_Number",
                table: "SupplierAdjustments",
                columns: new[] { "SupplierId", "Kind", "Series", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdjustmentAllocations");

            migrationBuilder.DropTable(
                name: "CheckDetails");

            migrationBuilder.DropTable(
                name: "SupplierAdjustments");

            migrationBuilder.DropColumn(
                name: "CancelledOn",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "CancelledOn",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RequiresCheckDetails",
                table: "PaymentMethods");
        }
    }
}
