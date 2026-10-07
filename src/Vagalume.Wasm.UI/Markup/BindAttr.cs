namespace Vagalume.Wasm.UI.Markup;

/// <summary>
/// Two-way binding of a text input: shows <see cref="Value"/> and reports every change to <see cref="Set"/>.
/// </summary>
public sealed record BindAttr(string Value, Action<string> Set) : Attr;
