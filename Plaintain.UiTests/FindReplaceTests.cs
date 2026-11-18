using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Plaintain.Control.Views;
using Xunit;

namespace Plaintain.UiTests;

/// <summary>The replace row of the find strip: one match at a time, or every match as one undoable edit.</summary>
public class FindReplaceTests
{
    private static PlainTextEditorView Open(string text)
    {
        var view = new PlainTextEditorView { Text = text };
        var window = new Window { Width = 500, Height = 300, Content = view };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    private static PlainTextEditorView Replacing(string text, string query, string replacement)
    {
        var view = Open(text);
        view.ShowReplace();
        view.Find.QueryBox.Text = query;
        view.Find.ReplaceBox.Text = replacement;
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    [AvaloniaFact]
    public void Find_and_replace_is_a_command_that_opens_the_strip_with_its_replace_row()
    {
        var view = Open("one two");
        var command = view.EditingCommands.Single(c => c.Id == "edit.replace");
        command.Execute();
        Assert.True(view.Find.IsVisible);
        Assert.True(view.Find.IsReplacing);

        view.ShowFind();
        Assert.False(view.Find.IsReplacing); // ⌘F goes back to the one-line strip
    }

    [AvaloniaFact]
    public void Replace_changes_the_current_match_and_moves_to_the_next()
    {
        var view = Replacing("cat dog cat", "cat", "bird");
        Assert.Equal(0, view.CurrentMatch);

        view.ReplaceCurrent();

        Assert.Equal("bird dog cat", view.Text);
        Assert.Single(view.Matches);
        Assert.Equal(9, view.Editor.Buffer.Selection.Start); // the remaining "cat" is selected
        Assert.Equal(12, view.Editor.Buffer.Selection.End);
    }

    [AvaloniaFact]
    public void Replace_does_not_find_its_own_output()
    {
        var view = Replacing("a b a", "a", "aa");

        view.ReplaceCurrent();
        view.ReplaceCurrent();

        Assert.Equal("aa b aa", view.Text);
    }

    [AvaloniaFact]
    public void Replace_only_selects_the_match_when_the_caret_has_moved_away()
    {
        var view = Replacing("cat dog cat", "cat", "bird");
        view.Editor.Buffer.Selection = Puffinote.Text.Document.Selection.At(5);

        view.ReplaceCurrent();

        Assert.Equal("cat dog cat", view.Text);
        Assert.Equal(0, view.Editor.Buffer.Selection.Start);
        Assert.Equal(3, view.Editor.Buffer.Selection.End);
    }

    [AvaloniaFact]
    public void Replace_after_an_edit_searches_again_instead_of_using_old_offsets()
    {
        var view = Replacing("cat dog cat", "cat", "bird");
        view.Editor.Buffer.ReplaceRange(0, 0, "xx", 0); // the matches now point two characters early

        view.ReplaceCurrent();

        Assert.Equal("xxcat dog cat", view.Text);
        Assert.Equal([2, 10], view.Matches.Select(m => m.Start));
    }

    [AvaloniaFact]
    public void Enter_in_the_replace_box_replaces_one_match()
    {
        var view = Replacing("cat cat", "cat", "x");

        view.Find.ReplaceBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = view.Find.ReplaceBox });

        Assert.Equal("x cat", view.Text);
    }

    [AvaloniaFact]
    public async Task Replace_all_is_one_undoable_edit_and_reports_the_count()
    {
        var view = Replacing("cat dog cat dog cat", "cat", "bird");

        await view.ReplaceAllAsync();

        Assert.Equal("bird dog bird dog bird", view.Text);
        Assert.Empty(view.Matches);
        Assert.Equal("Replaced 3 · No results", view.Find.CountText);

        view.Undo();
        Assert.Equal("cat dog cat dog cat", view.Text);
    }

    [AvaloniaFact]
    public async Task Replace_all_with_regex_groups()
    {
        var view = Replacing("a=1\nb=2\n", @"(\w)=(\d)", "$2=$1");
        view.Find.QueryBox.Text = @"(\w)=(\d)";
        var regex = view.Find.GetLogicalDescendants().OfType<CheckBox>().Single(c => Equals(c.Content, "Regex"));
        regex.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        await view.ReplaceAllAsync();

        Assert.Equal("1=a\n2=b\n", view.Text);
    }

    [AvaloniaFact]
    public async Task Typing_during_replace_all_is_kept()
    {
        var view = Replacing("cat dog cat", "cat", "bird");
        var replacing = view.ReplaceAllAsync();
        view.Editor.Buffer.ReplaceRange(view.Text.Length, 0, " cat");

        await replacing;

        Assert.Equal("bird dog bird bird", view.Text);
    }

    [AvaloniaFact]
    public async Task The_replaced_count_clears_when_the_query_changes()
    {
        var view = Replacing("cat", "cat", "dog");
        await view.ReplaceAllAsync();
        Assert.StartsWith("Replaced 1", view.Find.CountText, StringComparison.Ordinal);

        view.Find.QueryBox.Text = "dog";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("1 of 1", view.Find.CountText);
    }

    [AvaloniaFact]
    public async Task The_word_box_limits_find_and_replace_all_to_whole_words()
    {
        var view = Replacing("cat concat cat", "cat", "dog");
        Assert.Equal(3, view.Matches.Count);

        view.Find.WholeWord = true; // ticking the box searches again
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, view.Matches.Count);

        await view.ReplaceAllAsync();
        Assert.Equal("dog concat dog", view.Text);
        Assert.Contains(view.Find.GetLogicalDescendants().OfType<CheckBox>(), c => Equals(c.Content, "Word"));
    }
}
