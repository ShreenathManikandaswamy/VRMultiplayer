namespace XRMultiplayer
{
    public interface INetworkInteractableSpawnPolicy
    {
        bool blocksSpawnerRespawn { get; }
        bool despawnWhenSpawnerRespawns { get; }
    }
}
