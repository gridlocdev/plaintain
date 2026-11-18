using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Plaintain.Control.Controls;
using Plaintain.Control.Views;
using Xunit;

namespace Plaintain.UiTests;

/// <summary>
/// The caret's row is filled edge-to-edge across the viewport, so its tint is doing a different job from
/// Typage's — there the fill stops at the content column, here it is a bar across the whole window on
/// every row the caret visits. It has to be findable at a glance and invisible the rest of the time.
/// </summary>
public class CurrentRowTintTests
{
    /// <summary>
    /// Faint enough not to compete with the text. The numbers are the point: anything much above these
    /// is the bar people asked to have turned down, and zero is not a highlight at all.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false, 0x0A)]
    [InlineData(true, 0x14)]
    public void The_caret_row_is_tinted_faintly_in_both_variants(bool dark, byte alpha)
    {
        var tint = PlainTextEditor.CurrentRowTintForTest(dark ? ThemeVariant.Dark : ThemeVariant.Light);

        Assert.Equal(alpha, tint.A);
        Assert.InRange(tint.A, 1, 0x18);
    }

    /// <summary>Still a tint of the canvas, not a colour of its own — white on dark, black on light.</summary>
    [AvaloniaFact]
    public void The_tint_is_the_canvas_lightened_or_darkened_rather_than_a_hue()
    {
        var dark = PlainTextEditor.CurrentRowTintForTest(ThemeVariant.Dark);
        var light = PlainTextEditor.CurrentRowTintForTest(ThemeVariant.Light);

        Assert.Equal((0xFF, 0xFF, 0xFF), (dark.R, dark.G, dark.B));
        Assert.Equal((0x00, 0x00, 0x00), (light.R, light.G, light.B));
    }

    /// <summary>
    /// The tint marks where the caret is. Once something is selected, the selection already says that,
    /// and the tint under it makes the selection's first or last row look fully selected when only its
    /// line break is, with the two washes stacking where they meet. So it goes while a selection is up.
    /// </summary>
    [AvaloniaFact]
    public void The_caret_row_is_not_tinted_while_text_is_selected()
    {
        var view = new PlainTextEditorView();
        var window = new Window { Width = 500, Height = 300, Content = view };
        window.Show();
        view.LoadText("one\n\ntwo\n\nthree");
        var editor = view.Editor;

        Assert.Equal(1, RowsTinted(editor));

        editor.Buffer.Selection = new Puffinote.Text.Document.Selection(10, 4); // caret on the blank line 2, line 3 selected
        Assert.Equal(0, RowsTinted(editor));

        editor.Buffer.Selection = Puffinote.Text.Document.Selection.At(4);
        Assert.Equal(1, RowsTinted(editor));
    }

    private static int RowsTinted(PlainTextEditor editor)
    {
        editor.UpdateLayout();
        int before = editor.CurrentRowsDrawnForTest;
        using (var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(500, 300)))
        using (var context = bitmap.CreateDrawingContext())
        {
            editor.Render(context);
        }

        return editor.CurrentRowsDrawnForTest - before;
    }
}
