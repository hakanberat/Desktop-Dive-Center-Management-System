using DiveCenterManager.Models;
using DiveCenterManager.Services;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Data;

// Only used with --sample-data, which selects a separate database by default.
public sealed class DevelopmentSampleData(IDbContextFactory<DiveCenterDbContext> factory, ReferenceDefaults defaults, MasterDataService masters, ReservationService reservations, DiveOperationService operations, LogbookService logbook, TransactionService transactions)
{
    public async Task AddAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        if (await db.Customers.AnyAsync() || await db.Reservations.AnyAsync() || await db.Transactions.AnyAsync()) return;
        await defaults.AddAsync();
        var names = new[] { ("Deniz", "Yılmaz"), ("Alex", "Morgan"), ("Maya", "Reed"), ("Can", "Kaya"), ("Elena", "Rossi"), ("Emre", "Aydın"), ("Lina", "Weber"), ("Sam", "Taylor"), ("Ece", "Demir"), ("Nora", "Berg") };
        var customers = new List<Customer>();
        foreach (var (first, last) in names) { var customer = new Customer { FirstName = first, LastName = last, Nationality = "", GeneralNotes = "Fictional sample customer", TotalLoggedDives = 10 }; await masters.SaveAsync(customer); customers.Add(customer); }
        var instructorRole = await db.StaffRoles.SingleAsync(x => x.Name == "Instructor");
        var captainRole = await db.StaffRoles.SingleAsync(x => x.Name == "Captain");
        var instructor = new Staff { FirstName = "Sample", LastName = "Instructor", StaffRoles = [new StaffStaffRole { StaffRoleId = instructorRole.Id }, new StaffStaffRole { StaffRoleId = captainRole.Id }] };
        await masters.SaveAsync(instructor);
        var boat = new Boat { Name = "Dive Hub · Sample boat", Capacity = 12 }; await masters.SaveAsync(boat);
        var sites = await db.DiveSites.ToListAsync(); var activities = await db.Activities.ToListAsync();
        var currencies = await db.Currencies.ToListAsync();
        for (var i = -2; i < 6; i++)
        {
            var activity = activities.Single(x => x.Name == (i % 2 == 0 ? "Try Dive" : "Boat Dive"));
            var reservation = new Reservation
            {
                ActivityId = activity.Id, ActivityDate = DateTime.Today.AddDays(i), Status = ReservationStatus.Confirmed,
                StartTime = TimeSpan.FromHours(i % 2 == 0 ? 9 : 11.5), InstructorId = instructor.Id, DiveSiteId = sites[(i + 2) % sites.Count].Id,
                BoatId = i % 2 == 0 ? null : boat.Id, MainCustomerId = customers[i + 2].Id, Notes = "Fictional sample booking",
                Participants = customers.Skip(i + 2).Take(2).Select(x => new ReservationParticipant { CustomerId = x.Id, FirstName = x.FirstName, LastName = x.LastName }).ToList()
            };
            reservation.NumberOfParticipants = reservation.Participants.Count; await reservations.SaveAsync(reservation);
            if (i <= 0)
            {
                var operation = await operations.DraftFromReservationAsync(reservation.Id); await operations.SaveAsync(operation);
                if (i < 0)
                {
                    await operations.ChangeStatusAsync(operation.Id, OperationStatus.InProgress); await operations.ChangeStatusAsync(operation.Id, OperationStatus.Completed);
                    var log = await logbook.DraftFromOperationAsync(operation.Id); log.MaxDepthMeters = 12; log.WaterTemperatureCelsius = 26; log.VisibilityMeters = 20;
                    await logbook.SaveAsync(log);
                    reservation = await reservations.GetAsync(reservation.Id); reservation.Status = ReservationStatus.Completed; reservation.NumberOfParticipants = reservation.Participants.Count; await reservations.SaveAsync(reservation);
                }
            }
            await transactions.SaveAsync(new Transaction { TransactionDate = DateTime.Today.AddDays(Math.Min(i, 0)), TransactionType = TransactionType.Income, Amount = 90 + (i + 2) * 15, CurrencyId = currencies[(i + 2) % 4].Id, ReservationId = reservation.Id, ActivityId = activity.Id, PersonCount = reservation.NumberOfParticipants, Description = "Sample dive payment" });
        }
        await transactions.SaveAsync(new Transaction { TransactionType = TransactionType.Expense, Amount = 45, CurrencyId = currencies.Single(x => x.Code == "EUR").Id, Description = "Sample boat fuel", CategoryId = (await db.TransactionCategories.SingleAsync(x => x.Name == "Fuel")).Id });
    }
}
