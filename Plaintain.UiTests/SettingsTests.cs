using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Plaintain.Desktop.Services;
using Plaintain.Desktop.Views;
using Puffinote.Components.Theming;
using Xunit;

namespace Plaintain.UiTests;

/// <summary>
/// Preferences: the menu affordance, and the form over what Plaintain actually keeps in its settings file.
/// </summary>
public class SettingsTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"plaintain-settings-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        File.Delete(_path);
        GC.SuppressFinalize(this);
    }

    private MainWindow Open()
    {
        var window = TestShell.NewWindow(new SettingsService(_path));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>
    /// The form scrolls, and its scroll bar sits in the card's padding beside the rows, not on top of the
    /// dropdowns' right edge, which is where Avalonia's overlay bar lands when the content fills the scroller.
    /// </summary>
    [AvaloniaFact]
    public void The_preferences_scroll_bar_does_not_cover_the_rows()
    {
        var settings = new SettingsService(_path);
        var form = MainWindow.BuildSettingsForm(settings.Settings, new ThemeLibrary(null));
        // Padded and centred as OverlayHost lays out a dialog, so the card shrinks to the form's width.
        var card = new Border
        {
            Padding = new Thickness(24),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Child = form,
        };
        var window = new Window { Width = 800, Height = 400, Content = card };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var scroller = form.GetVisualDescendants().OfType<ScrollViewer>().First();
        var rows = (Avalonia.Controls.Control)scroller.Content!;
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height, "the form is long enough to scroll");

        var bar = scroller.GetVisualDescendants().OfType<ScrollBar>().Single(b => b.Orientation == Avalonia.Layout.Orientation.Vertical && ReferenceEquals(b.TemplatedParent, scroller));
        double rowsRight = rows.TranslatePoint(new Point(rows.Bounds.Width, 0), card)!.Value.X;
        double barLeft = bar.TranslatePoint(new Point(0, 0), card)!.Value.X;
        double cardContentRight = card.Bounds.Width; // the bar may use the padding, but not leave the card

        Assert.True(bar.IsVisible && bar.Bounds.Width > 0, "the bar is laid out");
        Assert.True(barLeft >= rowsRight, $"the bar starts at {barLeft}, inside the rows, which end at {rowsRight}");
        Assert.True(barLeft + bar.Bounds.Width <= cardContentRight, "the bar stays inside the card");
        Assert.Equal(form.FormWidth, rows.Bounds.Width); // the rows keep the width they were laid out to
    }

    [AvaloniaFact]
    public void The_File_menu_offers_Preferences_on_the_platform_chord()
    {
        var window = Open();

        // On macOS the File menu lives in the system bar, which a headless test cannot walk; the chord is
        // the part that holds either way, and MenuBarTests covers where the menu ended up.
        if (!OperatingSystem.IsMacOS())
        {
            var file = window.GetLogicalDescendants().OfType<MenuItem>().First(m => (string?)m.Header == "_File");
            var preferences = file.Items.OfType<MenuItem>().Single(m => (string?)m.Header == "_Preferences…");
            Assert.Equal(Key.OemComma, preferences.InputGesture!.Key);
        }

        Assert.Contains(window.KeyBindings, b => b.Gesture.Key == Key.OemComma);
    }

    [AvaloniaFact]
    public void The_form_covers_every_preference_in_the_settings_file()
    {
        var settings = new AppSettings { WordWrap = true, ShowLineNumbers = false, FontSize = 18 };
        var form = MainWindow.BuildSettingsForm(settings, new ThemeLibrary(null));

        Assert.Equal(
            ["Theme", "Dark theme", "Light theme", "Wrap long lines at words", "Show line numbers", "Highlight the current line",
             "Highlight other occurrences of the selection", "Font size", "Auto-save", "Recovery snapshots"],
            form.Fields.Select(f => f.Label));
    }

    /// <summary>
    /// The current-line tint is on until someone says otherwise — this setting exists so it can be turned
    /// off, not to change what the editor does by default — and the answer reaches the open document.
    /// </summary>
    [AvaloniaFact]
    public void The_current_line_highlight_is_on_by_default_and_can_be_turned_off()
    {
        Assert.True(new AppSettings().HighlightCurrentLine);
        Assert.True(Open().EditorView.Editor.HighlightCurrentLine);

        var service = new SettingsService(_path);
        service.Settings.HighlightCurrentLine = false;
        service.Save();

        var window = TestShell.NewWindow(new SettingsService(_path)); // i.e. the next launch
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.EditorView.Editor.HighlightCurrentLine);
    }

    /// <summary>
    /// Same shape for the occurrence tint: on until someone says otherwise, and the answer survives a
    /// restart and reaches the open document rather than only the next one.
    /// </summary>
    [AvaloniaFact]
    public void The_selection_occurrence_highlight_is_on_by_default_and_can_be_turned_off()
    {
        Assert.True(new AppSettings().HighlightSelectionOccurrences);
        Assert.True(Open().EditorView.Editor.HighlightSelectionOccurrences);

        var service = new SettingsService(_path);
        service.Settings.HighlightSelectionOccurrences = false;
        service.Save();

        var window = TestShell.NewWindow(new SettingsService(_path)); // i.e. the next launch
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.EditorView.Editor.HighlightSelectionOccurrences);
    }

    /// <summary>
    /// The appearance is a preference like any other: unset it follows the OS, and choosing one is written
    /// to the file so the next launch opens in it. The drop-down's options are the family's three, in the
    /// order the shared enum declares them — which is the order an index maps back to a value in.
    /// </summary>
    [AvaloniaFact]
    public void The_theme_can_be_chosen_and_is_remembered()
    {
        var settings = new AppSettings();
        Assert.Equal(ThemePreference.System, settings.Theme); // nobody has said otherwise yet

        var form = MainWindow.BuildSettingsForm(settings, new ThemeLibrary(null));
        var window = new Window { Width = 400, Height = 300, Content = form };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var theme = form.GetLogicalDescendants().OfType<ComboBox>().First();
        Assert.Equal(ThemePreferences.Labels, theme.ItemsSource);
        theme.SelectedIndex = (int)ThemePreference.Light;

        Assert.Equal(ThemePreference.System, settings.Theme); // nothing written until the dialog is accepted
        form.Commit();
        Assert.Equal(ThemePreference.Light, settings.Theme);

        var service = new SettingsService(_path);
        service.Settings.Theme = ThemePreference.Light;
        service.Save();
        Assert.Equal(ThemePreference.Light, new SettingsService(_path).Settings.Theme); // i.e. the next launch
    }

    [AvaloniaFact]
    public void Committing_the_form_writes_the_edited_values_back()
    {
        var settings = new AppSettings();
        var form = MainWindow.BuildSettingsForm(settings, new ThemeLibrary(null));
        var window = new Window { Width = 400, Height = 300, Content = form };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var boxes = form.GetLogicalDescendants().OfType<CheckBox>().ToList();
        boxes[0].IsChecked = true;   // wrap
        boxes[1].IsChecked = false;  // line numbers
        var numbers = form.GetLogicalDescendants().OfType<NumericUpDown>().ToList();
        numbers[0].Value = 20m;  // font size
        numbers[1].Value = 60m;  // auto-save, once a minute

        Assert.False(settings.WordWrap); // nothing written until the dialog is accepted
        form.Commit();

        Assert.True(settings.WordWrap);
        Assert.False(settings.ShowLineNumbers);
        Assert.Equal(20, settings.FontSize);
        Assert.Equal(60, settings.AutoSaveSeconds);
    }

    [AvaloniaFact]
    public void Preferences_that_survive_a_restart_come_back_on_the_editor()
    {
        var service = new SettingsService(_path);
        service.Settings.WordWrap = true;
        service.Settings.FontSize = 22;
        service.Save();

        var window = Open(); // a fresh window over the same file — i.e. the next launch
        Assert.True(window.EditorView.Editor.WordWrap);
        Assert.Equal(22, window.EditorView.Editor.FontSize);
    }

    [AvaloniaFact]
    public void A_settings_file_written_by_an_earlier_build_still_loads()
    {
        // The format the reflection-based serializer wrote, hand-edits included: enum names in any case, an
        // enum as a number, and a key this build doesn't know. The source-generated serializer (there for
        // Native AOT) has to read all of it, or an upgrade resets everyone's preferences.
        File.WriteAllText(_path, """
            {
              "Theme": "dark",
              "WordWrap": true,
              "FontSize": 18,
              "RecentFiles": ["/tmp/a.log", "/tmp/b.txt"],
              "SomethingFromTheFuture": 1
            }
            """);
        var settings = new SettingsService(_path).Settings;
        Assert.Equal(ThemePreference.Dark, settings.Theme);
        Assert.True(settings.WordWrap);
        Assert.Equal(18, settings.FontSize);
        Assert.Equal(["/tmp/a.log", "/tmp/b.txt"], settings.RecentFiles);

        File.WriteAllText(_path, $$"""{ "Theme": {{(int)ThemePreference.Light}} }""");
        Assert.Equal(ThemePreference.Light, new SettingsService(_path).Settings.Theme);
    }

    [AvaloniaFact]
    public void A_settings_file_without_recent_files_still_gets_an_empty_list()
    {
        // Every key is optional: a file from before recent files existed, or one someone trimmed by hand.
        File.WriteAllText(_path, """{ "Theme": "Dark" }""");
        var settings = new SettingsService(_path).Settings;

        Assert.Empty(settings.RecentFiles);
        settings.PushRecent("/tmp/a.log");
        Assert.Equal(["/tmp/a.log"], settings.RecentFiles);
    }

    [AvaloniaFact]
    public void Saved_settings_write_enums_by_name()
    {
        var service = new SettingsService(_path);
        service.Settings.Theme = ThemePreference.Dark;
        service.Save();

        Assert.Contains("\"Theme\": \"Dark\"", File.ReadAllText(_path), StringComparison.Ordinal);
    }
}
