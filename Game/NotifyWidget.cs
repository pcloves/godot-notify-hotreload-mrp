using Godot;
using Godot.Collections;

/// <summary>
/// Game-assembly node with <c>[Notify]</c> properties whose value is a
/// <see cref="Resource"/>. Its generated setters subscribe to the resource's
/// <c>changed</c> signal.
/// </summary>
[Tool]
public partial class NotifyWidget : Node
{
    [Export]
    [Notify]
    public NotifyGadget Gadget
    {
        get => _gadget.Get();
        set => _gadget.Set(value);
    }

    [Export]
    [Notify]
    public Array<NotifyGadget> Gadgets
    {
        get => _gadgets.Get();
        set => _gadgets.Set(value);
    }
}
