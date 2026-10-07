using Godot;

/// <summary>
/// Game-assembly resource with a <c>[Notify]</c> property.
/// Restoring this property during a hot reload emits <c>Resource.changed</c>.
/// </summary>
[Tool]
public partial class NotifyGadget : Resource
{
    [Export]
    [Notify]
    public int Level
    {
        get => _level.Get();
        set => _level.Set(value);
    }
}
