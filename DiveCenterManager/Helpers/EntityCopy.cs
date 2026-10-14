using DiveCenterManager.Models;

namespace DiveCenterManager.Helpers;

public static class EntityCopy
{
    public static object Copy(object source)
    {
        var target = Activator.CreateInstance(source.GetType())!;
        foreach (var property in source.GetType().GetProperties().Where(x => x.CanWrite && EntitySchema.IsScalar(x.PropertyType)))
            property.SetValue(target, property.GetValue(source));
        if (source is Staff staff && target is Staff copy)
            copy.StaffRoles = staff.StaffRoles.Select(x => new StaffStaffRole { StaffId = x.StaffId, StaffRoleId = x.StaffRoleId }).ToList();
        return target;
    }
}
