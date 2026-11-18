using Avalonia;
using Avalonia.Media;
using Plaintain.Desktop.Services;
using Puffinote.Components.Controls;
using Puffinote.Components.Overlays;
using Puffinote.Components.Shell;
using Puffinote.Components.Theming;

namespace Plaintain.Desktop.Views;

public sealed partial class MainWindow
{
    /// <summary>
    /// The Preferences dialog — every option in Plaintain's <c>settings.json</c> that is a preference
    /// rather than a record of what you did (the recent-files list is the latter, and File ▸ Open Recent
    /// is where it belongs). The form is the shared <see cref="SettingsEditorView"/>, so this reads the
    /// same as every other app in the family; what stays here is which settings exist and what applying
    /// one means for the live editor.
    /// </summary>
    private async void ShowSettings()
    {
        var s = _settings.Settings;
        Themes.Reload(); // pick up a theme file added since the window opened
        var form = BuildSettingsForm(s, Themes);

        if (!await _overlay.ShowModalAsync(new FormDialog("Preferences", form)))
        {
            return; // Cancel / Esc — leave settings untouched
        }

        form.Commit();
        ApplySettings(s);
        _settings.Save();
    }

    /// <summary>The Preferences form over <paramref name="s"/> (also the seam the UI tests drive).</summary>
    internal static SettingsEditorView BuildSettingsForm(AppSettings s, ThemeLibrary themes) => new(
    [
        new SettingsSection("Appearance",
        [
            ChoiceSetting.ForEnum<ThemePreference>("Theme", () => s.Theme, v => s.Theme = v,
                ThemePreferences.Labels, ThemePreferences.Hint),
            ColorThemeChoice("Dark theme", themes, dark: true, () => s.DarkColorTheme, v => s.DarkColorTheme = v),
            ColorThemeChoice("Light theme", themes, dark: false, () => s.LightColorTheme, v => s.LightColorTheme = v),
        ]),
        new SettingsSection("Editor",
        [
            new ToggleSetting("Wrap long lines at words", () => s.WordWrap, v => s.WordWrap = v,
                "Off is the Notepad default: a long line runs off the right edge instead of folding. On, it folds at the window's edge between words, so only a word too long for a whole row is ever split."),
            new ToggleSetting("Show line numbers", () => s.ShowLineNumbers, v => s.ShowLineNumbers = v),
            new ToggleSetting("Show invisibles", () => s.ShowInvisibles, v => s.ShowInvisibles = v,
                "Mark tabs as →, spaces at the end of a line as · and CRLF line endings as ¤, in the gutter's muted ink."),
            new ToggleSetting("Highlight the current line", () => s.HighlightCurrentLine, v => s.HighlightCurrentLine = v,
                "A faint tint across the row the caret is on. On a log or an aligned table it lands on the data rather than on the caret, so it can be turned off; the gutter still marks the line either way."),
            new ToggleSetting("Highlight other occurrences of the selection", () => s.HighlightSelectionOccurrences, v => s.HighlightSelectionOccurrences = v,
                "Select a word and every other place it appears is tinted, faintly, in the same colour — which is also what ⌘D takes next. In a file where the selection is everywhere, that lights up half the page, so it can be turned off."),
            new NumberSetting("Font size", () => s.FontSize, v => s.FontSize = v, 8, 48, step: 1, decimals: 1,
                hint: "Point size of the editor text."),
        ]),
        new SettingsSection("Saving",
        [
            AutoSave.IntervalField(() => s.AutoSaveSeconds, v => s.AutoSaveSeconds = v),
            new NumberSetting("Recovery snapshots", () => s.RecoverySnapshotSeconds, v => s.RecoverySnapshotSeconds = (int)v, 0, 600, step: 5,
                hint: "Seconds between sidecar copies of an edited buffer, offered back if Plaintain closes without saving. 0 turns them off. Unlike auto-save, this covers an untitled buffer and a file whose encoding is only a guess — the two it refuses to write."),
        ]),
    ]);

