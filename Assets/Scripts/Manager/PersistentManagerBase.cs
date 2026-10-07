using UnityEngine;
using UnityEngine.SceneManagement;

public abstract class PersistentManagerBase : MonoBehaviour
{
    protected virtual void Awake()
    {
        // make root and persist
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    protected virtual void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    protected virtual void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // override to rebind scene-local UI
    }

    /// <summary>
    /// Called when a new run starts and the manager should reset runtime state.
    /// </summary>
    public virtual void ResetForNewRun() { }
}
