using UnityEngine;
using Core.Registry;

/// <summary>
/// RegistrySingleton provides universal singleton reference to the Registry Database.
/// It surfaces Icons and other registry types as convenient cached properties.
/// </summary>
public class RegistrySingleton : MonoBehaviour
{
    private static RegistrySingleton _instance;
    
    [SerializeField] private RegistryManager _registryManager;
    
    [Header("Registry References")]
    [SerializeField] public Registry iconsRegistry;
    [SerializeField] public Registry prefabRegistry;
    [SerializeField] public Registry materialRegistry;
    [SerializeField] public Registry meshRegistry;
    [SerializeField] public Registry audioRegistry;

    public static RegistrySingleton Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<RegistrySingleton>();
                if (_instance == null)
                {
                    Debug.LogError("[RegistrySingleton] Instance not found in scene. Please add RegistrySingleton to the scene.");
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets the Icons registry from the Registry Database.
    /// Returns Texture assets by UID for displaying registry item icons.
    /// </summary>
    public Registry Icons
    {
        get
        {
            if (iconsRegistry == null)
            {
                Debug.LogError("[RegistrySingleton] Icons registry not assigned in Inspector.");
            }
            return iconsRegistry;
        }
    }

    /// <summary>
    /// Gets a texture from the Icons registry by UID.
    /// </summary>
    /// <param name="uid">The icon registry UID, e.g. "creature".</param>
    /// <returns>The Texture2D icon, or null if not found</returns>
    public Texture2D GetIcon(string uid)
    {
        if (iconsRegistry == null)
        {
            Debug.LogError("[RegistrySingleton] Icons registry not assigned.");
            return null;
        }

        var item = iconsRegistry.GetItemByUID(uid);
        if (item == null)
        {
            Debug.LogWarning($"[RegistrySingleton] Icon UID '{uid}' not found in Icons registry.");
            return null;
        }

        return item.asset as Texture2D;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[RegistrySingleton] Multiple instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
