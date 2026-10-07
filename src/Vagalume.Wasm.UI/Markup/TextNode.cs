namespace Vagalume.Wasm.UI.Markup;

/// <summary>
/// Literal text; the browser escapes it, so it can never inject markup.
/// </summary>
public sealed record TextNode(string Text) : Node;
