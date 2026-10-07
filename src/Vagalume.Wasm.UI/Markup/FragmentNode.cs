namespace Vagalume.Wasm.UI.Markup;

/// <summary>
/// A group of nodes without a wrapper element; an empty fragment keeps the position of a conditional child stable.
/// </summary>
public sealed record FragmentNode(IReadOnlyList<Node> Children) : Node
{
    public static FragmentNode Empty { get; } = new([]);
}
