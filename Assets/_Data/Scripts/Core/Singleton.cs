using UnityEngine;
using UnityEngine.SceneManagement;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T m_ins;
    private static bool m_isQuitting = false;
    private static bool m_wasDestroyed = false;

    static Singleton()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneUnloaded(Scene current)
    {
        m_wasDestroyed = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        m_wasDestroyed = false;
    }

    protected virtual void Awake()
    {
        MakeSingleton(true);
    }

    protected virtual void OnApplicationQuit()
    {
        m_isQuitting = true;
    }

    protected virtual void OnDestroy()
    {
        if (m_ins == this)
        {
            m_ins = null;
            m_wasDestroyed = true;
        }
    }

    public static bool HasInstance => (UnityEngine.Object)m_ins != null && !m_isQuitting;

    public static T Ins
    {
        get
        {
            if (m_isQuitting)
            {
                return null;
            }

#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorApplication.isCompiling)
            {
                return null;
            }
#endif

            if ((UnityEngine.Object)m_ins == null)
            {
                m_ins = Object.FindFirstObjectByType<T>();

                if ((UnityEngine.Object)m_ins == null && !m_isQuitting && !m_wasDestroyed && Application.isPlaying)
                {
                    GameObject singleton = new GameObject(typeof(T).Name);
                    m_ins = singleton.AddComponent<T>();
                    m_wasDestroyed = false;
                }
            }

            return m_ins;
        }
    }

    public void MakeSingleton(bool destroyOnload)
    {
        if ((UnityEngine.Object)m_ins == null)
        {
            m_ins = this as T;
            m_wasDestroyed = false;

            if (destroyOnload) return;

            var root = transform.root;

            if (root != transform)
            {
                DontDestroyOnLoad(root);
            }
            else
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (m_ins != this)
        {
            Destroy(gameObject);
        }
    }
}
