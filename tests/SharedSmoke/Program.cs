using System;
using System.IO;
using SoundReplacer.Shared;

/// <summary>
/// Дымовые тесты общего слоя (config.json, catalog.json, мини-JSON).
/// Запускаются в CI: dotnet run --project tests/SharedSmoke -c Release.
/// Ненулевой код возврата = есть падения.
/// </summary>
internal static class Smoke
{
    private static int _failures;

    private static void Main()
    {
        ConfigRoundTrip();
        CatalogRoundTrip();
        BrokenJson();
        Escapes();
        FileSaveLoad();
        PathResolution();

        Console.WriteLine(_failures == 0 ? "ALL OK" : "FAILURES: " + _failures);
        Environment.Exit(_failures == 0 ? 0 : 1);
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("ok   " + name);
            return;
        }

        _failures++;
        Console.WriteLine("FAIL " + name);
    }

    private static void ConfigRoundTrip()
    {
        var config = new ReplacerConfig();
        config.Sounds["Sfx - Item Break"] = "Sounds/break_0123456789abcdef.ogg";
        config.Sounds["Крик игрока"] = "Sounds/крик.wav";
        config.PatchClipSetter = false;
        config.MatchVolume = false;
        config.LogPlays = true;
        config.NoTrim.Add("extraction point jingle complete 2 local");
        config.OncePerFile = true;
        config.OnceWindowSeconds = 3.5;
        config.FadeOutSeconds = 1.5;

        string error;
        ReplacerConfig parsed = ReplacerConfig.FromJson(config.ToJson(), out error);

        Check(parsed != null, "config parsed (" + error + ")");
        if (parsed == null) return;

        Check(parsed.Sounds.Count == 2, "config sounds count");
        Check(parsed.Sounds["Sfx - Item Break"] == "Sounds/break_0123456789abcdef.ogg", "config ascii mapping");
        Check(parsed.Sounds["Крик игрока"] == "Sounds/крик.wav", "config cyrillic mapping");
        Check(parsed.PatchClipSetter == false, "config patchClipSetter");
        Check(parsed.Version == ReplacerConfig.CurrentVersion, "config version");
        Check(parsed.MatchLength, "config matchLength default true");
        Check(parsed.MatchChannels, "config matchChannels default true");
        Check(parsed.MatchVolume == false, "config matchVolume respected");
        Check(parsed.LogPlays, "config logPlays");
        Check(parsed.NoTrim.Count == 1 && parsed.NoTrim[0] == "extraction point jingle complete 2 local", "config noTrim");
        Check(parsed.OncePerFile, "config oncePerFile");
        Check(Math.Abs(parsed.OnceWindowSeconds - 3.5) < 0.001, "config onceWindowSeconds");
        Check(Math.Abs(parsed.FadeOutSeconds - 1.5) < 0.001, "config fadeOutSeconds");
    }

    private static void CatalogRoundTrip()
    {
        var catalog = new SoundCatalog
        {
            GeneratedUtc = DateTime.UtcNow.ToString("o"),
            GameVersion = "0.1.0",
            UnityVersion = "2022.3.62f1",
            ModVersion = "1.0.0"
        };
        catalog.Sounds.Add(new SoundCatalogEntry
        {
            Name = "Sfx - Item Break",
            Plays = 12,
            Length = 2.35f,
            Channels = 1,
            LastSeenUtc = DateTime.UtcNow.ToString("o")
        });
        catalog.Sounds.Add(new SoundCatalogEntry
        {
            Name = "Меню / клик",
            Plays = 3,
            LastSeenUtc = DateTime.UtcNow.ToString("o")
        });

        string error;
        SoundCatalog parsed = SoundCatalog.FromJson(catalog.ToJson(), out error);

        Check(parsed != null, "catalog parsed (" + error + ")");
        if (parsed == null) return;

        Check(parsed.Sounds.Count == 2, "catalog count");
        Check(parsed.Sounds[0].Name == "Sfx - Item Break", "catalog sorted by plays");
        Check(parsed.Sounds[0].Plays == 12, "catalog plays");
        Check(Math.Abs(parsed.Sounds[0].Length - 2.35f) < 0.001f, "catalog length");
        Check(parsed.Sounds[0].Channels == 1, "catalog channels");
        Check(parsed.GameVersion == "0.1.0", "catalog game version");
    }

    private static void BrokenJson()
    {
        string error;
        ReplacerConfig broken = ReplacerConfig.FromJson("{ \"sounds\": { \"a\": }", out error);
        Check(broken == null && !string.IsNullOrEmpty(error), "broken json rejected");

        ReplacerConfig rootArray = ReplacerConfig.FromJson("[1,2,3]", out error);
        Check(rootArray == null && !string.IsNullOrEmpty(error), "array root rejected");

        ReplacerConfig badSounds = ReplacerConfig.FromJson("{ \"sounds\": [] }", out error);
        Check(badSounds == null && !string.IsNullOrEmpty(error), "sounds array rejected");

        object value;
        Check(!Json.TryParse("{\"a\":1} trailing", out value, out error), "trailing garbage rejected");
    }

    private static void Escapes()
    {
        var config = new ReplacerConfig();
        config.Sounds["quote\"backslash\\tab\ttab"] = "Sounds/a b.ogg";

        string error;
        ReplacerConfig parsed = ReplacerConfig.FromJson(config.ToJson(), out error);

        Check(parsed != null, "escape config parsed (" + error + ")");
        if (parsed == null) return;

        Check(parsed.Sounds.ContainsKey("quote\"backslash\\tab\ttab"), "escape round trip");
        Check(parsed.Sounds["quote\"backslash\\tab\ttab"] == "Sounds/a b.ogg", "path with space preserved");
    }

    private static void FileSaveLoad()
    {
        string directory = CreateTempDirectory();

        try
        {
            string path = Path.Combine(directory, "config.json");

            var config = new ReplacerConfig();
            config.Sounds["A"] = "Sounds/a.ogg";

            string error;
            Check(config.Save(path, out error), "save config (" + error + ")");

            ReplacerConfig loaded;
            Check(ReplacerConfig.TryLoad(path, out loaded, out error) && loaded.Sounds.Count == 1,
                "load config (" + error + ")");
            Check(!File.Exists(path + ".tmp"), "no temp file left");

            string missingError;
            Check(!ReplacerConfig.TryLoad(Path.Combine(directory, "nope.json"), out loaded, out missingError),
                "missing file reported");
        }
        finally
        {
            DeleteQuietly(directory);
        }
    }

    private static void PathResolution()
    {
        string directory = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "a.ogg"), "OggS");

            string absolute;
            string error;

            Check(ReplacerConfig.TryResolvePath(directory, "a.ogg", out absolute, out error) && File.Exists(absolute),
                "relative path resolved");
            Check(ReplacerConfig.TryResolvePath(directory, Path.Combine(directory, "a.ogg"), out absolute, out error),
                "absolute path resolved");
            Check(!ReplacerConfig.TryResolvePath(directory, "missing.ogg", out absolute, out error) &&
                  !string.IsNullOrEmpty(error), "missing path reported");
            Check(!ReplacerConfig.TryResolvePath(directory, "   ", out absolute, out error), "empty path rejected");
        }
        finally
        {
            DeleteQuietly(directory);
        }
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "soundreplacer-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        catch
        {
            // временная папка — не критично
        }
    }
}