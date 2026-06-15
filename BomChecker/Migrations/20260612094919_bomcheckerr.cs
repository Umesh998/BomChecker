using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BomChecker.Migrations
{
    /// <inheritdoc />
    public partial class bomcheckerr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BomReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ProcessedRows = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BomReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BomRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<int>(type: "int", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    OriginalDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    BestMatchPartNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BestMatchScore = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BomRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BomRows_BomReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "BomReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BomRowId = table.Column<int>(type: "int", nullable: false),
                    ColumnName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApiDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Package = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MslLevel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MountType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MatchScore = table.Column<double>(type: "float", nullable: false),
                    MatchVerdict = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Found = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartResults_BomRows_BomRowId",
                        column: x => x.BomRowId,
                        principalTable: "BomRows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BomRows_ReportId",
                table: "BomRows",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_PartResults_BomRowId",
                table: "PartResults",
                column: "BomRowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartResults");

            migrationBuilder.DropTable(
                name: "BomRows");

            migrationBuilder.DropTable(
                name: "BomReports");
        }
    }
}
