using Avalonia.Media;

namespace Plaintain.Control.Theming;

/// <summary>
/// Every colour the editing surface paints with. The surface draws itself, so neither Fluent's theme nor
/// the family's chrome tokens reach it; this record is the whole of what a colour theme changes there.
/// </summary>
/// <param name="Background">The canvas behind the text.</param>
/// <param name="Text">The document's text, and the caret line's number in the gutter.</param>
/// <param name="GutterBackground">The line-number column.</param>
/// <param name="GutterText">Line numbers other than the caret's.</param>
/// <param name="CurrentLine">The faint full-width tint on the caret's row. Usually translucent.</param>
/// <param name="Selection">The selection's wash. Usually translucent, so the text stays readable through it.
/// Other occurrences of the selected text are tinted in this colour at half its alpha.</param>
/// <param name="Caret">The caret, and the find match the find bar has stepped to.</param>
/// <param name="Accent">The theme's accent, which the window's chrome (dialogs, focus, buttons) uses too.</param>
/// <param name="FindMatch">Every other find match. Usually translucent.</param>
public sealed record EditorColors(
    Color Background,
    Color Text,
    Color GutterBackground,
    Color GutterText,
    Color CurrentLine,
    Color Selection,
    Color Caret,
    Color Accent,
    Color FindMatch)
{
    /// <summary>The colours the surface has always used in dark mode.</summary>
    public static EditorColors DefaultDark { get; } = new(
        Background: Color.FromUInt32(0xFF1E1E1E),
        Text: Color.FromUInt32(0xFFE6E6E6),
        GutterBackground: Color.FromUInt32(0xFF232323),
        GutterText: Color.FromUInt32(0xFF9BA1A6),
        CurrentLine: Color.FromUInt32(0x14FFFFFF),
        Selection: Color.FromUInt32(0x332A7DE1),
        Caret: Color.FromUInt32(0xFF2A7DE1),
        Accent: Color.FromUInt32(0xFF2A7DE1),
        FindMatch: Color.FromUInt32(0x553FB084));

    /// <summary>The colours the surface has always used in light mode.</summary>
    public static EditorColors DefaultLight { get; } = new(
        Background: Color.FromUInt32(0xFFFFFFFF),
        Text: Color.FromUInt32(0xFF1A1A1A),
        GutterBackground: Color.FromUInt32(0xFFF6F6F6),
        GutterText: Color.FromUInt32(0xFF5C636B),
        CurrentLine: Color.FromUInt32(0x0A000000),
        Selection: Color.FromUInt32(0x332A7DE1),
        Caret: Color.FromUInt32(0xFF2A7DE1),
        Accent: Color.FromUInt32(0xFF2A7DE1),
        FindMatch: Color.FromUInt32(0x5515734F));

    /// <summary>The default colours for a dark or a light theme.</summary>
    public static EditorColors DefaultFor(bool dark) => dark ? DefaultDark : DefaultLight;
}

/// <summary>A named colour theme: what Preferences lists, and what a theme file in the themes folder describes.</summary>
/// <param name="Id">What settings.json stores. A built-in's id is a fixed slug; a theme file's is <c>file:</c> plus its file name.</param>
/// <param name="Name">What Preferences shows.</param>
/// <param name="IsDark">Which appearance it is for. Plaintain wears a dark theme in dark mode and a light one in light mode.</param>
/// <param name="Colors">The colours themselves.</param>
public sealed record EditorTheme(string Id, string Name, bool IsDark, EditorColors Colors)
{
    public const string DefaultDarkId = "plaintain-dark";
    public const string DefaultLightId = "plaintain-light";

