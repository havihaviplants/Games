public static class SceneWarpState
{
    private static string pendingSpawnId;
    private static float nextAllowedWarpTime;

    public static void SetPendingSpawn(string spawnId)
    {
        pendingSpawnId = spawnId;
        nextAllowedWarpTime = UnityEngine.Time.unscaledTime + 0.45f;
    }

    public static string ConsumePendingSpawn()
    {
        string spawnId = pendingSpawnId;
        pendingSpawnId = null;
        return spawnId;
    }

    public static bool CanWarpNow()
    {
        return UnityEngine.Time.unscaledTime >= nextAllowedWarpTime;
    }
}
