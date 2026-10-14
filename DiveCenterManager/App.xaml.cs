using System.Windows;
using DiveCenterManager.Data;
using DiveCenterManager.Helpers;
using DiveCenterManager.Services;
using DiveCenterManager.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Windows.Markup;
using System.ComponentModel.DataAnnotations;

namespace DiveCenterManager;

public partial class App : Application
{
    private ServiceProvider? _services;
    private AppPaths? _paths;
    private Mutex? _mutex;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        AppOptions? options = null;
        try
        {
            options = AppOptions.Parse(e.Args);
            _paths = new AppPaths(options.Directory, options.SampleData);
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_paths.DatabasePath.ToUpperInvariant())))[..20];
            _mutex = new Mutex(true, "Local\\DiveHubManagement-" + key, out var isNew);
            if (!isNew) { MessageBox.Show("Dive Hub Management is already running."); Shutdown(); return; }
            DispatcherUnhandledException += (_, args) =>
            {
                args.Handled = true;
                if (options.IsDiagnostic) { LogError(args.Exception); File.WriteAllText(Path.Combine(_paths.DataDirectory, "test-failure.txt"), args.Exception.ToString()); Shutdown(1); return; }
                var message = ErrorMessages.Friendly(args.Exception);
                if (MainWindow?.DataContext is MainViewModel main) main.ShowNotice(message);
                else MessageBox.Show(message, "Dive Hub Management", MessageBoxButton.OK, MessageBoxImage.Error);
            };
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            var services = new ServiceCollection().AddDiveHub(_paths);
            _services = services.BuildServiceProvider();
            await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
            if (options.IsDiagnostic && !options.StartupSmoke)
            {
                var runner = new Diagnostics.AcceptanceRunner(_services, _paths);
                if (options.Phase8Test) await runner.RunPhase8Async();
                else if (options.Phase8Persistence) await runner.VerifyPhase8PersistenceAsync();
                else if (options.Phase7Test) await runner.RunPhase7Async();
                else if (options.Phase7Persistence) await runner.VerifyPhase7PersistenceAsync();
                else if (options.Phase6Test) await runner.RunPhase6Async();
                else if (options.Phase6Persistence) await runner.VerifyPhase6PersistenceAsync();
                else if (options.Phase5Test) await runner.RunPhase5Async();
                else if (options.Phase5Persistence) await runner.VerifyPhase5PersistenceAsync();
                else if (options.Phase4Test) await runner.RunPhase4Async();
                else if (options.Phase4Persistence) await runner.VerifyPhase4PersistenceAsync();
                else if (options.Phase3Test) await runner.RunPhase3Async();
                else if (options.Phase3Persistence) await runner.VerifyPhase3PersistenceAsync();
                else if (options.Phase2Test) await runner.RunPhase2Async();
                else if (options.SelfTest) await runner.RunAsync(); else await runner.VerifyPersistenceAsync();
                Shutdown(0); return;
            }
            if (options.SampleData) await _services.GetRequiredService<DevelopmentSampleData>().AddAsync();
            MainWindow = _services.GetRequiredService<MainWindow>();
            if (options.SampleData) MainWindow.Title += " · SAMPLE WORKSPACE";
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            if (options.StartupSmoke) { ShutdownMode = ShutdownMode.OnExplicitShutdown; MainWindow.ShowActivated = false; }
            MainWindow.Show();
            if (options.StartupSmoke)
            {
                await new Diagnostics.AcceptanceRunner(_services, _paths).VerifyStartupAsync((MainWindow)MainWindow);
                MainWindow.Close(); Shutdown(0);
            }
        }
        catch (Exception exception)
        {
            if (options?.IsDiagnostic == true) { LogError(exception); if (_paths != null) File.WriteAllText(Path.Combine(_paths.DataDirectory, "test-failure.txt"), exception.ToString()); Shutdown(1); return; }
            MessageBox.Show("Dive Hub Management could not start.\n\n" + ErrorMessages.Friendly(exception), "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    public void LogError(Exception exception) { if (exception is not ValidationException) _paths?.Log(exception); }
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) { LogError(e.Exception); e.SetObserved(); }
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) { if (e.ExceptionObject is Exception exception) LogError(exception); }
    protected override void OnExit(ExitEventArgs e)
    {
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        _services?.Dispose(); _mutex?.Dispose(); base.OnExit(e);
    }
}
