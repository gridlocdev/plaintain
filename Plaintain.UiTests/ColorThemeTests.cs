using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Plaintain.Components.Theming;
using Plaintain.Control.Theming;
using Plaintain.Control.Views;
using Plaintain.Desktop.Services;
using Plaintain.Desktop.Views;
using Xunit;

namespace Plaintain.UiTests;

/// <summary>
/// Colour themes: the built-in ones, theme files in the themes folder beside settings.json, and the
/// Preferences choices that pick one for dark mode and one for light.
/// </summary>
public sealed class ColorThemeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"plaintain-themes-{Guid.NewGuid():N}");

    public ColorThemeTests() => Directory.CreateDirectory(Path.Combine(_dir, "themes"));

    public void Dispose()
    {
        Tokens.Current = PlaintainTheme.Default; // a window applying a theme sets the app-wide accent
        Directory.Delete(_dir, recursive: true);
    }

    private string ThemesFolder => Path.Combine(_dir, "themes");

    private void WriteTheme(string file, string json) => File.WriteAllText(Path.Combine(ThemesFolder, file), json);

    /// <summary>Every built-in has its own id, and the defaults are what the surface has always worn.</summary>
    [AvaloniaFact]
    public void The_built_in_themes_have_unique_ids_and_start_with_the_defaults()
    {
        var ids = EditorTheme.BuiltIn.Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());

        var library = new ThemeLibrary(null);
        Assert.Equal(EditorTheme.DefaultDarkId, library.ThemesFor(dark: true)[0].Id);
        Assert.Equal(EditorTheme.DefaultLightId, library.ThemesFor(dark: false)[0].Id);
        Assert.Same(EditorColors.DefaultDark, library.Resolve(null, dark: true).Colors);
    }

    /// <summary>
    /// The text has to be readable on every built-in: WCAG's 4.5:1 for body text, and the line numbers
    /// 3:1, which is what UI text that isn't the content needs.
    /// </summary>
    [AvaloniaFact]
    public void Every_built_in_theme_keeps_its_text_readable()
    {
        foreach (var theme in EditorTheme.BuiltIn)
        {
            var c = theme.Colors;
            Assert.True(Contrast(c.Text, c.Background) >= 4.5, $"{theme.Name}: text is {Contrast(c.Text, c.Background):F1}:1");
            Assert.True(Contrast(c.GutterText, c.GutterBackground) >= 3, $"{theme.Name}: line numbers are {Contrast(c.GutterText, c.GutterBackground):F1}:1");
            Assert.Equal(theme.IsDark, Luminance(c.Background) < 0.2);
        }
    }

    /// <summary>
    /// A theme file sets only the colours it names. The rest come from the default for its appearance,
    /// except that the caret and the selection follow its accent. Hand-written files get comments and
    /// trailing commas, and a misspelt colour is reported rather than silently dropped.
    /// </summary>
    [AvaloniaFact]
    public void A_theme_file_fills_in_what_it_leaves_out_and_reports_unknown_colours()
    {
        WriteTheme("dusk.json", """
            {
              // my theme
              "name": "Dusk",
              "base": "dark",
              "colors": { "background": "#202030", "accent": "#FF8800", "backgrund": "#000000", },
            }
            """);

        var library = new ThemeLibrary(ThemesFolder);
        var dusk = Assert.Single(library.Themes, t => t.Name == "Dusk");

        Assert.Equal("file:dusk.json", dusk.Id);
        Assert.True(dusk.IsDark);
        Assert.Equal(Color.Parse("#202030"), dusk.Colors.Background);
        Assert.Equal(EditorColors.DefaultDark.Text, dusk.Colors.Text);
        Assert.Equal(Color.Parse("#FF8800"), dusk.Colors.Caret);
        Assert.Equal(Color.FromArgb(0x40, 0xFF, 0x88, 0x00), dusk.Colors.Selection);
        Assert.Contains(library.Problems, p => p.Contains("dusk.json", StringComparison.Ordinal) && p.Contains("backgrund", StringComparison.Ordinal));
    }

    /// <summary>A bad file is left out and named, and the good ones beside it still load.</summary>
    [AvaloniaFact]
    public void A_bad_theme_file_is_left_out_and_named_without_costing_the_others()
    {
        WriteTheme("no-base.json", """{ "name": "No Base", "colors": {} }""");
        WriteTheme("bad-colour.json", """{ "base": "light", "colors": { "text": "purple-ish" } }""");
        WriteTheme("broken.json", "{ not json");
        WriteTheme("good.json", """{ "base": "light" }""");
        File.WriteAllText(Path.Combine(ThemesFolder, "notes.txt"), "not a theme");

        var library = new ThemeLibrary(ThemesFolder);

        var custom = library.Themes.Where(t => t.Id.StartsWith(ThemeLibrary.FilePrefix, StringComparison.Ordinal)).ToList();
        Assert.Equal("good", Assert.Single(custom).Name); // no name: the file's
        Assert.Equal(3, library.Problems.Count);
        Assert.Contains(library.Problems, p => p.StartsWith("no-base.json", StringComparison.Ordinal) && p.Contains("base", StringComparison.Ordinal));
        Assert.Contains(library.Problems, p => p.StartsWith("bad-colour.json", StringComparison.Ordinal) && p.Contains("\"text\"", StringComparison.Ordinal));
        Assert.Contains(library.Problems, p => p.StartsWith("broken.json", StringComparison.Ordinal));
    }

    /// <summary>
    /// A theme that is gone, or that was edited to the other appearance, falls back to the default rather
    /// than leaving the editor with nothing to paint with.
    /// </summary>
    [AvaloniaFact]
    public void A_missing_theme_or_one_for_the_other_appearance_falls_back_to_the_default()
    {
        var library = new ThemeLibrary(null);

        Assert.Equal(EditorTheme.DefaultDarkId, library.Resolve("file:gone.json", dark: true).Id);
        Assert.Equal(EditorTheme.DefaultLightId, library.Resolve("midnight", dark: false).Id);
        Assert.Equal("midnight", library.Resolve("midnight", dark: true).Id);
    }

    /// <summary>
    /// The window wears the saved themes from its first paint: the editor's colours for each appearance, and
    /// each theme's accent on the chrome.
    /// </summary>
    [AvaloniaFact]
    public void The_window_wears_the_saved_theme_file_and_its_accent()
    {
        WriteTheme("mine.json", """{ "name": "Mine", "base": "dark", "colors": { "background": "#101820", "accent": "#E04080" } }""");
        var settings = new SettingsService(Path.Combine(_dir, "settings.json"));
        settings.Settings.DarkColorTheme = "file:mine.json";
        settings.Settings.LightColorTheme = "paper";

        var window = TestShell.NewWindow(settings);

        Assert.Equal(Color.Parse("#101820"), window.EditorView.Editor.DarkColors.Background);
        Assert.Equal(EditorTheme.BuiltIn.Single(t => t.Id == "paper").Colors, window.EditorView.Editor.LightColors);
        Assert.Equal(Color.Parse("#E04080"), ((ISolidColorBrush)Tokens.Dark.Accent).Color);
        Assert.Equal(Color.Parse("#B5651D"), ((ISolidColorBrush)Tokens.Light.Accent).Color);
    }

    /// <summary>
    /// A new theme repaints the surface in its colours. A shaped row carries its text colour, so the rows
    /// are shaped again rather than reused in the old one.
    /// </summary>
    [AvaloniaFact]
    public void Changing_the_theme_repaints_and_reshapes_the_rows()
    {
        var view = new PlainTextEditorView { Text = "one\ntwo\nthree" };
        var window = new Window { Width = 400, Height = 300, Content = view, RequestedThemeVariant = ThemeVariant.Dark };
        window.Show();
        var editor = view.Editor;
        Render(editor);
        Assert.Equal(EditorColors.DefaultDark.Background, editor.CanvasColorForTest);

        var midnight = EditorTheme.BuiltIn.Single(t => t.Id == "midnight").Colors;
        int shaped = editor.RowsShapedForTest;
        editor.DarkColors = midnight;
        Render(editor);

        Assert.Equal(midnight.Background, editor.CanvasColorForTest);
        Assert.Equal(midnight.Caret, editor.CaretColorForTest);
        Assert.True(editor.RowsShapedForTest >= shaped + 3, "the rows were reused in the old text colour");

        editor.LightColors = EditorTheme.BuiltIn.Single(t => t.Id == "paper").Colors;
        Assert.Equal(midnight.Background, editor.CanvasColorForTest); // still dark mode, so still the dark theme
    }

    /// <summary>Preferences lists each appearance's themes by name, and committing stores the chosen one's id.</summary>
    [AvaloniaFact]
    public void Preferences_offers_a_theme_for_each_appearance_and_remembers_the_choice()
    {
        WriteTheme("mine.json", """{ "name": "Mine", "base": "light" }""");
        var library = new ThemeLibrary(ThemesFolder);
        var settings = new AppSettings();
        var form = MainWindow.BuildSettingsForm(settings, library);
        var window = new Window { Width = 400, Height = 300, Content = form };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var combos = form.GetLogicalDescendants().OfType<ComboBox>().ToList();
        var dark = combos[1];
        var light = combos[2];
        Assert.Equal(["Plaintain Dark", "Midnight", "Ember", "Forest", "High Contrast Dark"], (IEnumerable<string>)dark.ItemsSource!);
        Assert.Equal(["Plaintain Light", "Paper", "Mist", "High Contrast Light", "Mine"], (IEnumerable<string>)light.ItemsSource!);
        Assert.Equal(0, dark.SelectedIndex);

        dark.SelectedIndex = 2;
        light.SelectedIndex = 4;
        form.Commit();

        Assert.Equal("ember", settings.DarkColorTheme);
        Assert.Equal("file:mine.json", settings.LightColorTheme);
    }

    private static void Render(Plaintain.Control.Controls.PlainTextEditor editor)
    {
        editor.UpdateLayout();
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(400, 300));
        using var context = bitmap.CreateDrawingContext();
        editor.Render(context);
    }

    private static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }
}
