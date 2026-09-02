using System.Text;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// A scene object's identity as a string two machines running the same build agree on.
    /// Instance ids are per-process and the generated house reuses names ("Cupboard" many
    /// times over), so the path carries each sibling index: a name collision at one level
    /// still yields a distinct string.
    /// </summary>
    public static class HierarchyPath
    {
        public static string Of(Transform transform)
        {
            if (transform == null) return string.Empty;

            var builder = new StringBuilder();
            Append(builder, transform);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, Transform transform)
        {
            if (transform.parent != null)
            {
                Append(builder, transform.parent);
                builder.Append('/');
            }

            builder.Append(transform.name).Append('#').Append(transform.GetSiblingIndex());
        }
    }
}
