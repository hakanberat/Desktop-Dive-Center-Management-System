using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public sealed class StaffService(IDbContextFactory<DiveCenterDbContext> factory) : ICatalogService<Staff>
{
    public async Task<List<Staff>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Staff.AsNoTracking().Include(x => x.StaffRoles).ThenInclude(x => x.StaffRole)
            .OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ToListAsync();
    }

    public async Task SaveAsync(Staff record)
    {
        Validation.Check(record);
        await using var db = await factory.CreateDbContextAsync();
        var saved = record.Id == 0 ? new Staff() : await db.Staff.Include(x => x.StaffRoles).SingleOrDefaultAsync(x => x.Id == record.Id)
            ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Record no longer exists. Refresh the list.");
        var roleIds = record.StaffRoles.Select(x => x.StaffRoleId).Distinct().ToList();
        var previousRoleIds = saved.StaffRoles.Select(x => x.StaffRoleId).ToList();
        Validation.Require(await db.StaffRoles.CountAsync(x => roleIds.Contains(x.Id) && (x.IsActive || previousRoleIds.Contains(x.Id))) == roleIds.Count,
            "Select active staff roles. Existing inactive roles may be retained.");
        db.Entry(saved).CurrentValues.SetValues(record);
        if (record.Id == 0) db.Staff.Add(saved);
        foreach (var old in saved.StaffRoles.Where(x => !roleIds.Contains(x.StaffRoleId)).ToList()) db.StaffStaffRoles.Remove(old);
        foreach (var id in roleIds.Where(id => !saved.StaffRoles.Any(x => x.StaffRoleId == id))) saved.StaffRoles.Add(new StaffStaffRole { StaffRoleId = id });
        await db.SaveChangesAsync();
        record.Id = saved.Id;
    }

    public Task SetActiveAsync(int id, bool active) => CatalogPersistence.SetActiveAsync<Staff>(factory, id, active);
}
