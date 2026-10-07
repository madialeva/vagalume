using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Vagalume.Wasm.UI.Markup;

/// <summary>
/// Writes a markup tree into Blazor's <see cref="RenderTreeBuilder"/>. Every child goes in its own region keyed by its
/// position, so a conditional child that appears or disappears does not shift the identity of its siblings.
/// </summary>
public static class TreeEmitter
{
    public static void Emit(RenderTreeBuilder builder, Node root, IHandleEvent receiver) => EmitNode(builder, root, receiver);

    private static void EmitNode(RenderTreeBuilder builder, Node node, IHandleEvent receiver)
    {
        switch (node)
        {
            case ElementNode element:
                EmitElement(builder, element, receiver);
                break;
            case TextNode text:
                builder.AddContent(0, text.Text);
                break;
            case FragmentNode fragment:
                EmitChildren(builder, fragment.Children, receiver, 0);
                break;
        }
    }

    private static void EmitElement(RenderTreeBuilder builder, ElementNode element, IHandleEvent receiver)
    {
        builder.OpenElement(0, element.Tag);
        var sequence = 1;
        foreach (var attribute in element.Attributes)
        {
            sequence = EmitAttribute(builder, attribute, receiver, sequence);
        }

        EmitChildren(builder, element.Children, receiver, sequence);
        builder.CloseElement();
    }

    private static int EmitAttribute(RenderTreeBuilder builder, Attr attribute, IHandleEvent receiver, int sequence)
    {
        switch (attribute)
        {
            case ValueAttr value:
                builder.AddAttribute(sequence++, value.Name, value.Value);
                break;
            case FlagAttr flag:
                builder.AddAttribute(sequence++, flag.Name, flag.On);
                break;
            case HandlerAttr handler:
                builder.AddAttribute(sequence++, handler.Event, EventCallback.Factory.Create(receiver, handler.Handler));
                if (handler.PreventDefault)
                {
                    builder.AddEventPreventDefaultAttribute(sequence++, handler.Event, true);
                }

                break;
            case BindAttr bind:
                builder.AddAttribute(sequence++, "value", bind.Value);
                builder.AddAttribute(
                    sequence++,
                    "oninput",
                    EventCallback.Factory.Create<ChangeEventArgs>(receiver, e => bind.Set(e.Value?.ToString() ?? string.Empty)));
                break;
        }

        return sequence;
    }

    private static void EmitChildren(RenderTreeBuilder builder, IReadOnlyList<Node> children, IHandleEvent receiver, int firstSequence)
    {
        for (var i = 0; i < children.Count; i++)
        {
            builder.OpenRegion(firstSequence + i);
            EmitNode(builder, children[i], receiver);
            builder.CloseRegion();
        }
    }
}
