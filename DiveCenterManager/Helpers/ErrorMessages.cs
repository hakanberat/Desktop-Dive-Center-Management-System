using System.ComponentModel.DataAnnotations;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Helpers;

public static class ErrorMessages
{
    public static string Friendly(Exception exception)
    {
        (System.Windows.Application.Current as App)?.LogError(exception);
        if (exception is ValidationException) return exception.Message;
        if (exception is DbUpdateConcurrencyException) return "This record changed while it was open. Refresh and try again.";
        var cause = exception;
        while (cause.InnerException != null) cause = cause.InnerException;
        if (cause is SqliteException sqlite)
            return sqlite.SqliteErrorCode switch
            {
                5 or 6 => "The database is busy. Wait for the other operation to finish, then try again.",
                8 => "The database is read-only. Check write permission for the database file and its folder.",
                11 or 26 => "The selected file is not a valid, healthy SQLite database. Use a verified backup in a separate data folder.",
                13 => "The disk is full. Free some space before saving or creating a backup.",
                14 => "The database could not be opened. Check that its folder exists and you have access to it.",
                10 => "The database could not be read or written. Check the disk and folder permissions.",
                19 when sqlite.SqliteExtendedErrorCode is 1555 or 2067 => "A record with the same unique value already exists. Refresh and check the number or code.",
                19 when sqlite.SqliteExtendedErrorCode == 787 => "This record is linked to other records. Refresh and check its references before changing or removing it.",
                19 => "The record could not be saved. Check the required values and related records.",
                _ => "The database operation could not be completed. Refresh and try again; details are in the application log."
            };
        return cause switch
        {
            UnauthorizedAccessException => "Access to the selected file or folder was denied. Choose a writable folder or check its permissions.",
            DirectoryNotFoundException or FileNotFoundException => "The selected file or folder is unavailable. Check its location and try again.",
            PathTooLongException => "The selected path is too long. Choose a shorter folder or filename.",
            IOException => "The file operation could not be completed. Check free disk space, permissions and whether another application is using the file.",
            OperationCanceledException => "The operation was cancelled.",
            OverflowException => "The value or total is too large. Check the entered amounts or narrow the date range.",
            ArgumentException => "A setting or file path is invalid. Check the supplied options and selected folder.",
            _ => "The action could not be completed. Refresh and try again; details are in the application log."
        };
    }
}