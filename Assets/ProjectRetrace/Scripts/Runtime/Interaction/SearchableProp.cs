using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Marks the root of one piece of searchable furniture. The interactor's shell latch
    /// needs to know where a prop ends: the imported level keeps every prop under a single
    /// house root, so "same root" would let a chair offer the dresser next to it.
    /// </summary>
    [DisallowMultipleComponent]
    public class SearchableProp : MonoBehaviour
    {
    }
}
