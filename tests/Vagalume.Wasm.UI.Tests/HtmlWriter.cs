using System.Text;
using Vagalume.Wasm.UI.Markup;

namespace Vagalume.Wasm.UI.Tests;

/// <summary>
/// Serializes a markup tree to HTML text so tests can assert on what a view shows; handlers and bindings are listed as markers.
/// </summary>
internal static class HtmlWriter
{
    public static string Render(Node node)
    {
        var html = new StringBuilder();
        Write(html, node);
        return html.ToString();
    }

    private static void Write(StringBuilder html, Node node)
    {
        switch (node)
        {
            case TextNode text:
                html.Append(Escape(text.Text));
                break;
            case FragmentNode fragment:
                fragment.Children.ToList().ForEach(c => Write(html, c));
                break;
            case ElementNode element:
                html.Append('<').Append(element.Tag);
                foreach (var attribute in element.Attributes)
                {
                    html.Append(Attribute(attribute));
                }

                html.Append('>');
                element.Children.ToList().ForEach(c => Write(html, c));
                html.Append("</").Append(element.Tag).Append('>');
                break;
        }
    }

    private static string Attribute(Attr attribute) => attribute switch
    {
        ValueAttr v => $" {v.Name}=\"{Escape(v.Value)}\"",
        FlagAttr { On: true } f => $" {f.Name}",
        FlagAttr => string.Empty,
        HandlerAttr h => $" {h.Event}=@handler",
        BindAttr b => $" value=\"{Escape(b.Value)}\" oninput=@bind",
        _ => string.Empty,
    };

    private static string Escape(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
