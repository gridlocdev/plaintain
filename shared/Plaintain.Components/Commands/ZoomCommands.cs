using Avalonia.Input;
using Plaintain.Components.Theming;

namespace Plaintain.Components.Commands;

/// <summary>
/// The three zoom commands, defined once. The editor's catalog splices these in over its own zoom factor,
/// so Zoom In has one id, one title, one icon and one chord wherever it appears — menu bar, command bar,
/// Command Palette or key binding.
/// <para>
/// The chords are <b>⌘=</b> in, <b>⌘−</b> out, and <b>⌥⌘0</b> back to 100%. What the menus spell as "⌘+" is
/// really ⌘= — no keyboard has an unshifted "+" on the number row — so the shifted form and the numeric
/// keypad's own keys ride along as <see cref="AppCommand.AlternateShortcuts"/> rather than as extra rows a
/// reader has to learn.
/// </para>
/// <para>
/// Reset takes <b>⌥⌘0</b> rather than the more usual ⌘0, which leaves ⌘0 free.
/// </para>
/// </summary>
public static class ZoomCommands
{
    /// <summary>The id of the "Zoom In" command, so a host can find it in a catalog by name.</summary>
    public const string ZoomInId = "view.zoomIn";

    public const string ZoomOutId = "view.zoomOut";

    public const string ActualSizeId = "view.actualSize";

    /// <summary>
    /// Build the three commands over one zoom factor. <paramref name="get"/> reads the editor's current
    /// factor and <paramref name="set"/> applies a new one (the editor clamps and repaints); nothing else
    /// about the editor is assumed, which is why one factory serves four unrelated editing surfaces.
    /// </summary>
    public static IReadOnlyList<AppCommand> Build(Func<double> get, Action<double> set)
    {
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);

        return
        [
            new(ZoomInId, "Zoom In", "Make everything larger.",
                Shortcut.Cmd(Key.OemPlus), () => set(ZoomScale.In(get())),
                canExecute: () => get() < ZoomScale.Max, icon: "zoom-in",
                alternateShortcuts: [Shortcut.Cmd(Key.OemPlus, KeyModifiers.Shift), Shortcut.Cmd(Key.Add)]),

            new(ZoomOutId, "Zoom Out", "Make everything smaller.",
                Shortcut.Cmd(Key.OemMinus), () => set(ZoomScale.Out(get())),
                canExecute: () => get() > ZoomScale.Min, icon: "zoom-out",
                alternateShortcuts: [Shortcut.Cmd(Key.Subtract)]),

            new(ActualSizeId, "Actual Size", "Reset zoom to 100%.",
                Shortcut.Cmd(Key.D0, KeyModifiers.Alt), () => set(ZoomScale.Default),
                canExecute: () => Math.Abs(get() - ZoomScale.Default) > 0.001d, icon: "maximize",
                alternateShortcuts: [Shortcut.Cmd(Key.NumPad0, KeyModifiers.Alt)]),
        ];
    }
}
