namespace Vagalume.Wasm.UI.Markup;

/// <summary>
/// The markup DSL: static functions that build element trees, so a view reads like HTML but is plain C#.
/// Use it with <c>using static Vagalume.Wasm.UI.Markup.Html;</c>.
/// </summary>
public static class Html
{
    public static Node Empty => FragmentNode.Empty;

    public static ElementNode El(string tag, params Part[] parts) =>
        new(tag, [.. parts.OfType<Attr>()], [.. parts.OfType<Node>()]);

    public static ElementNode Header(params Part[] parts) => El("header", parts);

    public static ElementNode MainContent(params Part[] parts) => El("main", parts);

    public static ElementNode Div(params Part[] parts) => El("div", parts);

    public static ElementNode Span(params Part[] parts) => El("span", parts);

    public static ElementNode H1(params Part[] parts) => El("h1", parts);

    public static ElementNode P(params Part[] parts) => El("p", parts);

    public static ElementNode Ul(params Part[] parts) => El("ul", parts);

    public static ElementNode Li(params Part[] parts) => El("li", parts);

    public static ElementNode Form(params Part[] parts) => El("form", parts);

    public static ElementNode Input(params Part[] parts) => El("input", parts);

    public static ElementNode Button(params Part[] parts) => El("button", parts);

    public static ElementNode Time(params Part[] parts) => El("time", parts);

    public static Node Text(string text) => new TextNode(text);

    /// <summary>Groups nodes without a wrapper; handy to return several children from a helper.</summary>
    public static Node Many(IEnumerable<Node> nodes) => new FragmentNode([.. nodes]);

    /// <summary>The node built by <paramref name="then"/> when <paramref name="condition"/> holds, otherwise an empty fragment.</summary>
    public static Node When(bool condition, Func<Node> then) => condition ? then() : Empty;

    public static Attr Class(string value) => new ValueAttr("class", value);

    public static Attr Id(string value) => new ValueAttr("id", value);

    public static Attr Type(string value) => new ValueAttr("type", value);

    public static Attr Placeholder(string value) => new ValueAttr("placeholder", value);

    public static Attr AriaLabel(string value) => new ValueAttr("aria-label", value);

    public static Attr Role(string value) => new ValueAttr("role", value);

    public static Attr DateTime(string value) => new ValueAttr("datetime", value);

    public static Attr Disabled(bool on = true) => new FlagAttr("disabled", on);

    public static Attr OnClick(Func<Task> handler) => new HandlerAttr("onclick", handler);

    public static Attr OnClick(Action handler) => new HandlerAttr("onclick", () => { handler(); return Task.CompletedTask; });

    /// <summary>Submit handler that also stops the browser from submitting the form itself.</summary>
    public static Attr OnSubmit(Func<Task> handler) => new HandlerAttr("onsubmit", handler, PreventDefault: true);

    public static Attr BindValue(string current, Action<string> set) => new BindAttr(current, set);
}
