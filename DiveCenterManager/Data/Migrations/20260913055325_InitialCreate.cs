using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiveCenterManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Boats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    BirthDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Nationality = table.Column<string>(type: "TEXT", nullable: false),
                    CertificationAgency = table.Column<int>(type: "INTEGER", nullable: true),
                    CertificationLevel = table.Column<string>(type: "TEXT", nullable: true),
                    CertificationNumber = table.Column<string>(type: "TEXT", nullable: true),
                    LastDiveDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalLoggedDives = table.Column<int>(type: "INTEGER", nullable: false),
                    EmergencyContactName = table.Column<string>(type: "TEXT", nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "TEXT", nullable: true),
                    MedicalNotes = table.Column<string>(type: "TEXT", nullable: true),
                    GeneralNotes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiveSites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    Latitude = table.Column<decimal>(type: "TEXT", nullable: true),
                    Longitude = table.Column<decimal>(type: "TEXT", nullable: true),
                    MaximumDepthMeters = table.Column<decimal>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiveSites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EquipmentType = table.Column<int>(type: "INTEGER", nullable: false),
                    Brand = table.Column<string>(type: "TEXT", nullable: true),
                    Model = table.Column<string>(type: "TEXT", nullable: true),
                    SerialNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Size = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumberSequences",
                columns: table => new
                {
                    Year = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LastValue = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberSequences", x => x.Year);
                });

            migrationBuilder.CreateTable(
                name: "Staff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    CertificationAgency = table.Column<int>(type: "INTEGER", nullable: true),
                    InstructorNumber = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ActivityType = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    DefaultCurrencyId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activities_Currencies_DefaultCurrencyId",
                        column: x => x.DefaultCurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffStaffRoles",
                columns: table => new
                {
                    StaffId = table.Column<int>(type: "INTEGER", nullable: false),
                    StaffRoleId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffStaffRoles", x => new { x.StaffId, x.StaffRoleId });
                    table.ForeignKey(
                        name: "FK_StaffStaffRoles_StaffRoles_StaffRoleId",
                        column: x => x.StaffRoleId,
                        principalTable: "StaffRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffStaffRoles_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReservationNumber = table.Column<string>(type: "TEXT", nullable: false),
                    ReservationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActivityDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    MainCustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                    InstructorId = table.Column<int>(type: "INTEGER", nullable: true),
                    DiveSiteId = table.Column<int>(type: "INTEGER", nullable: true),
                    BoatId = table.Column<int>(type: "INTEGER", nullable: true),
                    NumberOfParticipants = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.CheckConstraint("CK_Reservation_Count", "NumberOfParticipants > 0");
                    table.ForeignKey(
                        name: "FK_Reservations_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_Boats_BoatId",
                        column: x => x.BoatId,
                        principalTable: "Boats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_Customers_MainCustomerId",
                        column: x => x.MainCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_DiveSites_DiveSiteId",
                        column: x => x.DiveSiteId,
                        principalTable: "DiveSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_Staff_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiveOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReservationId = table.Column<int>(type: "INTEGER", nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    DiveSiteId = table.Column<int>(type: "INTEGER", nullable: false),
                    BoatId = table.Column<int>(type: "INTEGER", nullable: true),
                    LeadInstructorId = table.Column<int>(type: "INTEGER", nullable: true),
                    DiveType = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiveOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiveOperations_Boats_BoatId",
                        column: x => x.BoatId,
                        principalTable: "Boats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiveOperations_DiveSites_DiveSiteId",
                        column: x => x.DiveSiteId,
                        principalTable: "DiveSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiveOperations_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiveOperations_Staff_LeadInstructorId",
                        column: x => x.LeadInstructorId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservationParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReservationId = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    CertificationAgency = table.Column<int>(type: "INTEGER", nullable: true),
                    CertificationLevel = table.Column<string>(type: "TEXT", nullable: true),
                    CertificationNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationParticipants_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationParticipants_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TransactionType = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    CurrencyId = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReservationId = table.Column<int>(type: "INTEGER", nullable: true),
                    PersonCount = table.Column<int>(type: "INTEGER", nullable: true),
                    DiveCount = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.CheckConstraint("CK_Transaction_Amount", "CAST(Amount AS NUMERIC) > 0");
                    table.ForeignKey(
                        name: "FK_Transactions_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_TransactionCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TransactionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiveLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiveOperationId = table.Column<int>(type: "INTEGER", nullable: false),
                    DiveSiteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EntryTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    ExitTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    BottomTimeMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxDepthMeters = table.Column<decimal>(type: "TEXT", nullable: false),
                    WaterTemperatureCelsius = table.Column<decimal>(type: "TEXT", nullable: true),
                    VisibilityMeters = table.Column<decimal>(type: "TEXT", nullable: true),
                    CurrentCondition = table.Column<string>(type: "TEXT", nullable: true),
                    WeatherCondition = table.Column<string>(type: "TEXT", nullable: true),
                    DiveType = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiveLogs", x => x.Id);
                    table.CheckConstraint("CK_DiveLog_Measurements", "CAST(MaxDepthMeters AS NUMERIC) >= 0 AND BottomTimeMinutes >= 0");
                    table.ForeignKey(
                        name: "FK_DiveLogs_DiveOperations_DiveOperationId",
                        column: x => x.DiveOperationId,
                        principalTable: "DiveOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiveLogs_DiveSites_DiveSiteId",
                        column: x => x.DiveSiteId,
                        principalTable: "DiveSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiveOperationParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiveOperationId = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReservationParticipantId = table.Column<int>(type: "INTEGER", nullable: true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    BuddyGroup = table.Column<string>(type: "TEXT", nullable: true),
                    InstructorId = table.Column<int>(type: "INTEGER", nullable: true),
                    EquipmentRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiveOperationParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiveOperationParticipants_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiveOperationParticipants_DiveOperations_DiveOperationId",
                        column: x => x.DiveOperationId,
                        principalTable: "DiveOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiveOperationParticipants_ReservationParticipants_ReservationParticipantId",
                        column: x => x.ReservationParticipantId,
                        principalTable: "ReservationParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiveOperationParticipants_Staff_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiverDiveLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiveLogId = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                    ParticipantName = table.Column<string>(type: "TEXT", nullable: false),
                    InstructorId = table.Column<int>(type: "INTEGER", nullable: true),
                    BuddyCustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxDepthMeters = table.Column<decimal>(type: "TEXT", nullable: true),
                    BottomTimeMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    StartPressureBar = table.Column<decimal>(type: "TEXT", nullable: true),
                    EndPressureBar = table.Column<decimal>(type: "TEXT", nullable: true),
                    TankSizeLiters = table.Column<decimal>(type: "TEXT", nullable: true),
                    GasType = table.Column<int>(type: "INTEGER", nullable: false),
                    OxygenPercentage = table.Column<decimal>(type: "TEXT", nullable: true),
                    WeightKg = table.Column<decimal>(type: "TEXT", nullable: true),
                    SuitType = table.Column<string>(type: "TEXT", nullable: true),
                    PersonalNotes = table.Column<string>(type: "TEXT", nullable: true),
                    IsVerified = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiverDiveLogs", x => x.Id);
                    table.CheckConstraint("CK_DiverDiveLog_Measurements", "(MaxDepthMeters IS NULL OR CAST(MaxDepthMeters AS NUMERIC) >= 0) AND (BottomTimeMinutes IS NULL OR BottomTimeMinutes >= 0) AND (StartPressureBar IS NULL OR CAST(StartPressureBar AS NUMERIC) >= 0) AND (EndPressureBar IS NULL OR CAST(EndPressureBar AS NUMERIC) >= 0) AND (OxygenPercentage IS NULL OR CAST(OxygenPercentage AS NUMERIC) BETWEEN 21 AND 100)");
                    table.ForeignKey(
                        name: "FK_DiverDiveLogs_Customers_BuddyCustomerId",
                        column: x => x.BuddyCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiverDiveLogs_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiverDiveLogs_DiveLogs_DiveLogId",
                        column: x => x.DiveLogId,
                        principalTable: "DiveLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiverDiveLogs_Staff_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_DefaultCurrencyId",
                table: "Activities",
                column: "DefaultCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Code",
                table: "Currencies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiveLogs_DiveOperationId",
                table: "DiveLogs",
                column: "DiveOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiveLogs_DiveSiteId",
                table: "DiveLogs",
                column: "DiveSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_DiveOperationParticipants_CustomerId",
                table: "DiveOperationParticipants",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DiveOperationParticipants_DiveOperationId",
                table: "DiveOperationParticipants",
                column: "DiveOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_DiveOperationParticipants_InstructorId",
                table: "DiveOperationParticipants",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_DiveOperationParticipants_ReservationParticipantId",
                table: "DiveOperationParticipants",
                column: "ReservationParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_DiveOperations_BoatId",
                table: "DiveOperations",
                column: "BoatId");

            migrationBuilder.CreateIndex(
                name: "IX_DiveOperations_Date_Status",
                table: "DiveOperations",
                columns: new[] { "Date", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DiveOperations_DiveSiteId",
                table: "DiveOperations",
                column: "DiveSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_DiveOperations_LeadInstructorId",
                table: "DiveOperations",
                column: "LeadInstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_DiveOperations_ReservationId",
                table: "DiveOperations",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_DiverDiveLogs_BuddyCustomerId",
                table: "DiverDiveLogs",
                column: "BuddyCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DiverDiveLogs_CustomerId",
                table: "DiverDiveLogs",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DiverDiveLogs_DiveLogId_CustomerId",
                table: "DiverDiveLogs",
                columns: new[] { "DiveLogId", "CustomerId" },
                unique: true,
                filter: "CustomerId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiverDiveLogs_InstructorId",
                table: "DiverDiveLogs",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationParticipants_CustomerId",
                table: "ReservationParticipants",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationParticipants_ReservationId",
                table: "ReservationParticipants",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ActivityDate_Status",
                table: "Reservations",
                columns: new[] { "ActivityDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ActivityId",
                table: "Reservations",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_BoatId",
                table: "Reservations",
                column: "BoatId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_DiveSiteId",
                table: "Reservations",
                column: "DiveSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_InstructorId",
                table: "Reservations",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_MainCustomerId",
                table: "Reservations",
                column: "MainCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ReservationNumber",
                table: "Reservations",
                column: "ReservationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffRoles_Name",
                table: "StaffRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffStaffRoles_StaffRoleId",
                table: "StaffStaffRoles",
                column: "StaffRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ActivityId",
                table: "Transactions",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CategoryId",
                table: "Transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CurrencyId",
                table: "Transactions",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ReservationId",
                table: "Transactions",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionDate_CurrencyId",
                table: "Transactions",
                columns: new[] { "TransactionDate", "CurrencyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiveOperationParticipants");

            migrationBuilder.DropTable(
                name: "DiverDiveLogs");

            migrationBuilder.DropTable(
                name: "Equipment");

            migrationBuilder.DropTable(
                name: "NumberSequences");

            migrationBuilder.DropTable(
                name: "StaffStaffRoles");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "ReservationParticipants");

            migrationBuilder.DropTable(
                name: "DiveLogs");

            migrationBuilder.DropTable(
                name: "StaffRoles");

            migrationBuilder.DropTable(
                name: "TransactionCategories");

            migrationBuilder.DropTable(
                name: "DiveOperations");

            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "Boats");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "DiveSites");

            migrationBuilder.DropTable(
                name: "Staff");

            migrationBuilder.DropTable(
                name: "Currencies");
        }
    }
}
