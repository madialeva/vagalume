using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Vagalume.Wasm.UI.Markup;
using static Vagalume.Wasm.UI.Markup.Html;

namespace Vagalume.Wasm.UI.Tests;

public sealed class MarkupTests
{
    [Fact]
    public void Elements_NestWithAttributesAndText()
    {
        var view = Div(Class("box"), H1("Título"), P(Id("p1"), "uno ", Span("dos")));

        Assert.Equal(
            "<div class=\"box\"><h1>Título</h1><p id=\"p1\">uno <span>dos</span></p></div>",
            HtmlWriter.Render(view));
    }

    [Fact]
    public void Text_IsEscaped_SoItCanNeverInjectMarkup()
    {
        var view = P("<script>alert(1)</script>", Class("a\"b"));

        Assert.Equal("<p class=\"a&quot;b\">&lt;script&gt;alert(1)&lt;/script&gt;</p>", HtmlWriter.Render(view));
    }

    [Fact]
    public void When_AddsTheChildOnlyIfTheConditionHolds()
    {
        Assert.Equal("<div><b></b></div>", HtmlWriter.Render(Div(When(true, () => El("b")))));
        Assert.Equal("<div></div>", HtmlWriter.Render(Div(When(false, () => El("b")))));
    }

    [Fact]
    public void Flags_AppearOnlyWhenOn()
    {
        Assert.Equal("<button disabled></button>", HtmlWriter.Render(Button(Disabled())));
        Assert.Equal("<button></button>", HtmlWriter.Render(Button(Disabled(false))));
    }

    [Fact]
    public void Emitter_WritesElementsAttributesAndText()
    {
        var frames = Emit(Div(Class("box"), "hola"));

        Assert.Equal(RenderTreeFrameType.Element, frames[0].FrameType);
        Assert.Equal("div", frames[0].ElementName);
        Assert.Contains(frames, f => f.FrameType == RenderTreeFrameType.Attribute && f.AttributeName == "class" && (string?)f.AttributeValue == "box");
        Assert.Contains(frames, f => f.FrameType == RenderTreeFrameType.Text && f.TextContent == "hola");
    }

    [Fact]
    public async Task Emitter_WiresHandlersToTheReceiver()
    {
        var receiver = new Receiver();
        var clicks = 0;

        var frames = Emit(Button(OnClick(() => clicks++)), receiver);
        var handler = frames.Single(f => f.FrameType == RenderTreeFrameType.Attribute && f.AttributeName == "onclick");
        await ((EventCallback)handler.AttributeValue!).InvokeAsync();

        Assert.Equal(1, clicks);
        Assert.Equal(1, receiver.Handled);
    }

    [Fact]
    public async Task Emitter_BindsInputValueAndReportsEveryChange()
    {
        var typed = string.Empty;

        var frames = Emit(Input(BindValue("inicial", v => typed = v)));
        var oninput = frames.Single(f => f.FrameType == RenderTreeFrameType.Attribute && f.AttributeName == "oninput");
        await ((EventCallback)oninput.AttributeValue!).InvokeAsync(new ChangeEventArgs { Value = "nuevo" });

        Assert.Contains(frames, f => f.FrameType == RenderTreeFrameType.Attribute && f.AttributeName == "value" && (string?)f.AttributeValue == "inicial");
        Assert.Equal("nuevo", typed);
    }

    [Fact]
    public void Emitter_KeepsSiblingPositionsWhenAConditionalChildAppears()
    {
        var without = Emit(Div(H1("a"), When(false, () => P("x")), Span("b")));
        var with = Emit(Div(H1("a"), When(true, () => P("x")), Span("b")));

        static int[] RegionsOfTheNamed(RenderTreeFrame[] frames, string tag) =>
            [.. frames.Where(f => f.FrameType == RenderTreeFrameType.Element && f.ElementName == tag).Select(f => f.Sequence)];

        Assert.Equal(RegionsOfTheNamed(without, "span"), RegionsOfTheNamed(with, "span"));
    }

    private static RenderTreeFrame[] Emit(Node node, IHandleEvent? receiver = null)
    {
        var builder = new RenderTreeBuilder();
        TreeEmitter.Emit(builder, node, receiver ?? new Receiver());
        var range = builder.GetFrames();
        return [.. range.Array.Take(range.Count)];
    }

    private sealed class Receiver : IHandleEvent
    {
        public int Handled { get; private set; }

        public Task HandleEventAsync(EventCallbackWorkItem callback, object? arg)
        {
            Handled++;
            return callback.InvokeAsync(arg);
        }
    }
}
