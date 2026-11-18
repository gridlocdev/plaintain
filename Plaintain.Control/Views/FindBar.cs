using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Puffinote.Components.Commands;
using Puffinote.Components.Theming;
using Puffinote.Text.Document;

namespace Plaintain.Control.Views;

/// <summary>
/// The find strip: a query, a live match count, step forward/back, and Esc to dismiss. Opened for replace,
/// it adds a second row with the replacement and Replace / Replace All. Plain substring
/// search with an optional regex, over the document's text — the same <see cref="DocumentSearch"/> the
/// rest of the family uses, so "what counts as a match" is one answer everywhere.
///
/// <para>Deliberately not shared with Typage's find bar, and the Phase 5 review settled it: the two
/// differ where it matters. Typage's is a controller bound to its editor that edits the text itself and
/// reveals matches through concealed syntax; this one is a passive strip that raises events (replace too)
/// and lets the view do the editing and the editor paint the row highlights. One control with two modes would be two controls with extra
/// steps. What the review <em>did</em> promote is the wording below — see
/// <see cref="Puffinote.Text.Document.SearchStatus"/>.</para>
/// </summary>
public sealed class FindBar : Border
{
    private readonly TextBlock _count;
    private readonly CheckBox _regex;
    private readonly CheckBox _matchCase;
    private readonly StackPanel _replaceRow;
    private string? _replaced; // "Replaced 17", shown beside the count until the query changes

    public FindBar()
    {
        var palette = Tokens.Active;
        Background = palette.Surface;
        BorderBrush = palette.SurfaceBorder;
        BorderThickness = new Thickness(0, 0, 0, 1);
        Padding = new Thickness(10, 6);
        IsVisible = false;

        QueryBox = new TextBox { Width = 240, PlaceholderText = "Find" };
        QueryBox.KeyDown += OnQueryKeyDown;
        QueryBox.TextChanged += (_, _) => RaiseChanged();

        _count = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = palette.MutedText, MinWidth = 90 };
        _regex = new CheckBox { Content = "Regex", VerticalAlignment = VerticalAlignment.Center };
        _matchCase = new CheckBox { Content = "Aa", VerticalAlignment = VerticalAlignment.Center };
        _regex.IsCheckedChanged += (_, _) => RaiseChanged();
        _matchCase.IsCheckedChanged += (_, _) => RaiseChanged();

        var previous = new Button { Content = "‹" };
        var next = new Button { Content = "›" };
        var close = new Button { Content = "✕" };
        previous.Click += (_, _) => Step?.Invoke(this, -1);
        next.Click += (_, _) => Step?.Invoke(this, +1);
        close.Click += (_, _) => Dismiss();

        ReplaceBox = new TextBox { Width = 240, PlaceholderText = "Replace" };
        ReplaceBox.KeyDown += OnReplaceKeyDown;
        var replaceOne = new Button { Content = "Replace" };
        var replaceAll = new Button { Content = "Replace All" };
        replaceOne.Click += (_, _) => ReplaceOne?.Invoke(this, EventArgs.Empty);
        replaceAll.Click += (_, _) => ReplaceAll?.Invoke(this, EventArgs.Empty);
        _replaceRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            IsVisible = false,
            Children = { ReplaceBox, replaceOne, replaceAll },
        };

        Child = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { QueryBox, previous, next, _count, _matchCase, _regex, close },
                },
                _replaceRow,
            },
        };
    }

    /// <summary>Raised when the query or its options change — the host re-runs the search.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised to step to the previous (-1) or next (+1) match.</summary>
    public event EventHandler<int>? Step;

    /// <summary>Raised when the strip is dismissed (Esc or the close button).</summary>
    public event EventHandler? Dismissed;

    /// <summary>Raised to replace the current match (the Replace button, or Enter in the replace box).</summary>
    public event EventHandler? ReplaceOne;

    /// <summary>Raised to replace every match (the Replace All button, or ⌘Enter in the replace box).</summary>
    public event EventHandler? ReplaceAll;

    public string Query => QueryBox.Text ?? string.Empty;

    public bool UseRegex => _regex.IsChecked == true;

    public bool MatchCase => _matchCase.IsChecked == true;

    public string Replacement => ReplaceBox.Text ?? string.Empty;

    /// <summary>True while the replace row is showing.</summary>
    public bool IsReplacing => _replaceRow.IsVisible;

    /// <summary>The search box, so a host can focus it when the strip opens.</summary>
    public TextBox QueryBox { get; }

    /// <summary>The replacement box. Internal so a headless test can type in it.</summary>
    internal TextBox ReplaceBox { get; }

    /// <summary>
    /// Open the strip, with the replace row when <paramref name="replace"/> is set. Plain Find hides the row
    /// again, so ⌘F always gives the one-line strip. With a seed already in the query, opening for replace
    /// puts the cursor in the replacement box, which is where the next keystroke belongs.
    /// </summary>
    public void Show(string? seed, bool replace = false)
    {
        if (!string.IsNullOrEmpty(seed))
        {
            QueryBox.Text = seed;
        }

        IsVisible = true;
        _replaceRow.IsVisible = replace;
        if (replace && Query.Length > 0)
        {
            ReplaceBox.Focus();
            ReplaceBox.SelectAll();
        }
        else
        {
            QueryBox.Focus();
            QueryBox.SelectAll();
        }

        RaiseChanged();
    }

    public void Dismiss()
    {
        IsVisible = false;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Report the search's result: "3 of 17", "No results", or nothing while the query is empty.</summary>
    public void ReportMatches(int current, int total)
    {
        string status = SearchStatus.Describe(current, total, Query);
        _count.Text = _replaced is null ? status : $"{_replaced} · {status}";
        _count.Foreground = total == 0 && Query.Length > 0 ? Brushes.IndianRed : Tokens.Active.MutedText;
    }

    /// <summary>Say how many matches the last Replace All changed, until the query or its options change.</summary>
    public void ReportReplaced(int count)
    {
        _replaced = count == 1 ? "Replaced 1" : $"Replaced {count}";
    }

    /// <summary>What the count shows now, for tests.</summary>
    internal string? CountText => _count.Text;

    private void RaiseChanged()
    {
        _replaced = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnReplaceKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Dismiss();
                e.Handled = true;
                break;
            case Key.Enter:
                bool all = e.KeyModifiers.HasFlag(Shortcut.IsMac ? KeyModifiers.Meta : KeyModifiers.Control);
                (all ? ReplaceAll : ReplaceOne)?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
        }
    }

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Dismiss();
                e.Handled = true;
                break;
            case Key.Enter:
                Step?.Invoke(this, e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : +1);
                e.Handled = true;
                break;
        }
    }
}
