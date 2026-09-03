using System;

namespace ProjectRetrace
{
    /// <summary>
    /// Marks where a settings tab begins in RetraceConfig: the tagged field and every field
    /// declared after it belong to that tab until the next tag. One attribute per group
    /// rather than one per field keeps adding a tunable a one-line job, which is the whole
    /// point of the reflected menu.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ConfigTabAttribute : Attribute
    {
        public string Name { get; }

        public ConfigTabAttribute(string name)
        {
            Name = name;
        }
    }
}
