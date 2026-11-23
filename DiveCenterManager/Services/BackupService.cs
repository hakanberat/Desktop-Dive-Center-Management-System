using System.Diagnostics;
using System.IO;
using DiveCenterManager.Helpers;
using Microsoft.Data.Sqlite;

namespace DiveCenterManager.Services;

public sealed class BackupService(AppPaths paths)
{
    private string LastFolderFile => Path.Combine(paths.DataDirectory, "last-backup-folder.txt");
    private string? _lastFolder;
    public string Folder
    {
        get
        {
            if (_lastFolder != null && Directory.Exists(_lastFolder)) return _lastFolder;
            try
            {
                if (File.Exists(LastFolderFile)) { var folder = File.ReadAllText(LastFolderFile).Trim(); if (Directory.Exists(folder)) return _lastFolder = folder; }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { paths.Log(e); }
            return paths.BackupDirectory;
        }
    }
    public async Task CreateAsync(string destination)
    {
        Validation.Require(!string.IsNullOrWhiteSpace(destination), "Choose a backup file.");
        destination = AppPaths.CanonicalPath(destination);
        var live = AppPaths.CanonicalPath(paths.DatabasePath);
        Validation.Require(!string.Equals(destination, live, StringComparison.OrdinalIgnoreCase), "Choose a backup destination other than the live database.");
        Validation.Require(!destination.StartsWith(live + "-", StringComparison.OrdinalIgnoreCase), "A backup cannot replace a live SQLite journal file.");
        Validation.Require(!Directory.Exists(destination), "Select a backup file, not a folder.");
        Validation.Require(!File.Exists(destination + "-wal") && !File.Exists(destination + "-shm") && !File.Exists(destination + "-journal"),
            "The destination has SQLite journal files. Close the application using it and choose a new backup filename.");
        var folder = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, ".divehub-backup-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            // On Windows this also prevents replacing the source through an alternate path spelling.
            using var sourceGuard = new FileStream(paths.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            await Task.Run(() =>
            {
                using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
                using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temporary, Pooling = false }.ToString());
                source.Open(); target.Open();
                // SQLite's online backup API captures committed WAL content safely.
                source.BackupDatabase(target);
                using var command = target.CreateCommand();
                // Publish a standalone backup that does not create WAL sidecars when opened read-only.
                command.CommandText = "PRAGMA journal_mode=DELETE";
                Validation.Require(Convert.ToString(command.ExecuteScalar()) == "delete", "Backup finalization failed. The existing destination has been preserved.");
                command.CommandText = "PRAGMA integrity_check";
                Validation.Require(Convert.ToString(command.ExecuteScalar()) == "ok", "Backup integrity verification failed. The existing destination has been preserved.");
            });
            File.Move(temporary, destination, overwrite: true);
            _lastFolder = folder;
            try { await File.WriteAllTextAsync(LastFolderFile, folder); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { paths.Log(e); }
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { paths.Log(e); }
        }
    }
    public void OpenFolder() { var folder = Folder; Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); }
}
