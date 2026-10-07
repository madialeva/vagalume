namespace Vagalume.Wasm.UI.Markup;

/// <summary>
/// An attribute with a text value, such as <c>class</c> or <c>placeholder</c>.
/// </summary>
public sealed record ValueAttr(string Name, string Value) : Attr;