    /// <summary>The themes that ship with Plaintain: the defaults first, then the others, dark before light.</summary>
    public static IReadOnlyList<EditorTheme> BuiltIn { get; } =
    [
        new(DefaultDarkId, "Plaintain Dark", true, EditorColors.DefaultDark),
        new("midnight", "Midnight", true, new EditorColors(
            Background: Color.FromUInt32(0xFF0F1724),
            Text: Color.FromUInt32(0xFFD6DEEB),
            GutterBackground: Color.FromUInt32(0xFF131C2B),
            GutterText: Color.FromUInt32(0xFF7D8BA1),
            CurrentLine: Color.FromUInt32(0x14FFFFFF),
            Selection: Color.FromUInt32(0x445B9CFF),
            Caret: Color.FromUInt32(0xFF5B9CFF),
            Accent: Color.FromUInt32(0xFF5B9CFF),
            FindMatch: Color.FromUInt32(0x55E0B341))),
        new("ember", "Ember", true, new EditorColors(
            Background: Color.FromUInt32(0xFF1F1A16),
            Text: Color.FromUInt32(0xFFEADFD3),
            GutterBackground: Color.FromUInt32(0xFF251F1A),
            GutterText: Color.FromUInt32(0xFFA3927F),
            CurrentLine: Color.FromUInt32(0x14FFFFFF),
            Selection: Color.FromUInt32(0x44E8894A),
            Caret: Color.FromUInt32(0xFFE8894A),
            Accent: Color.FromUInt32(0xFFE8894A),
            FindMatch: Color.FromUInt32(0x556FB36B))),
        new("forest", "Forest", true, new EditorColors(
            Background: Color.FromUInt32(0xFF17201B),
            Text: Color.FromUInt32(0xFFDCE8DF),
            GutterBackground: Color.FromUInt32(0xFF1B2620),
            GutterText: Color.FromUInt32(0xFF8BA494),
            CurrentLine: Color.FromUInt32(0x14FFFFFF),
            Selection: Color.FromUInt32(0x444DBF86),
            Caret: Color.FromUInt32(0xFF4DBF86),
            Accent: Color.FromUInt32(0xFF4DBF86),
            FindMatch: Color.FromUInt32(0x55E0C04D))),
        new("high-contrast-dark", "High Contrast Dark", true, new EditorColors(
            Background: Color.FromUInt32(0xFF000000),
            Text: Color.FromUInt32(0xFFFFFFFF),
            GutterBackground: Color.FromUInt32(0xFF000000),
            GutterText: Color.FromUInt32(0xFFD0D0D0),
            CurrentLine: Color.FromUInt32(0x26FFFFFF),
            Selection: Color.FromUInt32(0x664D9DFF),
            Caret: Color.FromUInt32(0xFFFFD500),
            Accent: Color.FromUInt32(0xFF4D9DFF),
            FindMatch: Color.FromUInt32(0x77FFD500))),
        new(DefaultLightId, "Plaintain Light", false, EditorColors.DefaultLight),
        new("paper", "Paper", false, new EditorColors(
            Background: Color.FromUInt32(0xFFFBF7EE),
            Text: Color.FromUInt32(0xFF2B2620),
            GutterBackground: Color.FromUInt32(0xFFF3EDE1),
            GutterText: Color.FromUInt32(0xFF756B5E),
            CurrentLine: Color.FromUInt32(0x0D000000),
            Selection: Color.FromUInt32(0x40B5651D),
            Caret: Color.FromUInt32(0xFFB5651D),
            Accent: Color.FromUInt32(0xFFB5651D),
            FindMatch: Color.FromUInt32(0x55D9A520))),
        new("mist", "Mist", false, new EditorColors(
            Background: Color.FromUInt32(0xFFF4F7FA),
            Text: Color.FromUInt32(0xFF1E2933),
            GutterBackground: Color.FromUInt32(0xFFEAEFF4),
            GutterText: Color.FromUInt32(0xFF5A6977),
            CurrentLine: Color.FromUInt32(0x0A000000),
            Selection: Color.FromUInt32(0x402F7F9E),
            Caret: Color.FromUInt32(0xFF2F7F9E),
            Accent: Color.FromUInt32(0xFF2F7F9E),
            FindMatch: Color.FromUInt32(0x553FA34D))),
        new("high-contrast-light", "High Contrast Light", false, new EditorColors(
            Background: Color.FromUInt32(0xFFFFFFFF),
            Text: Color.FromUInt32(0xFF000000),
            GutterBackground: Color.FromUInt32(0xFFFFFFFF),
            GutterText: Color.FromUInt32(0xFF333333),
            CurrentLine: Color.FromUInt32(0x14000000),
            Selection: Color.FromUInt32(0x550040C0),
            Caret: Color.FromUInt32(0xFF0040C0),
            Accent: Color.FromUInt32(0xFF0040C0),
            FindMatch: Color.FromUInt32(0x77FF9900))),
    ];
}
