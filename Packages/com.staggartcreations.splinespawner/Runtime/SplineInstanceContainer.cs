// Spline Spawner by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//  • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//  • Uploading this file to a public GitHub repository will subject it to an automated DMCA takedown request.

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sc.splines.spawner.runtime
{
    [AddComponentMenu("")]
    [ExecuteInEditMode]
    public class SplineInstanceContainer : MonoBehaviour
    {
        //Important to track the container's owner, as duplicating a spawner (and thus its containers) would not update references (the copy will reference the original's containers)
        [SerializeReference]
        public SplineSpawner owner;
        
        [SerializeField]
        private List<GameObject> instances = new List<GameObject>(SplineSpawner.CAPACITY);
        
        public readonly Dictionary<GameObject, Queue<GameObject>> prefabPools = new Dictionary<GameObject, Queue<GameObject>>(SplineSpawner.CAPACITY);
        
        //Reference the source object/prefab for every instance
        private Dictionary<GameObject, GameObject> instanceToPrefab = new Dictionary<GameObject, GameObject>(SplineSpawner.CAPACITY);
        
        [SerializeField]
        [Tooltip("Make use of object pooling")]
        private bool usePooling = true;
        [Tooltip("Link spawned objects to their source prefab. Disable for best performance.")]
        public bool linkedPrefabs = true;

        public int InstanceCount => instances.Count;
        public int PoolSize => prefabPools.Count;

        public static SplineInstanceContainer Create(SplineSpawner spawner, int splineIndex)
        {
            GameObject go = new GameObject($"Spline #{splineIndex} Objects");

            go.transform.SetParent(spawner.root);
            go.transform.SetSiblingIndex(splineIndex);
            go.transform.hideFlags = HideFlags.NotEditable;

            SplineInstanceContainer container = go.AddComponent<SplineInstanceContainer>();
            container.owner = spawner;
            
            return container;
        }

        public void Destroy()
        {
            if (Application.isPlaying)
                Destroy(this.gameObject);
            else
                DestroyImmediate(this.gameObject);
        }

        private void OnDisable()
        {
            //if(owner.IsAllowedToSpawn()) ClearUnusedObjects();
        }

        public void DestroyInstances()
        {
            if (usePooling)
                DisableAllInstances();

            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i])
                {
                    if (usePooling)
                    {
                        Recycle(instances[i]);
                    }
                    else
                    {
                        if (Application.isPlaying)
                            Destroy(instances[i]);
                        else
                            DestroyImmediate(instances[i]);
                    }
                }
            }
            instances.Clear();
            instanceToPrefab.Clear();
        }
        
        public GameObject SpawnObject(SpawnPoint spawnPoint, GameObject prefab, Transform root, bool hide)
        {
            GameObject instance = usePooling ? GetPooledObject(prefab) : null;

            if (!instance)
            {
                instance = InstantiateObject(prefab, this.transform);
            }

            if (!instance)
            {
                Debug.LogError($"Failed to instantiate {prefab.name}");
                return null;
            }

            #if UNITY_EDITOR
            //Amazing slow!
            //UnityEditor.SceneVisibilityManager.instance.DisablePicking(transform.gameObject, false);
            
            //Support static batching if this object is marked static
            if (root && root.gameObject.isStatic)
            {
                StaticEditorFlags staticFlags = GameObjectUtility.GetStaticEditorFlags(root.gameObject);
                //staticFlags |= StaticEditorFlags.BatchingStatic;
                GameObjectUtility.SetStaticEditorFlags(instance, staticFlags);
            }
            #endif
            
            if(!instance.activeSelf) instance.SetActive(true);

            instance.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            instance.transform.localScale = spawnPoint.scale;

            if (hide)
                instance.hideFlags |= HideFlags.HideInHierarchy;  //Add the flag
            else
                instance.hideFlags &= ~HideFlags.HideInHierarchy; //Remove the flag
            
            //Source
            instanceToPrefab[instance] = prefab;
            //Instance of source
            instances.Add(instance);
            
            return instance;
        }

        private GameObject GetPooledObject(GameObject prefab)
        {
            if (prefabPools.TryGetValue(prefab, out var pool))
            {
                while (pool.Count > 0)
                {
                    GameObject obj = pool.Dequeue();
                    if (obj)
                    {
                        //Remove the flag
                        obj.hideFlags &= ~HideFlags.DontSave;
                        
                        return obj;
                    }
                }
            }
            return null;
        }

        private GameObject InstantiateObject(GameObject source, Transform parent)
        {
#if UNITY_EDITOR
            bool sourceIsPrefab = false;

            if (linkedPrefabs)
            {
                PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(source);

                sourceIsPrefab = prefabAssetType is PrefabAssetType.Regular or PrefabAssetType.Variant or PrefabAssetType.Model;

                if (sourceIsPrefab)
                {
                    GameObject prefabSource = prefabAssetType != PrefabAssetType.Variant ? PrefabUtility.GetCorrespondingObjectFromOriginalSource(source) : source;

                    if (prefabSource)
                    {
                        source = prefabSource;
                    }
                    else
                    {
                        Debug.LogError($"Failed to get prefab source from {source.name}");
                        sourceIsPrefab = false;
                    }
                }
            }
            
            if (sourceIsPrefab)
            {
                return (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            }
#endif
            
            //Runtime, or if not a prefab
            return Instantiate(source, parent);
        }

        private void Recycle(GameObject instance)
        {
            if (instance == null) return;

            instance.SetActive(false);
            
            //Thrown into the pool, but ensures it is never saved
            instance.hideFlags |= HideFlags.DontSave;
            
            if (!instanceToPrefab.TryGetValue(instance, out var prefab))
            {
                return;
            }

            if (!prefabPools.TryGetValue(prefab, out var pool))
            {
                pool = new Queue<GameObject>();
                prefabPools[prefab] = pool;
            }

            pool.Enqueue(instance);
        }
        
        public void WarmUpObjectPool(GameObject sourceObject, int poolSize = 1000)
        {
            if (!usePooling || sourceObject == null) return;
    
            if (!prefabPools.TryGetValue(sourceObject, out var pool))
            {
                pool = new Queue<GameObject>();
                prefabPools[sourceObject] = pool;
            }
    
            for (int i = 0; i < poolSize; i++)
            {
                GameObject instance = InstantiateObject(sourceObject, this.transform);
        
                if (instance)
                {
                    instance.SetActive(false);
                    instance.hideFlags |= HideFlags.DontSave;
                    
                    instanceToPrefab[instance] = sourceObject;
                    pool.Enqueue(instance);
                }
            }
        }
        
        /// <summary>
        /// Clears unused objects from the pool
        /// </summary>
        public void ClearUnusedObjects()
        {
            foreach (var kvp in prefabPools)
            {
                Queue<GameObject> pool = kvp.Value;

                while (pool.Count > 0)
                {
                    GameObject obj = pool.Dequeue();
                    if (obj == null)
                        continue;

                    if (!obj.activeSelf && (obj.hideFlags & HideFlags.DontSave) != 0)
                    {
                        //Remove corresponding source prefab link aswell
                        instanceToPrefab.Remove(obj);
                        
                        if (Application.isPlaying)
                            Destroy(obj);
                        else
                            DestroyImmediate(obj);
                    }
                    else
                    {
                        pool.Enqueue(obj);
                    }
                }
            }

            //Check stray children
            Transform[] allObjects = GetComponentsInChildren<Transform>(true);

            for (int i = 1; i < allObjects.Length; i++)
            {
                if(!allObjects[i]) continue;
                
                GameObject obj = allObjects[i].gameObject;
                if (obj == null)
                    continue;

                if (!obj.activeSelf && (obj.hideFlags & HideFlags.DontSave) != 0)
                {
                    if (Application.isPlaying)
                        Destroy(obj);
                    else
                        DestroyImmediate(obj);
                }
            }
        }

        private void DisableAllInstances()
        {
            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i] && instances[i].activeSelf)
                {
                    instances[i].SetActive(false);
                    instances[i].hideFlags |= HideFlags.DontSave;
                }
            }
        }
    }
}
