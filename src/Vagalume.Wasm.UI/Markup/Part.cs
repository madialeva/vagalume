namespace Vagalume.Wasm.UI.Markup;

/// <summary>
/// Anything that can appear inside an element of the markup DSL: a child node or an attribute.
/// Plain strings convert to text nodes, so <c>Div("hola")</c> works.
/// </summary>
public abstract record Part
{
    public static implicit operator Part(string text) => new TextNode(text);
}
