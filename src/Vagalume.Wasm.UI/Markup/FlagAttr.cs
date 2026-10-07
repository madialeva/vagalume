namespace Vagalume.Wasm.UI.Markup;

/// <summary>
/// A boolean attribute such as <c>disabled</c>; it is emitted only when <see cref="On"/> is true.
/// </summary>
public sealed record FlagAttr(string Name, bool On) : Attr;
