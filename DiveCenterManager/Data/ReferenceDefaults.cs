using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Data;

// Optional catalog defaults, never invoked by production initialization.
// Public site names: https://dive-hub.com/en/services/dive-sites/
public sealed class ReferenceDefaults(IDbContextFactory<DiveCenterDbContext> factory)
{
    public async Task AddAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var activities = new (string Name, ActivityType Type)[]
        {
            ("Try Dive", ActivityType.TryDive), ("Discover Scuba Diving", ActivityType.TryDive), ("Shore Dive", ActivityType.GuidedDive),
            ("Boat Dive", ActivityType.GuidedDive), ("Night Dive", ActivityType.GuidedDive), ("Fun Dive", ActivityType.FunDive),
            ("Check Dive", ActivityType.TrainingDive), ("Open Water Diver", ActivityType.Course), ("Advanced Open Water Diver", ActivityType.Course),
            ("Rescue Diver", ActivityType.Course), ("Divemaster", ActivityType.Course), ("Enriched Air", ActivityType.Specialty), ("Private Dive", ActivityType.PrivateDive)
        };
        var activityNames = await db.Activities.Select(x => x.Name).ToListAsync();
        db.Activities.AddRange(activities.Where(x => !activityNames.Contains(x.Name, StringComparer.OrdinalIgnoreCase)).Select(x => new Activity { Name = x.Name, ActivityType = x.Type }));
        var siteNames = await db.DiveSites.Select(x => x.Name).ToListAsync();
        var sites = new[] { "Kocareis Beach", "Balloon", "Bogaz Ship Wreck", "Alp", "Bedri", "Pitos", "Noah's Ark", "Palm Beach", "Ray Cave", "Tomofil Car Wreck", "Abdulselam", "A-Wall" };
        db.DiveSites.AddRange(sites.Where(x => !siteNames.Contains(x, StringComparer.OrdinalIgnoreCase)).Select(x => new DiveSite { Name = x, Location = "Northern Cyprus" }));
        await db.SaveChangesAsync();
    }
}
