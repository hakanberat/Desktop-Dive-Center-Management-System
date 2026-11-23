using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiveCenterManager.Models;

public interface IEntity { int Id { get; set; } }
public interface IActiveEntity : IEntity { bool IsActive { get; set; } }

public class Customer : IActiveEntity
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string FirstName { get; set; } = "";
    [Required, MaxLength(100)] public string LastName { get; set; } = "";
    public string Phone { get; set; } = "";
    [EmailAddress] public string? Email { get; set; }
    public DateTime? BirthDate { get; set; }
    public string Nationality { get; set; } = "";
    public CertificationAgency? CertificationAgency { get; set; }
    public string? CertificationLevel { get; set; }
    public string? CertificationNumber { get; set; }
    public DateTime? LastDiveDate { get; set; }
    [Range(0, int.MaxValue)] public int TotalLoggedDives { get; set; }
    public int PriorLoggedDives { get; set; }
    public DateTime? PriorLastDiveDate { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? MedicalNotes { get; set; }
    public string? GeneralNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    [NotMapped] public string FullName => $"{FirstName} {LastName}".Trim();
    public override string ToString() => FullName;
}

public class Activity : IActiveEntity
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    public ActivityType ActivityType { get; set; }
    [Range(typeof(decimal), "0", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal? DefaultPrice { get; set; }
    public int? DefaultCurrencyId { get; set; }
    public Currency? DefaultCurrency { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public override string ToString() => Name;
}

public class Reservation : IEntity
{
    public int Id { get; set; }
    public string ReservationNumber { get; set; } = "";
    public DateTime ReservationDate { get; set; } = DateTime.Today;
    public DateTime ActivityDate { get; set; } = DateTime.Today;
    public TimeSpan StartTime { get; set; } = TimeSpan.FromHours(9);
    [Range(1, int.MaxValue)] public int ActivityId { get; set; }
    public Activity? Activity { get; set; }
    public int? MainCustomerId { get; set; }
    public Customer? MainCustomer { get; set; }
    public int? InstructorId { get; set; }
    public Staff? Instructor { get; set; }
    public int? DiveSiteId { get; set; }
    public DiveSite? DiveSite { get; set; }
    public int? BoatId { get; set; }
    public Boat? Boat { get; set; }
    [Range(1, 10000)] public int NumberOfParticipants { get; set; } = 1;
    public ReservationStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public List<ReservationParticipant> Participants { get; set; } = [];
    public override string ToString() => $"{ReservationNumber} · {ActivityDate:dd MMM yyyy} · {Activity?.Name}";
}

public class ReservationParticipant : IEntity
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    [Required] public string FirstName { get; set; } = "";
    [Required] public string LastName { get; set; } = "";
    public CertificationAgency? CertificationAgency { get; set; }
    public string? CertificationLevel { get; set; }
    public string? CertificationNumber { get; set; }
    public string? Notes { get; set; }
    [NotMapped] public string FullName => $"{FirstName} {LastName}".Trim();
    public override string ToString() => FullName;
}

public class DiveOperation : IEntity
{
    public int Id { get; set; }
    public int? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public TimeSpan StartTime { get; set; } = TimeSpan.FromHours(9);
    [Range(1, int.MaxValue)] public int DiveSiteId { get; set; }
    public DiveSite? DiveSite { get; set; }
    public int? BoatId { get; set; }
    public Boat? Boat { get; set; }
    public int? LeadInstructorId { get; set; }
    public Staff? LeadInstructor { get; set; }
    public ActivityType DiveType { get; set; } = ActivityType.GuidedDive;
    public OperationStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public List<DiveOperationParticipant> Participants { get; set; } = [];
    public override string ToString() => $"Dive #{Id} · {Date:dd MMM yyyy} · {DiveSite?.Name}";
}

public class DiveOperationParticipant : IEntity
{
    public int Id { get; set; }
    public int DiveOperationId { get; set; }
    public DiveOperation? DiveOperation { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? ReservationParticipantId { get; set; }
    public ReservationParticipant? ReservationParticipant { get; set; }
    [Required] public string FirstName { get; set; } = "";
    [Required] public string LastName { get; set; } = "";
    public ParticipantRole Role { get; set; }
    public string? BuddyGroup { get; set; }
    public int? InstructorId { get; set; }
    public Staff? Instructor { get; set; }
    public bool EquipmentRequired { get; set; }
    public string? Notes { get; set; }
    [NotMapped] public string FullName => $"{FirstName} {LastName}".Trim();
    public override string ToString() => FullName;
}

public class DiveLog : IEntity
{
    public int Id { get; set; }
    [Range(1, int.MaxValue)] public int DiveOperationId { get; set; }
    public DiveOperation? DiveOperation { get; set; }
    [Range(1, int.MaxValue)] public int DiveSiteId { get; set; }
    public DiveSite? DiveSite { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public TimeSpan EntryTime { get; set; } = TimeSpan.FromHours(9);
    public TimeSpan ExitTime { get; set; } = TimeSpan.FromHours(10);
    [Range(0, 1440)] public int BottomTimeMinutes { get; set; }
    [Range(typeof(decimal), "0", "1000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal MaxDepthMeters { get; set; }
    [Range(typeof(decimal), "-10", "60", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal? WaterTemperatureCelsius { get; set; }
    [Range(typeof(decimal), "0", "1000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal? VisibilityMeters { get; set; }
    public string? CurrentCondition { get; set; }
    public string? WeatherCondition { get; set; }
    public ActivityType DiveType { get; set; }
    public string? Notes { get; set; }
    public List<DiverDiveLog> DiverLogs { get; set; } = [];
    public override string ToString() => $"Log #{Id} · {Date:dd MMM yyyy} · {DiveSite?.Name}";
}

public class DiverDiveLog : IEntity
{
    public int Id { get; set; }
    public int DiveLogId { get; set; }
    public DiveLog? DiveLog { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    [Required] public string ParticipantName { get; set; } = "";
    public int? InstructorId { get; set; }
    public Staff? Instructor { get; set; }
    public int? BuddyCustomerId { get; set; }
    public Customer? BuddyCustomer { get; set; }
    [Range(typeof(decimal), "0", "1000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal? MaxDepthMeters { get; set; }
    [Range(0, 1440)] public int? BottomTimeMinutes { get; set; }
    [Range(typeof(decimal), "0", "1000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal? StartPressureBar { get; set; }
    [Range(typeof(decimal), "0", "1000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal? EndPressureBar { get; set; }
    [Range(typeof(decimal), "0.1", "100", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal? TankSizeLiters { get; set; }
    public GasType GasType { get; set; }
    [Range(typeof(decimal), "21", "100", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal? OxygenPercentage { get; set; }
    [Range(typeof(decimal), "0", "1000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal? WeightKg { get; set; }
    public string? SuitType { get; set; }
    public string? PersonalNotes { get; set; }
    public bool IsVerified { get; set; }
    public override string ToString() => ParticipantName;
}

public class DiveSite : IActiveEntity
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    [Range(typeof(decimal), "-90", "90", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal? Latitude { get; set; }
    [Range(typeof(decimal), "-180", "180", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal? Longitude { get; set; }
    [Range(typeof(decimal), "0", "1000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal? MaximumDepthMeters { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public override string ToString() => Name;
}

public class Staff : IActiveEntity
{
    public int Id { get; set; }
    [Required] public string FirstName { get; set; } = "";
    [Required] public string LastName { get; set; } = "";
    public string? Phone { get; set; }
    [EmailAddress] public string? Email { get; set; }
    public CertificationAgency? CertificationAgency { get; set; }
    public string? InstructorNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public List<StaffStaffRole> StaffRoles { get; set; } = [];
    [NotMapped] public string FullName => $"{FirstName} {LastName}".Trim();
    [NotMapped] public string RoleNames => string.Join(", ", StaffRoles.Select(x => x.StaffRole?.Name));
    public override string ToString() => FullName;
}

public class StaffRole : IActiveEntity
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public override string ToString() => Name;
}
public class StaffStaffRole
{
    public int StaffId { get; set; }
    public Staff? Staff { get; set; }
    public int StaffRoleId { get; set; }
    public StaffRole? StaffRole { get; set; }
}

public class Boat : IActiveEntity
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string? RegistrationNumber { get; set; }
    [Range(1, 10000)] public int Capacity { get; set; } = 10;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public override string ToString() => Name;
}

public class Equipment : IActiveEntity
{
    public int Id { get; set; }
    public EquipmentType EquipmentType { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? Size { get; set; }
    public EquipmentStatus Status { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public override string ToString() => $"{EquipmentType} · {Brand} {Model} · {SerialNumber}";
}

public class Currency : IActiveEntity
{
    public int Id { get; set; }
    [Required, RegularExpression("^[A-Z]{3}$", ErrorMessage = "Currency code must contain three uppercase letters.")] public string Code { get; set; } = "";
    [Required] public string Name { get; set; } = "";
    [Required] public string Symbol { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public override string ToString() => $"{Code} · {Name}";
}

public class TransactionCategory : IActiveEntity
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public CategoryType Type { get; set; } = CategoryType.Both;
    public bool IsActive { get; set; } = true;
    public override string ToString() => Name;
}

public class Transaction : IEntity
{
    public int Id { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Today;
    public TransactionType TransactionType { get; set; }
    [Required] public decimal Amount { get; set; }
    [Range(1, int.MaxValue)] public int CurrencyId { get; set; }
    public Currency? Currency { get; set; }
    public int? CategoryId { get; set; }
    public TransactionCategory? Category { get; set; }
    public int? ActivityId { get; set; }
    public Activity? Activity { get; set; }
    public int? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    [Range(0, int.MaxValue)] public int? PersonCount { get; set; }
    [Range(0, int.MaxValue)] public int? DiveCount { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class NumberSequence
{
    public int Year { get; set; }
    public int LastValue { get; set; }
}
