using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class ScenePortal2D : MonoBehaviour
{
    [SerializeField] private string destinationScene = "SampleScene";
    [SerializeField] private string destinationSpawnId = "Start";

    public void Configure(string targetScene, string targetSpawnId)
    {
        destinationScene = targetScene;
        destinationSpawnId = targetSpawnId;
    }

    private void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!SceneWarpState.CanWarpNow())
        {
            return;
        }

        if (other.GetComponent<TopDownPlayerController2D>() == null
            && other.GetComponentInParent<TopDownPlayerController2D>() == null)
        {
            return;
        }

        SceneWarpState.SetPendingSpawn(destinationSpawnId);
        SceneManager.LoadScene(destinationScene);
    }
}
