using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalVibrance.Core;

namespace DigitalVibrance.Services;

/// <summary>Loads and saves <see cref="AppConfig"/> as JSON under %AppData%\DigitalVibrance.</summary>
public static class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Directory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DigitalVibrance");

    public static string ConfigPath => Path.Combine(Directory, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new AppConfig();
            string json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, Options) ?? new AppConfig();
            Normalize(config);
            return config;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt config should not stop the app from starting; keep a copy and move on.
            TryBackupCorruptConfig();
            return new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);

            // Write to a temp file first so a crash mid-write cannot leave a half-written config.
            string temp = ConfigPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(config, Options));
            File.Move(temp, ConfigPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing a settings write is not worth crashing over.
        }
    }

    /// <summary>
    /// Repairs anything the deserializer could not guarantee — missing sub-objects and slider
    /// values that are out of range or inconsistent with the override flag.
    /// </summary>
    private static void Normalize(AppConfig config)
    {
        if (config.Desktop is null) config.Desktop = new ColorProfile();
        config.Desktop.Normalize();

        if (config.Games is null)
        {
            config.Games = new List<GameProfile>();
            return;
        }

        // Entries without an executable can never match a process, so they would be dead weight.
        config.Games.RemoveAll(g => g is null || string.IsNullOrWhiteSpace(g.ExecutablePath));

        foreach (var game in config.Games)
        {
            if (game.Color is null) game.Color = new ColorProfile();
            game.Color.Normalize();
        }
    }

    private static void TryBackupCorruptConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
                File.Move(ConfigPath, ConfigPath + ".corrupt", overwrite: true);
        }
        catch
        {
            // ignored
        }
    }
}
