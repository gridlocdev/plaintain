using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Plaintain.Components.Commands;
using Plaintain.Components.Input;
using Plaintain.Components.Theming;
using Plaintain.Control.Commands;
using Plaintain.Control.Controls;
using Plaintain.Text.Document;

namespace Plaintain.Control.Views;

/// <summary>
/// The embeddable composite: the editing surface, its find strip, and the one command catalog that acts
/// on the open document. The Plaintain window embeds *this* and gets the whole editor; the window adds
/// only what needs a window.
/// </summary>
public sealed class PlainTextEditorView : UserControl
{
    public PlainTextEditorView()
    {
        Editor = new PlainTextEditor();

        var scroller = new ScrollViewer
        {
            Content = Editor,

            // The editor is an IScrollable, so the viewer hands it an offset instead of arranging a
            // million rows: Auto on both axes, and the surface decides its own extent.
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        Find.Changed += (_, _) => QueueSearch();
        Find.Step += (_, direction) => StepMatch(direction);
        Find.ReplaceOne += (_, _) => ReplaceCurrent();
        Find.ReplaceAll += (_, _) => PendingReplaceForTest = ReplaceAllAsync();
        Find.Dismissed += (_, _) =>
        {
            CancelSearch(); // a search still running must not bring the highlights back after the strip closes
            Matches = [];
            CurrentMatch = -1;
            Editor.SetMatches(Matches, CurrentMatch);
            Editor.Focus();
        };

        Editor.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);

        // The trackpad half of zoom, over the same seam the ⌘= / ⌘− catalog uses (see PinchZoom).
        PinchZoom.Attach(this, () => Zoom, zoom => Zoom = zoom);

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(Find, Dock.Top);
        root.Children.Add(Find);
        root.Children.Add(scroller);
        Content = root;
    }

    /// <summary>The surface itself, for a host that needs to focus it or read its caret.</summary>
    public PlainTextEditor Editor { get; }

    /// <summary>Raised when the caret, selection, or document changes — for a host's status bar.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>The document text.</summary>
    public string Text
    {
        get => Editor.Text;
        set => Editor.Text = value;
    }

    public bool IsDirty => Editor.Buffer.IsDirty;

    public bool CanUndo => Editor.Buffer.CanUndo;

    public bool CanRedo => Editor.Buffer.CanRedo;

    /// <summary>Load a document, marking it clean (or dirty, for a crash-recovery restore).</summary>
    public void LoadText(string? text, bool markSaved = true) => Editor.Load(text, markSaved);

    /// <summary>Take the version on disk as one undoable step (an external-change reload).</summary>
    public void ReloadText(string? text) => Editor.ReloadFrom(text);

    public void MarkSaved() => Editor.Buffer.MarkSaved();

    public void FocusEditor() => Editor.Focus();

    /// <summary>Word/character/line counts and the line-ending style, for a status bar.</summary>
    public DocumentStats Stats => Editor.Buffer.Stats;

    /// <inheritdoc cref="DocumentBuffer.StatsInBackgroundAsync"/>
    public Task<DocumentStats?> StatsInBackgroundAsync() => Editor.Buffer.StatsInBackgroundAsync();

    /// <summary>The caret's 1-based line and column, for a status bar.</summary>
    public LinePosition CaretPosition => Editor.Buffer.Document.GetPosition(Editor.Buffer.Selection.Caret);

    /// <summary>
    /// The editor's command catalog, built once per view so a bar, a palette, and a key binding all share
    /// one command instance (and one enabled state).
    /// </summary>
    public IReadOnlyList<AppCommand> EditingCommands => field ??= PlainTextEditingCommands.Build(this);

    public void Undo() => Editor.Undo();

    public void Redo() => Editor.Redo();

