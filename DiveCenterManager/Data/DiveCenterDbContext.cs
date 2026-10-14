using DiveCenterManager.Models;
using DiveCenterManager.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DiveCenterManager.Data;

public class DiveCenterDbContext(DbContextOptions<DiveCenterDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationParticipant> ReservationParticipants => Set<ReservationParticipant>();
    public DbSet<DiveOperation> DiveOperations => Set<DiveOperation>();
    public DbSet<DiveOperationParticipant> DiveOperationParticipants => Set<DiveOperationParticipant>();
    public DbSet<DiveLog> DiveLogs => Set<DiveLog>();
    public DbSet<DiverDiveLog> DiverDiveLogs => Set<DiverDiveLog>();
    public DbSet<DiveSite> DiveSites => Set<DiveSite>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<StaffRole> StaffRoles => Set<StaffRole>();
    public DbSet<StaffStaffRole> StaffStaffRoles => Set<StaffStaffRole>();
    public DbSet<Boat> Boats => Set<Boat>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<TransactionCategory> TransactionCategories => Set<TransactionCategory>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NumberSequence>().HasKey(x => x.Year);
        modelBuilder.Entity<StaffStaffRole>().HasKey(x => new { x.StaffId, x.StaffRoleId });
        ConfigureRelationships(modelBuilder);
        modelBuilder.Entity<Reservation>().HasIndex(x => x.ReservationNumber).IsUnique();
        modelBuilder.Entity<Reservation>().HasIndex(x => new { x.ActivityDate, x.Status });
        modelBuilder.Entity<DiveOperation>().HasIndex(x => new { x.Date, x.Status });
        modelBuilder.Entity<DiveLog>().HasIndex(x => x.DiveOperationId).IsUnique();
        modelBuilder.Entity<DiverDiveLog>().HasIndex(x => new { x.DiveLogId, x.CustomerId }).IsUnique().HasFilter("CustomerId IS NOT NULL");
        modelBuilder.Entity<Currency>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<StaffRole>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<Transaction>().HasIndex(x => new { x.TransactionDate, x.CurrencyId });
        // SQLite stores decimals as TEXT. Use decimal sums (C# or the provider's ef_sum), never double.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var foreignKey in entity.GetForeignKeys()) foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
                property.SetColumnType("TEXT");
        }
        modelBuilder.Entity<Transaction>().ToTable(t => t.HasCheckConstraint("CK_Transaction_Amount", "CAST(Amount AS NUMERIC) > 0"));
        modelBuilder.Entity<Reservation>().ToTable(t => t.HasCheckConstraint("CK_Reservation_Count", "NumberOfParticipants > 0"));
        modelBuilder.Entity<DiveLog>().ToTable(t => t.HasCheckConstraint("CK_DiveLog_Measurements", "CAST(MaxDepthMeters AS NUMERIC) >= 0 AND BottomTimeMinutes >= 0"));
        modelBuilder.Entity<DiverDiveLog>().ToTable(t => t.HasCheckConstraint("CK_DiverDiveLog_Measurements", "(MaxDepthMeters IS NULL OR CAST(MaxDepthMeters AS NUMERIC) >= 0) AND (BottomTimeMinutes IS NULL OR BottomTimeMinutes >= 0) AND (StartPressureBar IS NULL OR CAST(StartPressureBar AS NUMERIC) >= 0) AND (EndPressureBar IS NULL OR CAST(EndPressureBar AS NUMERIC) >= 0) AND (OxygenPercentage IS NULL OR CAST(OxygenPercentage AS NUMERIC) BETWEEN 21 AND 100)"));
    }

    private static void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        // Name every FK explicitly so navigation/property renames cannot silently create
        // shadow foreign keys. OnModelCreating applies Restrict to every relationship.
        modelBuilder.Entity<Activity>().HasOne(x => x.DefaultCurrency).WithMany().HasForeignKey(x => x.DefaultCurrencyId).IsRequired(false);

        modelBuilder.Entity<Reservation>().HasOne(x => x.Activity).WithMany().HasForeignKey(x => x.ActivityId).IsRequired();
        modelBuilder.Entity<Reservation>().HasOne(x => x.MainCustomer).WithMany().HasForeignKey(x => x.MainCustomerId).IsRequired(false);
        modelBuilder.Entity<Reservation>().HasOne(x => x.Instructor).WithMany().HasForeignKey(x => x.InstructorId).IsRequired(false);
        modelBuilder.Entity<Reservation>().HasOne(x => x.DiveSite).WithMany().HasForeignKey(x => x.DiveSiteId).IsRequired(false);
        modelBuilder.Entity<Reservation>().HasOne(x => x.Boat).WithMany().HasForeignKey(x => x.BoatId).IsRequired(false);
        modelBuilder.Entity<ReservationParticipant>().HasOne(x => x.Reservation).WithMany(x => x.Participants).HasForeignKey(x => x.ReservationId).IsRequired();
        modelBuilder.Entity<ReservationParticipant>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).IsRequired(false);

        modelBuilder.Entity<DiveOperation>().HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.ReservationId).IsRequired(false);
        modelBuilder.Entity<DiveOperation>().HasOne(x => x.DiveSite).WithMany().HasForeignKey(x => x.DiveSiteId).IsRequired();
        modelBuilder.Entity<DiveOperation>().HasOne(x => x.Boat).WithMany().HasForeignKey(x => x.BoatId).IsRequired(false);
        modelBuilder.Entity<DiveOperation>().HasOne(x => x.LeadInstructor).WithMany().HasForeignKey(x => x.LeadInstructorId).IsRequired(false);
        modelBuilder.Entity<DiveOperationParticipant>().HasOne(x => x.DiveOperation).WithMany(x => x.Participants).HasForeignKey(x => x.DiveOperationId).IsRequired();
        modelBuilder.Entity<DiveOperationParticipant>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).IsRequired(false);
        modelBuilder.Entity<DiveOperationParticipant>().HasOne(x => x.ReservationParticipant).WithMany().HasForeignKey(x => x.ReservationParticipantId).IsRequired(false);
        modelBuilder.Entity<DiveOperationParticipant>().HasOne(x => x.Instructor).WithMany().HasForeignKey(x => x.InstructorId).IsRequired(false);

        // The existing unique DiveOperationId index limits each operation to one shared log.
        modelBuilder.Entity<DiveLog>().HasOne(x => x.DiveOperation).WithMany().HasForeignKey(x => x.DiveOperationId).IsRequired();
        modelBuilder.Entity<DiveLog>().HasOne(x => x.DiveSite).WithMany().HasForeignKey(x => x.DiveSiteId).IsRequired();
        modelBuilder.Entity<DiverDiveLog>().HasOne(x => x.DiveLog).WithMany(x => x.DiverLogs).HasForeignKey(x => x.DiveLogId).IsRequired();
        modelBuilder.Entity<DiverDiveLog>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).IsRequired(false);
        modelBuilder.Entity<DiverDiveLog>().HasOne(x => x.Instructor).WithMany().HasForeignKey(x => x.InstructorId).IsRequired(false);
        modelBuilder.Entity<DiverDiveLog>().HasOne(x => x.BuddyCustomer).WithMany().HasForeignKey(x => x.BuddyCustomerId).IsRequired(false);

        modelBuilder.Entity<StaffStaffRole>().HasOne(x => x.Staff).WithMany(x => x.StaffRoles).HasForeignKey(x => x.StaffId).IsRequired();
        modelBuilder.Entity<StaffStaffRole>().HasOne(x => x.StaffRole).WithMany().HasForeignKey(x => x.StaffRoleId).IsRequired();

        modelBuilder.Entity<Transaction>().HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).IsRequired();
        modelBuilder.Entity<Transaction>().HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).IsRequired(false);
        modelBuilder.Entity<Transaction>().HasOne(x => x.Activity).WithMany().HasForeignKey(x => x.ActivityId).IsRequired(false);
        modelBuilder.Entity<Transaction>().HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.ReservationId).IsRequired(false);
    }
}

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DiveCenterDbContext>
{
    public DiveCenterDbContext CreateDbContext(string[] args)
    {
        var paths = new AppPaths(AppOptions.Parse(args).Directory);
        return new DiveCenterDbContext(new DbContextOptionsBuilder<DiveCenterDbContext>().UseSqlite(paths.ConnectionString).Options);
    }
}
