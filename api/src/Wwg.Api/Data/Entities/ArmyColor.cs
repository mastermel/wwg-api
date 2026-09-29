namespace Wwg.Api.Data.Entities;

/// <summary>
/// An army's colour on the map and beside its name: one of the front-end's 8 palette colours,
/// which it tunes for contrast in light and dark. Stored by name, so the palette can change.
/// </summary>
public enum ArmyColor
{
    Red,
    Blue,
    Green,
    Orange,
    Purple,
    Sky,
    Gold,
    Magenta,
}
