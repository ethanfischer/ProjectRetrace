namespace ProjectRetrace
{
    public interface IInteractable
    {
        /// <summary>Shown on the reticle when looked at, e.g. "Open drawer".</summary>
        string Prompt { get; }

        bool CanInteract { get; }

        void Interact(PlayerInteractor interactor);
    }
}
