using System.Linq.Expressions;

namespace DiveCenterManager.Services;

internal static class DateQueryExtensions
{
    // Inclusive calendar-day bounds without applying a function to the indexed column.
    public static IQueryable<T> InDateRange<T>(this IQueryable<T> query, Expression<Func<T, DateTime>> date, DateTime? from, DateTime? to)
    {
        Validation.Require(!from.HasValue || !to.HasValue || from.Value.Date <= to.Value.Date, "The end date must be on or after the start date.");
        if (from.HasValue)
            query = query.Where(Expression.Lambda<Func<T, bool>>(Expression.GreaterThanOrEqual(date.Body, Expression.Constant(from.Value.Date)), date.Parameters));
        if (to.HasValue && to.Value.Date < DateTime.MaxValue.Date)
            query = query.Where(Expression.Lambda<Func<T, bool>>(Expression.LessThan(date.Body, Expression.Constant(to.Value.Date.AddDays(1))), date.Parameters));
        return query;
    }
}
