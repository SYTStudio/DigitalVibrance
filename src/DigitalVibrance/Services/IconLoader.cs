using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DigitalVibrance.Interop;

namespace DigitalVibrance.Services;

/// <summary>Pulls the best available icon out of a game executable for the grid cards.</summary>
public static class IconLoader
{
    private const int PreferredSize = 128;

    public static ImageSource? Load(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            return null;

        // PrivateExtractIcons returns the closest match to the requested size, so asking for a
        // large one gets the 256px asset from modern game executables instead of a blurry 32px.
        var handles = new IntPtr[1];
        var ids = new int[1];
        int count = NativeMethods.PrivateExtractIcons(
            executablePath, 0, PreferredSize, PreferredSize, handles, ids, 1, 0);

        if (count <= 0 || handles[0] == IntPtr.Zero) return null;

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                handles[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze(); // shared across threads and never mutated
            return source;
        }
        catch (Exception ex) when (ex is ArgumentException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            NativeMethods.DestroyIcon(handles[0]);
        }
    }

    /// <summary>Best-effort friendly name for a newly added executable.</summary>
    public static string GuessName(string executablePath)
    {
        try
        {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(executablePath);
            string? product = info.ProductName?.Trim();
            if (!string.IsNullOrWhiteSpace(product)) return product;

            string? description = info.FileDescription?.Trim();
            if (!string.IsNullOrWhiteSpace(description)) return description;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // fall through to the file name
        }

        return Path.GetFileNameWithoutExtension(executablePath);
    }
}
