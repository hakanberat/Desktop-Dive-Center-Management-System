using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public sealed class StaffRoleService(IDbContextFactory<DiveCenterDbContext> factory) : ICatalogService<StaffRole>
{
    public async Task<List<StaffRole>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.StaffRoles.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
    }

    public async Task SaveAsync(StaffRole record)
    {
        record.Name = record.Name.Trim();
        Validation.Check(record);
        await using var db = await factory.CreateDbContextAsync();
        var previous = await db.StaffRoles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == record.Id);
        Validation.Require(previous?.Name != "Instructor" || record.Name == "Instructor", "The Instructor role is used for instructor selection and cannot be renamed.");
        Validation.Require(!await db.StaffRoles.AnyAsync(x => x.Name.ToUpper() == record.Name.ToUpper() && x.Id != record.Id), "This staff role already exists.");
        await CatalogPersistence.SaveAsync(db, record);
    }

    public Task SetActiveAsync(int id, bool active) => CatalogPersistence.SetActiveAsync<StaffRole>(factory, id, active);
}
