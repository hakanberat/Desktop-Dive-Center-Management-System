using System.IO;
using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;

namespace DiveCenterManager.Helpers;

public sealed class AppPaths
{
    public string DataDirectory { get; }
    public string DatabasePath => Path.Combine(DataDirectory, "DiveCenter.db");
    public string BackupDirectory => Path.Combine(DataDirectory, "Backups");
    public string LogPath => Path.Combine(DataDirectory, "application.log");
    public bool IsSampleWorkspace { get; }
    public static string DefaultDataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiveHubManagement");
    public string ConnectionString => new SqliteConnectionStringBuilder { DataSource = DatabasePath, ForeignKeys = true, DefaultTimeout = 30 }.ToString();
    public AppPaths(string? directory = null, bool sampleWorkspace = false)
    {
        if (directory != null && string.IsNullOrWhiteSpace(directory)) throw new ValidationException("Choose a non-empty database folder.");
        DataDirectory = CanonicalPath(directory ?? DefaultDataDirectory);
        IsSampleWorkspace = sampleWorkspace;
        ValidateStorage();
    }
    public void ValidateStorage()
    {
        if (File.Exists(DataDirectory)) throw new ValidationException("The database folder points to a file. Choose a folder instead.");
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupDirectory);
        if (Directory.Exists(DatabasePath)) throw new ValidationException("DiveCenter.db is a folder. Select a data folder with a valid database file.");
        if (File.Exists(DatabasePath) && (File.GetAttributes(DatabasePath) & FileAttributes.ReadOnly) != 0)
            throw new ValidationException("The database file is read-only. Enable write access before opening this workspace.");
        // WAL and migrations also require write access to the containing directory.
        var probe = Path.Combine(DataDirectory, ".write-check-" + Guid.NewGuid().ToString("N"));
        using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
    }
    public static string CanonicalPath(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var resolved = root;
        // Resolve existing junction/symbolic-link segments before comparing database paths.
        foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            resolved = Path.Combine(resolved, part);
            FileSystemInfo? entry = Directory.Exists(resolved) ? new DirectoryInfo(resolved) : File.Exists(resolved) ? new FileInfo(resolved) : null;
            if (entry?.LinkTarget != null) resolved = entry.ResolveLinkTarget(true)?.FullName ?? resolved;
        }
        return Path.TrimEndingDirectorySeparator(resolved);
    }
    public void Log(Exception exception)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.UtcNow:O} {exception}\n"); }
        catch (IOException) { /* An unavailable log destination must not hide the original error. */ }
        catch (UnauthorizedAccessException) { }
    }
}
