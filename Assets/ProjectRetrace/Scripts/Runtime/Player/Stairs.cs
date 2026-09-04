using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Marks the one kind of geometry the player may climb. The controller keeps its step
    /// height near zero everywhere else, so furniture is a wall rather than a ladder; a
    /// component instead of a tag or layer, matching how the rest of the house is wired.
    /// </summary>
    public class Stairs : MonoBehaviour
    {
    }
}
