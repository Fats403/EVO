/// <summary>
/// Simple interface for components that can build a deck definition for
/// the local player. This is intended as an abstraction point so that
/// different modes (constructed, draft, random, networked) can share a
/// common output type.
/// </summary>
public interface IDeckProvider
{
    /// <summary>
    /// Builds the local player's deck definition (cardIds + counts).
    /// </summary>
    DeckDefinition BuildLocalDeckDefinition();
}


