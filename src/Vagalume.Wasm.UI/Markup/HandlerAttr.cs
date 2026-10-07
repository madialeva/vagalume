namespace Vagalume.Wasm.UI.Markup;

/// <summary>
/// An event handler; the component re-renders after it completes.
/// </summary>
public sealed record HandlerAttr(string Event, Func<Task> Handler, bool PreventDefault = false) : Attr;
