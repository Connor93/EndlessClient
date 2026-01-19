namespace EOLib.Domain.Interact
{
    public interface INpcInfoDialogActions
    {
        void ShowNpcInfo(int npcId);
        void ShowNpcSearchResults(int[] npcIds);
        void ShowNoNpcsFound(string searchTerm);
    }
}