    /// <summary>
    /// Rewrite every line ending as <paramref name="style"/>, as one undoable edit. A no-op when the
    /// document already uses only that convention — a command that dirtied a file it did not change would
    /// be the worst kind of "did that do anything?". The text is read and rewritten on a background thread
    /// (a document snapshot is immutable), so only the edit itself runs on the UI thread. If the document
    /// changes meanwhile, the rewrite starts again from the new text, so nothing typed is lost.
    /// </summary>
    public async Task NormalizeLineEndingsAsync(NewlineStyle style)
    {
        var buffer = Editor.Buffer;
        while (true)
        {
            var snapshot = buffer.Document;
            string? normalized = await Task.Run(() =>
            {
                string text = snapshot.GetText();
                string result = LineEndings.Normalize(text, style);
                return ReferenceEquals(text, result) ? null : result; // Normalize returns its input when nothing changes
            }).ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.ForceYielding); // as in ReplaceAllAsync

            if (!ReferenceEquals(snapshot, buffer.Document))
            {
                continue;
            }

            if (normalized is not null)
            {
                buffer.ReplaceRange(0, snapshot.Length, normalized);
            }

            return;
        }
    }

    /// <summary>
    /// How many endings of each kind the document has, counted on a background thread. It is a much
    /// cheaper scan than <see cref="StatsInBackgroundAsync"/>, which also counts graphemes and words.
    /// </summary>
    public Task<LineEndingCounts> LineEndingCountsAsync()
    {
        var snapshot = Editor.Buffer.Document;
        return Task.Run(() => LineEndingCounts.Of(snapshot.GetText()));
    }

    /// <summary>
    /// The reading zoom, on the shared ladder. Delegates to the editor, which multiplies the
    /// configured font size by it; the host persists whatever it lands on.
    /// </summary>
    public double Zoom
    {
        get => Editor.Zoom;
        set
        {
            double clamped = ZoomScale.Clamp(value);
            if (Editor.Zoom == clamped)
            {
                return;
            }

            Editor.Zoom = clamped;
            ZoomChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Raised after <see cref="Zoom"/> changes, so a host can persist it as the new default.</summary>
    public event EventHandler? ZoomChanged;

    public void ToggleWordWrap() => Editor.WordWrap = !Editor.WordWrap;

    public void ToggleLineNumbers() => Editor.ShowLineNumbers = !Editor.ShowLineNumbers;

    public void ToggleInvisibles() => Editor.ShowInvisibles = !Editor.ShowInvisibles;

    /// <summary>Open the find strip, seeded with the selection when there is one (the usual courtesy).</summary>
    public void ShowFind() => Find.Show(SelectionSeed());

    /// <summary>Open the find strip with its replace row, seeded the same way as <see cref="ShowFind"/>.</summary>
    public void ShowReplace() => Find.Show(SelectionSeed(), replace: true);

    private string? SelectionSeed()
    {
        var sel = Editor.Buffer.Selection;
        return sel.IsEmpty || sel.Length > 200 ? null : Editor.Buffer.Document.GetText(sel.Start, sel.Length);
    }

    /// <summary>
    /// Replace the current match and move to the next one. When the selection is not on the current match
    /// (the caret moved since), the first press only selects the match, so you see what Replace will change
    /// before it changes it. Matches found in an older version of the document are searched for again
    /// rather than trusted.
    /// </summary>
    public void ReplaceCurrent()
    {
        if (CurrentMatch < 0 || CurrentMatch >= Matches.Count)
        {
            return;
        }

        var buffer = Editor.Buffer;
        if (!ReferenceEquals(_matchedDocument, buffer.Document))
        {
            QueueSearch();
            return;
        }

        var match = Matches[CurrentMatch];
        if (buffer.Selection.Start != match.Start || buffer.Selection.End != match.End)
        {
            RevealCurrent();
            return;
        }

        string? replacement = DocumentReplace.ReplacementFor(buffer.Document.GetText(), match, Find.Query, Find.Replacement,
            Find.UseRegex, Find.MatchCase, Find.WholeWord);
        if (replacement is not null)
        {
            // The caret lands after the replacement, so the search below picks the match after it: replacing
            // "a" with "aa" moves on instead of finding its own output.
            buffer.ReplaceRange(match.Start, match.Length, replacement, match.Start + replacement.Length);
        }

        QueueSearch();
    }

    /// <summary>
    /// Replace every match as one undoable edit. The new text is built on a background thread from a
    /// document snapshot, as <see cref="NormalizeLineEndingsAsync"/> does, and built again if the document
    /// changes meanwhile; only the edit itself runs on the UI thread. The strip then says how many it replaced.
    /// </summary>
    public async Task ReplaceAllAsync()
    {
        var buffer = Editor.Buffer;
        string query = Find.Query, replacement = Find.Replacement;
        bool useRegex = Find.UseRegex, matchCase = Find.MatchCase, wholeWord = Find.WholeWord;
        while (true)
        {
            var snapshot = buffer.Document;

            // ForceYielding: even when the work is already done by the time it is awaited (a small file on
            // a fast machine), the edit is posted back to the UI thread rather than applied before this
            // method returns, so a caller always gets control back first and the change check below decides.
            var edit = await Task.Run(() => DocumentReplace.ReplaceAll(snapshot.GetText(), query, replacement, useRegex, matchCase, wholeWord))
                .ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.ForceYielding);
            if (!ReferenceEquals(snapshot, buffer.Document))
            {
                continue;
            }

            if (edit is { } e)
            {
                buffer.ReplaceRange(e.Start, e.Length, e.Text, e.Start + e.Text.Length);
            }

            Find.ReportReplaced(edit?.Count ?? 0);
            QueueSearch();
            return;
        }
    }

    /// <summary>Test seam: the Replace All the strip's button started, or a completed task.</summary>
    internal Task PendingReplaceForTest { get; private set; } = Task.CompletedTask;

    /// <summary>Move the caret to a 1-based line number and centre it.</summary>
    public void GoToLine(int displayLine) => Editor.RevealLine(displayLine - 1);

    /// <summary>The find strip. Internal, not private, so a headless test can drive it as a user would.</summary>
    internal FindBar Find { get; } = new();

    /// <summary>Every match of the current query, in document order (empty while there is no query).</summary>
    internal IReadOnlyList<SearchMatch> Matches { get; private set; } = [];

    /// <summary>Which of <see cref="Matches"/> the caret is sitting on, or -1 when there are none.</summary>
    internal int CurrentMatch { get; private set; } = -1;

    /// <summary>
    /// A document at least this long (in characters) is searched on a background thread after a pause in
    /// typing. A shorter one is searched at once, on the UI thread, which costs well under a frame.
    /// Settable so a test can push a small document down the background path.
    /// </summary>
    internal int BackgroundSearchFrom { get; set; } = 256 * 1024;

    /// <summary>How long a large document waits for the query to stop changing before it is searched.</summary>
    internal TimeSpan SearchDelay { get; set; } = TimeSpan.FromMilliseconds(120);

    /// <summary>Test seam: the background search in flight, or a completed task.</summary>
    internal Task PendingSearchForTest { get; private set; } = Task.CompletedTask;

    private CancellationTokenSource? _search;

    // The document Matches were found in. An edit since then leaves their offsets pointing at old text.
    private TextDocument? _matchedDocument;

    /// <summary>
    /// Search for the find strip's query. Every keystroke in the query box lands here, so on a 50 MB log
    /// the search can't run on the UI thread: it waits for a pause, runs over the document as it is now,
    /// and is thrown away if a newer query (or an edit) arrives first. The old highlights stay up meanwhile;
    /// their offsets still hold, because the document hasn't changed under them.
    /// </summary>
    private void QueueSearch()
    {
        CancelSearch();
        if (!Find.IsVisible)
        {
            return; // a query-box change the dispatcher delivers after the strip closed has nothing to highlight
        }

        var document = Editor.Buffer.Document;
        string query = Find.Query;
        if (query.Length == 0)
        {
            ShowMatches([]);
            return;
        }

        if (document.Length < BackgroundSearchFrom)
        {
            ShowMatches(DocumentSearch.FindAll(document.GetText(), query, Find.UseRegex, Find.MatchCase, Find.WholeWord));
            return;
        }

        _search = new CancellationTokenSource();
        PendingSearchForTest = SearchInBackgroundAsync(document, query, Find.UseRegex, Find.MatchCase, Find.WholeWord, _search.Token);
    }

    private async Task SearchInBackgroundAsync(TextDocument document, string query, bool useRegex, bool matchCase, bool wholeWord,
        CancellationToken token)
    {
        try
        {
            await Task.Delay(SearchDelay, token).ConfigureAwait(true);

            // The snapshot is immutable, so reading it on another thread is safe while the UI goes on editing.
            var matches = await Task.Run(() => DocumentSearch.FindAll(document.GetText(), query, useRegex, matchCase, wholeWord, token), token)
                .ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (!ReferenceEquals(document, Editor.Buffer.Document))
            {
                QueueSearch(); // edited while searching: these offsets are the old text's
                return;
            }

            ShowMatches(matches);
        }
        catch (OperationCanceledException)
        {
            // Replaced by a newer query, or the strip closed.
        }
    }

    private void CancelSearch()
    {
        _search?.Cancel();
        _search?.Dispose();
        _search = null;
    }

    private void ShowMatches(IReadOnlyList<SearchMatch> matches)
    {
        Matches = matches;
        _matchedDocument = Editor.Buffer.Document;

        // Land on the first match at or after the caret, so opening find where you are reading does
        // not throw you back to the top of a 50,000-line log.
        int caret = Editor.Buffer.Selection.Start;
        CurrentMatch = -1;
        for (int i = 0; i < Matches.Count; i++)
        {
            if (Matches[i].Start >= caret)
            {
                CurrentMatch = i;
                break;
            }
        }

        if (CurrentMatch < 0 && Matches.Count > 0)
        {
            CurrentMatch = 0;
        }

        Editor.SetMatches(Matches, CurrentMatch);
        Find.ReportMatches(Math.Max(0, CurrentMatch), Matches.Count);
        RevealCurrent();
    }

    private void StepMatch(int direction)
    {
        if (Matches.Count == 0)
        {
            return;
        }

        CurrentMatch = ((CurrentMatch + direction) % Matches.Count + Matches.Count) % Matches.Count; // wraps both ways
        Editor.SetMatches(Matches, CurrentMatch);
        Find.ReportMatches(CurrentMatch, Matches.Count);
        RevealCurrent();
    }

    private void RevealCurrent()
    {
        if (CurrentMatch < 0 || CurrentMatch >= Matches.Count)
        {
            return;
        }

        var match = Matches[CurrentMatch];

        // Select the match without stealing focus from the query box — you are still typing in it.
        Editor.Buffer.Selection = new Selection(match.Start, match.End);
        Editor.BringCaretIntoView();
        Editor.InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && Find.IsVisible)
        {
            Find.Dismiss();
            e.Handled = true;
        }
    }
}
