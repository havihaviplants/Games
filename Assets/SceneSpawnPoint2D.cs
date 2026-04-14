using UnityEngine;

public class SceneSpawnPoint2D : MonoBehaviour
{
    [SerializeField] private string spawnId = "Start";

    public string SpawnId => spawnId;

    public void Configure(string id)
    {
        spawnId = id;
        name = $"Spawn_{id}";
    }
}
