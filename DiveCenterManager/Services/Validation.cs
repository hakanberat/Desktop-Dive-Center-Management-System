using System.ComponentModel.DataAnnotations;
using DiveCenterManager.Models;

namespace DiveCenterManager.Services;

public static class Validation
{
    public static void Check(object entity)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(entity, new ValidationContext(entity), errors, true);
        foreach (var property in entity.GetType().GetProperties())
        {
            var value = property.GetValue(entity);
            if (value != null && (Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType).IsEnum && !Enum.IsDefined(value.GetType(), value))
                errors.Add(new ValidationResult($"Select a valid {property.Name}."));
            if (property.PropertyType == typeof(DateTime) && value is DateTime date && date == default)
                errors.Add(new ValidationResult($"{property.Name} is required."));
            if (value is TimeSpan time && (time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)))
                errors.Add(new ValidationResult($"{property.Name} must be a time between 00:00 and 23:59."));
        }
        if (entity is DiveLog shared)
        {
            if (shared.ExitTime < shared.EntryTime)
                errors.Add(new ValidationResult("Exit time must be on or after entry time on the dive date."));
            else if (shared.BottomTimeMinutes > (shared.ExitTime - shared.EntryTime).TotalMinutes)
                errors.Add(new ValidationResult("Bottom time cannot exceed the entry-to-exit duration."));
        }
        if (entity is DiverDiveLog log)
        {
            if (log.GasType != GasType.Nitrox && log.OxygenPercentage.HasValue)
                errors.Add(new ValidationResult("Oxygen percentage is only used for Nitrox. Clear it for Air or Other."));
            if (log.GasType == GasType.Nitrox && !log.OxygenPercentage.HasValue)
                errors.Add(new ValidationResult("Enter the oxygen percentage for Nitrox (21–100)."));
            if (log.EndPressureBar > log.StartPressureBar)
                errors.Add(new ValidationResult("End pressure cannot exceed start pressure."));
        }
        if (entity is Activity activity && activity.DefaultPrice.HasValue && !activity.DefaultCurrencyId.HasValue)
            errors.Add(new ValidationResult("Select a currency for the default price."));
        if (entity is Transaction transaction && transaction.Amount <= 0)
            errors.Add(new ValidationResult("Amount must be greater than zero."));
        if (entity is Equipment equipment && new[] { equipment.SerialNumber, equipment.Brand, equipment.Model, equipment.Size }.All(string.IsNullOrWhiteSpace))
            errors.Add(new ValidationResult("Enter a serial/inventory number, brand, model or size to identify this equipment."));
        if (entity is Customer customer && customer.BirthDate > DateTime.Today)
            errors.Add(new ValidationResult("Birth date cannot be in the future."));
        if (errors.Count > 0) throw new ValidationException(string.Join(Environment.NewLine, errors.Select(x => x.ErrorMessage)));
    }
    public static void Require(bool condition, string message) { if (!condition) throw new ValidationException(message); }
}
