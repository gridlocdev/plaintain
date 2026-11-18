using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Puffinote.Components.Controls;

/// <summary>
/// A vertical scroller for a dialog's form whose scroll bar sits in a gutter of its own, not on the rows.
///
/// <para>Avalonia's scroll bar overlays the content it scrolls. A form laid out to a fixed width fills the
/// scroller exactly, so the bar was drawn over the right edge of every dropdown and text box beside it. The
/// gutter is taken from the dialog card's padding (24 px, see <c>OverlayHost</c>): the scroller reaches
/// <see cref="Width"/> past its slot and the content gives the same back, so the rows keep their width and
/// position and the bar lands in space that was empty.</para>
/// </summary>
public static class ScrollGutter
{
    /// <summary>The width kept clear for the scroll bar: its full, expanded width plus a little air.</summary>
    public const double Width = 16;

    /// <summary>The tallest a dialog's form gets before it scrolls.</summary>
    public const double MaxHeight = 460;

    public static ScrollViewer Wrap(Control content)
    {
        ArgumentNullException.ThrowIfNull(content);
        content.Margin = new Thickness(0, 0, Width, 0);
        return new ScrollViewer
        {
            MaxHeight = MaxHeight,
            Margin = new Thickness(0, 0, -Width, 0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content,
        };
    }
}
