using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Media;
using Plaintain.Control.Theming;

namespace Plaintain.Desktop.Services;

/// <summary>
/// The colour themes Preferences offers: the built-in ones, then any theme files in the themes folder
/// (a <c>themes</c> folder beside settings.json). A file that can't be read is left out and reported in
/// <see cref="Problems"/>, so one bad file never costs the rest. See docs/themes.md for the file format.
/// </summary>
public sealed class ThemeLibrary
{
    /// <summary>The prefix of a theme file's id, so it can never collide with a built-in's.</summary>
    public const string FilePrefix = "file:";

    private List<EditorTheme> _themes = [.. EditorTheme.BuiltIn];
    private List<string> _problems = [];

    public ThemeLibrary(string? folder)
    {
        Folder = folder;
        Reload();
    }

    /// <summary>Where theme files are read from, or null for built-ins only.</summary>
    public string? Folder { get; }

    /// <summary>Every theme, built-ins first, then theme files by name.</summary>
    public IReadOnlyList<EditorTheme> Themes => _themes;

    /// <summary>One line per theme file that was left out or partly read, naming the file and the reason.</summary>
    public IReadOnlyList<string> Problems => _problems;

    /// <summary>The themes for one appearance, in the order Preferences lists them.</summary>
    public IReadOnlyList<EditorTheme> ThemesFor(bool dark) => [.. _themes.Where(t => t.IsDark == dark)];

    /// <summary>Read the folder again, so a file added since the last read shows up.</summary>
    public void Reload()
    {
        var themes = new List<EditorTheme>(EditorTheme.BuiltIn);
        var problems = new List<string>();
        if (Folder is not null && Directory.Exists(Folder))
        {
            var files = Directory.GetFiles(Folder, "*.json").Order(StringComparer.OrdinalIgnoreCase);
            var custom = new List<EditorTheme>();
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                try
                {
                    custom.Add(Parse(File.ReadAllText(file), name, problems));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    problems.Add($"{name}: couldn't be read ({ex.Message})");
                }
                catch (ThemeFileException ex)
                {
                    problems.Add($"{name}: {ex.Message}");
                }
            }

            themes.AddRange(custom.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase));
        }

        _themes = themes;
        _problems = problems;
    }

    /// <summary>
    /// The theme <paramref name="id"/> names, if it exists and is for the right appearance; otherwise the
    /// default for that appearance. A theme file that was deleted, or edited to the other appearance,
    /// falls back rather than leaving the editor with no colours.
    /// </summary>
    public EditorTheme Resolve(string? id, bool dark) =>
        _themes.FirstOrDefault(t => t.IsDark == dark && string.Equals(t.Id, id, StringComparison.Ordinal))
        ?? _themes.First(t => t.Id == (dark ? EditorTheme.DefaultDarkId : EditorTheme.DefaultLightId));

    /// <summary>
    /// Read one theme file. Colours it leaves out come from the default theme for its appearance, except
    /// that the selection and the caret follow its accent when it sets one. Unknown colour names are
    /// reported in <paramref name="problems"/> but don't stop the file loading.
    /// </summary>
    internal static EditorTheme Parse(string json, string fileName, List<string> problems)
    {
        ThemeFile? file;
        try
        {
            file = JsonSerializer.Deserialize(json, ThemeJsonContext.Default.ThemeFile);
        }
        catch (JsonException ex)
        {
            throw new ThemeFileException($"isn't valid JSON ({ex.Message})");
        }

        if (file is null)
        {
            throw new ThemeFileException("is empty");
        }

        bool dark = file.Base?.Trim().ToUpperInvariant() switch
        {
            "DARK" => true,
            "LIGHT" => false,
            _ => throw new ThemeFileException("needs \"base\": \"dark\" or \"light\""),
        };

        var colors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in file.Colors ?? [])
        {
            if (!ColorNames.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add($"{fileName}: ignored unknown colour \"{key}\"");
                continue;
            }

            if (value is null || !Color.TryParse(value, out var color))
            {
                throw new ThemeFileException($"\"{key}\" isn't a colour (use #RRGGBB, or #AARRGGBB with the alpha first)");
            }

            colors[key] = color;
        }

        var d = EditorColors.DefaultFor(dark);
        Color Pick(string key, Color fallback) => colors.TryGetValue(key, out var c) ? c : fallback;
        var accent = Pick("accent", d.Accent);
        var result = new EditorColors(
            Background: Pick("background", d.Background),
            Text: Pick("text", d.Text),
            GutterBackground: Pick("gutterBackground", d.GutterBackground),
            GutterText: Pick("gutterText", d.GutterText),
            CurrentLine: Pick("currentLine", d.CurrentLine),
            Selection: Pick("selection", Color.FromArgb(0x40, accent.R, accent.G, accent.B)),
            Caret: Pick("caret", accent),
            Accent: accent,
            FindMatch: Pick("findMatch", d.FindMatch));

        string name = string.IsNullOrWhiteSpace(file.Name) ? Path.GetFileNameWithoutExtension(fileName) : file.Name.Trim();
        return new EditorTheme(FilePrefix + fileName, name, dark, result);
    }

    /// <summary>The colour names a theme file may set, as docs/themes.md lists them.</summary>
    internal static readonly string[] ColorNames =
        ["background", "text", "gutterBackground", "gutterText", "currentLine", "selection", "caret", "accent", "findMatch"];

    private sealed class ThemeFileException(string message) : Exception(message);
}

/// <summary>A theme file as written. Everything optional here; <see cref="ThemeLibrary.Parse"/> decides what's required.</summary>
internal sealed class ThemeFile
{
    public string? Name { get; set; }

    public string? Base { get; set; }

    public Dictionary<string, string?>? Colors { get; set; }
}

/// <summary>
/// Source-generated JSON for theme files, so they load in a Native AOT build. Lenient on purpose: people
/// write these by hand, so comments and trailing commas are allowed and property names ignore case.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(ThemeFile))]
internal sealed partial class ThemeJsonContext : JsonSerializerContext;
