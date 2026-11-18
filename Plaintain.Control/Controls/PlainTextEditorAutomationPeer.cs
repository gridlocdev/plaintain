using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Threading;

namespace Plaintain.Control.Controls;

/// <summary>
/// What assistive technology sees of <see cref="PlainTextEditor"/>: an editable text control (an
/// <c>AXTextField</c> on macOS) whose value is the text on screen, with a value-changed event after every
/// edit, scroll and resize.
///
/// <para>Avalonia 12.0 gives a custom control two text-ish roles. <c>Edit</c> becomes an
/// <c>AXTextField</c> that reads its <c>AXValue</c> from <see cref="IValueProvider"/>; <c>Document</c>
/// becomes <c>AXStaticText</c>, which says the text cannot be edited. It has no text-range or
/// selected-text provider, so the caret and selection are not exposed yet (ARCHITECTURE §6).</para>
/// </summary>
public sealed class PlainTextEditorAutomationPeer : ControlAutomationPeer, IValueProvider
{
    private string? _announced; // the value assistive technology was last told about
    private bool _refreshQueued;

    public PlainTextEditorAutomationPeer(PlainTextEditor owner)
        : base(owner)
    {
        owner.VisibleTextChanged += (_, _) => QueueRefresh();
    }

    public new PlainTextEditor Owner => (PlainTextEditor)base.Owner;

    public bool IsReadOnly => false;

    public string Value => _announced = Owner.VisibleText();

    public void SetValue(string? value) => Owner.ReplaceVisibleText(value ?? string.Empty);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;

    protected override string GetClassNameCore() => nameof(PlainTextEditor);

    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : "Text editor";

    // A burst of keystrokes or scroll ticks becomes one event, sent once the UI thread is idle, and only
    // when the text on screen really differs from what was last announced.
    private void QueueRefresh()
    {
        if (_refreshQueued || _announced is null)
        {
            return; // nothing has asked for the value yet, so there is nobody to tell
        }

        _refreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshQueued = false;
            string old = _announced;
            string now = Owner.VisibleText();
            if (!string.Equals(old, now, StringComparison.Ordinal))
            {
                _announced = now;
                RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, old, now);
            }
        }, DispatcherPriority.Background);
    }
}
