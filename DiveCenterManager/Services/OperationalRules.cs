using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

internal static class OperationalRules
{
    public static async Task ActiveReferenceAsync<T>(DiveCenterDbContext db, int? id, int? previous = null) where T : class, IActiveEntity
    {
        if (!id.HasValue) return;
        var record = await db.Set<T>().SingleOrDefaultAsync(x => x.Id == id.Value);
        Validation.Require(record != null && (record.IsActive || id == previous), $"Select an active {typeof(T).Name}.");
    }
    public static async Task InstructorAsync(DiveCenterDbContext db, int? id, int? previous = null)
    {
        if (!id.HasValue) return;
        Validation.Require(await db.Staff.AnyAsync(x => x.Id == id && (x.Id == previous || x.IsActive && x.StaffRoles.Any(r => r.StaffRole!.Name == "Instructor" && r.StaffRole.IsActive))), "Select a staff member with the Instructor role.");
    }
    public static void UniqueCustomers(IEnumerable<int?> customerIds)
    {
        var ids = customerIds.Where(x => x.HasValue).ToList();
        Validation.Require(ids.Distinct().Count() == ids.Count, "The same customer cannot be included more than once.");
    }
    public static async Task BoatCapacityAsync(DiveCenterDbContext db, int? boatId, int participants)
    {
        if (boatId.HasValue)
        {
            var boat = await db.Boats.SingleAsync(x => x.Id == boatId);
            Validation.Require(participants <= boat.Capacity, $"{boat.Name} has capacity for {boat.Capacity} people. Reduce the participants or choose another boat.");
        }
    }
}
