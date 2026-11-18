using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Puffinote.Components.Controls;

/// <summary>
/// A vertical scroller for a dialog's form whose scroll bar sits in a gutter of its own, not on the rows.
///
/// <para>Avalonia's scroll bar overlays the content it scrolls. A form laid out to a fixed width fills the
/// scroller exactly, so the bar was drawn over the right edge of every dropdown and text box beside it. The
/// content now keeps <see cref="Width"/> clear on its right, inside the scroller, so the bar has a column
/// of its own at the card's content edge, in line with the dialog's buttons.</para>
///
/// <para>Not a negative margin that pushes the scroller out into the card's padding: that lays the bar out
/// where it looks right, but outside its parent's bounds, where Avalonia neither draws it nor routes the
/// pointer to it.</para>
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
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content,
        };
    }
}
