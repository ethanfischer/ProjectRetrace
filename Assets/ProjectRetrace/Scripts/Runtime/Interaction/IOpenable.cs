namespace ProjectRetrace
{
    /// <summary>A drawer, door, or lid that can be open or shut -- whatever the keys can be
    /// behind.</summary>
    public interface IOpenable
    {
        bool IsOpen { get; }
    }
}
