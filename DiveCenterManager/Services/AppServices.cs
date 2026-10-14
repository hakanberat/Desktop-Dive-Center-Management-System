using DiveCenterManager.Data;
using DiveCenterManager.Helpers;
using DiveCenterManager.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DiveCenterManager.Services;

public static class AppServices
{
    public static IServiceCollection AddDiveHub(this IServiceCollection services, AppPaths paths)
    {
        services.AddSingleton(paths);
        services.AddDbContextFactory<DiveCenterDbContext>(builder => builder.UseSqlite(paths.ConnectionString));
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<MasterDataService>();
        services.AddSingleton<LookupService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<ReservationService>();
        services.AddSingleton<DiveOperationService>();
        services.AddSingleton<LogbookService>();
        services.AddSingleton<CustomerService>();
        services.AddSingleton<ActivityService>();
        services.AddSingleton<DiveSiteService>();
        services.AddSingleton<StaffService>();
        services.AddSingleton<BoatService>();
        services.AddSingleton<EquipmentService>();
        services.AddSingleton<CurrencyService>();
        services.AddSingleton<TransactionCategoryService>();
        services.AddSingleton<StaffRoleService>();
        services.AddSingleton<TransactionService>();
        services.AddSingleton<DashboardService>();
        services.AddSingleton<ReportService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<ReferenceDefaults>();
        services.AddSingleton<DevelopmentSampleData>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<CustomerViewModel>();
        services.AddTransient<ReservationViewModel>();
        services.AddTransient<OperationViewModel>();
        services.AddTransient<TransactionViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<ActivityViewModel>();
        services.AddTransient<DiveSiteViewModel>();
        services.AddTransient<StaffViewModel>();
        services.AddTransient<BoatViewModel>();
        services.AddTransient<EquipmentViewModel>();
        services.AddTransient<CurrencyViewModel>();
        services.AddTransient<TransactionCategoryViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MainWindow>();
        return services;
    }
}
