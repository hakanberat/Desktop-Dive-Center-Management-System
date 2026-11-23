using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using DiveCenterManager.Models;

namespace DiveCenterManager.Helpers;

public record ColumnSpec(string Path, string Header, string? Format = null);
public static class EntitySchema
{
    public static string Label(string name) => name switch
    {
        "IsActive" => "Active", "IsVerified" => "Verified", "TotalLoggedDives" => "Total logged dives (including prior dives)",
        "StartTime" or "EntryTime" or "ExitTime" => Words(name) + " (HH:mm)",
        "NumberOfParticipants" => "Participants", "DefaultCurrencyId" => "Default currency",
        _ => Words(name.EndsWith("Id", StringComparison.Ordinal) ? name[..^2] : name)
    };
    public static string Words(string value) => Regex.Replace(value, "(?<=[a-z0-9])([A-Z])", " $1");
    public static bool IsScalar(Type type) { type = Nullable.GetUnderlyingType(type) ?? type; return type.IsEnum || type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(TimeSpan); }
    public static IEnumerable<PropertyInfo> EditableProperties(Type type) => type.GetProperties().Where(p => p.CanWrite && IsScalar(p.PropertyType) && !Hidden(type).Contains(p.Name));
    private static HashSet<string> Hidden(Type type)
    {
        var hidden = new HashSet<string> { "Id", "CreatedAt", "UpdatedAt", "ReservationNumber", "PriorLoggedDives", "PriorLastDiveDate" };
        if (type == typeof(Reservation)) hidden.Add("MainCustomerId");
        if (type == typeof(ReservationParticipant)) hidden.Add("ReservationId");
        if (type == typeof(DiveOperationParticipant)) hidden.UnionWith(["DiveOperationId", "ReservationParticipantId"]);
        if (type == typeof(DiveOperation)) hidden.UnionWith(["ReservationId", "Status"]);
        if (type == typeof(DiveLog)) hidden.Add("DiveOperationId");
        if (type == typeof(DiverDiveLog)) hidden.Add("DiveLogId");
        return hidden;
    }
    public static List<ColumnSpec> Columns(Type type)
    {
        var names = type.Name switch
        {
            nameof(Customer) => "FullName,Phone,Email,CertificationAgency,CertificationLevel,TotalLoggedDives,LastDiveDate,IsActive",
            nameof(Activity) => "Name,ActivityType,DefaultPrice,DefaultCurrency.Code,IsActive",
            nameof(DiveSite) => "Name,Location,MaximumDepthMeters,IsActive",
            nameof(Staff) => "FullName,RoleNames,Phone,Email,CertificationAgency,InstructorNumber,IsActive",
            nameof(Boat) => "Name,RegistrationNumber,Capacity,IsActive",
            nameof(Equipment) => "EquipmentType,Brand,Model,SerialNumber,Size,Status,IsActive",
            nameof(Currency) => "Code,Name,Symbol,IsActive",
            nameof(TransactionCategory) => "Name,Type,IsActive",
            nameof(StaffRole) => "Name,IsActive",
            nameof(Reservation) => "ReservationNumber,ActivityDate,StartTime,Activity.Name,NumberOfParticipants,Instructor.FullName,Boat.Name,DiveSite.Name,Status",
            nameof(ReservationParticipant) => "FullName,Customer.FullName,CertificationAgency,CertificationLevel,CertificationNumber,Notes",
            nameof(DiveOperation) => "Date,StartTime,DiveSite.Name,DiveType,Participants.Count,LeadInstructor.FullName,Boat.Name,Status,Reservation.ReservationNumber",
            nameof(DiveOperationParticipant) => "FullName,Customer.FullName,Role,BuddyGroup,Instructor.FullName,EquipmentRequired,Notes",
            nameof(DiveLog) => "Date,EntryTime,DiveSite.Name,DiveOperationId,DiverLogs.Count,MaxDepthMeters,BottomTimeMinutes,DiveType",
            nameof(DiverDiveLog) => "ParticipantName,IsVerified,GasType,OxygenPercentage,MaxDepthMeters,BottomTimeMinutes,StartPressureBar,EndPressureBar,TankSizeLiters,WeightKg,Instructor.FullName,BuddyCustomer.FullName,SuitType,PersonalNotes",
            nameof(Transaction) => "TransactionDate,TransactionType,Activity.Name,Category.Name,Amount,Currency.Code,PersonCount,DiveCount,Reservation.ReservationNumber,Description",
            _ => "Name"
        };
        return names.Split(',').Select(n => new ColumnSpec(n, ColumnLabel(n), n.Contains("Date") ? "{0:dd MMM yyyy}" : n.EndsWith("Time", StringComparison.Ordinal) ? "{0:hh\\:mm}" : n is "Amount" or "DefaultPrice" ? "{0:#,##0.00##########################}" : null)).ToList();
    }
    private static string ColumnLabel(string name) => name switch
    {
        "FullName" => "Name", "TotalLoggedDives" => "Logged dives", "RoleNames" => "Roles", "DefaultCurrency.Code" => "Currency",
        "Customer.FullName" => "Linked customer", "Instructor.FullName" or "LeadInstructor.FullName" => "Instructor",
        "Reservation.ReservationNumber" => "Reservation", "Currency.Code" => "Currency",
        "Participants.Count" or "DiverLogs.Count" => "Participants",
        "DiveOperationId" => "Operation #", "BuddyCustomer.FullName" => "Buddy",
        "TransactionDate" => "Date", "TransactionType" => "Type",
        _ => Label(name.Replace(".Name", ""))
    };
    public static bool Required(PropertyInfo property) => property.GetCustomAttribute<RequiredAttribute>() != null || property.PropertyType == typeof(DateTime)
        || property.PropertyType == typeof(int) && property.Name.EndsWith("Id", StringComparison.Ordinal);
}
