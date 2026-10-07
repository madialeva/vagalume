namespace Vagalume.Wasm.UI.Markup;

/// <summary>
/// An HTML element with its attributes, handlers and children.
/// </summary>
public sealed record ElementNode(string Tag, IReadOnlyList<Attr> Attributes, IReadOnlyList<Node> Children) : Node;
