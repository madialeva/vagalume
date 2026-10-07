using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Vagalume.Wasm.UI.Markup;

/// <summary>
/// Base of the components written without Razor: the component only describes its view as a function of its
/// state in <see cref="Render"/>, and the Blazor render tree is derived from the returned node.
/// </summary>
public abstract class MarkupComponent : ComponentBase
{
    protected abstract Node Render();

    protected override void BuildRenderTree(RenderTreeBuilder builder) => TreeEmitter.Emit(builder, Render(), this);
}
