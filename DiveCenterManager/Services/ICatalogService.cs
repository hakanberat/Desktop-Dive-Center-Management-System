using DiveCenterManager.Models;

namespace DiveCenterManager.Services;

// Application operations shared by the small editable catalogs, not operational aggregates.
public interface ICatalogService<T> where T : class, IActiveEntity
{
    Task<List<T>> ListAsync();
    Task SaveAsync(T record);
    Task SetActiveAsync(int id, bool active);
}