    /// <summary>
    /// A drop-down over the colour themes for one appearance. Its hint says where theme files go and names
    /// any file that couldn't be read, since Preferences is where someone looks for a theme that didn't show up.
    /// </summary>
    private static ChoiceSetting ColorThemeChoice(string label, ThemeLibrary themes, bool dark, Func<string> get, Action<string> set)
    {
        var options = themes.ThemesFor(dark);
        string hint = dark
            ? "The colours worn in dark mode. The light theme below is worn in light mode."
            : "The colours worn in light mode.";
        // Where theme files go, and which ones didn't load, said once: under the second of the two choices.
        if (!dark && themes.Folder is { } folder)
        {
            hint += $" Add your own as JSON files in {folder} (see docs/themes.md). Preferences reads them each time it opens.";
        }

        if (!dark && themes.Problems.Count > 0)
        {
            hint += " Not loaded: " + string.Join("; ", themes.Problems) + ".";
        }

        string current = themes.Resolve(get(), dark).Id;
        return new ChoiceSetting(
            label,
            [.. options.Select(t => t.Name)],
            () => Math.Max(0, options.ToList().FindIndex(t => t.Id == current)),
            index => set(options[Math.Clamp(index, 0, options.Count - 1)].Id),
            hint);
    }

    /// <summary>Push the committed settings into the live editor.</summary>
    private void ApplySettings(AppSettings s)
    {
        ApplyTheme(s);

        // WordWrap, ShowLineNumbers and ShowInvisibles persist themselves through the editor's PropertyChanged hook
        // (see the constructor); assigning them here is what makes the change visible.
        EditorView.Editor.WordWrap = s.WordWrap;
        EditorView.Editor.ShowLineNumbers = s.ShowLineNumbers;
        EditorView.Editor.ShowInvisibles = s.ShowInvisibles;
        EditorView.Editor.HighlightCurrentLine = s.HighlightCurrentLine;
        EditorView.Editor.HighlightSelectionOccurrences = s.HighlightSelectionOccurrences;
        EditorView.Editor.FontSize = s.FontSize;
        _autoSave.IntervalSeconds = s.AutoSaveSeconds; // takes effect on the document already open
        SetupRecoverySnapshots(); // pick up the new interval (or stop, when it is 0)
    }

    /// <summary>
    /// Wear <paramref name="theme"/>: request the variant, which re-resolves Fluent's control themes and
    /// the family's chrome tokens together, and repaint the editing surface, which draws itself and so
    /// isn't reached by either. Called as the window opens and again whenever Preferences commits, so the
    /// choice lands on the window in front of you rather than on the next one you open.
    /// </summary>
    private void ApplyTheme(AppSettings s)
    {
        var dark = Themes.Resolve(s.DarkColorTheme, dark: true);
        var light = Themes.Resolve(s.LightColorTheme, dark: false);
        EditorView.Editor.DarkColors = dark.Colors;
        EditorView.Editor.LightColors = light.Colors;

        // The chrome (dialogs, buttons, focus rings) takes each theme's accent. Set before the variant, so
        // the retint the variant change triggers already has the new accent.
        Tokens.Current = new AccentTheme(light: light.Colors.Accent, dark: dark.Colors.Accent);
        if (Application.Current is { } app)
        {
            s.Theme.Apply(app);
        }

        EditorView.Editor.InvalidateVisual();
    }

    /// <summary>The colour themes Preferences offers (built-ins plus theme files).</summary>
    internal ThemeLibrary Themes { get; }

    /// <summary>The family's chrome palette with a separate accent for light and dark.</summary>
    private sealed class AccentTheme(Color light, Color dark) : IAppTheme
    {
        public ThemePalette Light { get; } = new PuffinoteTheme(light).Light;

        public ThemePalette Dark { get; } = new PuffinoteTheme(dark).Dark;
    }
}
