// Spline Spawner by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//  • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//  • Uploading this file to a public GitHub repository will subject it to an automated DMCA takedown request.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace sc.splines.spawner.runtime
{
    public partial class SplineSpawner : MonoBehaviour
    {
        private void ValidateContainers()
        {
            containers.RemoveAll(item => !item || item.transform.parent != root);

            int adopted = 0;
            //If the root is empty, containers will not be children of the spawner.
            //Duplicating a spawner would mean the containers aren't duplicated
            for (int i = 0; i < containers.Count; i++)
            {
                //Belongs to the original, manually duplicate and adopt it
                if (containers[i].owner != this)
                {
                    SplineInstanceContainer newContainer = GameObject.Instantiate(containers[i], root);
                    newContainer.owner = this;
                    
                    containers[i] = newContainer;

                    adopted++;
                }
            }
            
            //Scan only top level child objects
            int childObjectCount = this.transform.childCount;
            for (int i = 0; i < childObjectCount; i++)
            {
                Transform child = transform.GetChild(i);
                SplineInstanceContainer container = child.GetComponent<SplineInstanceContainer>();
                
                if(!container) continue;
                
                if(container.owner == null || container.owner != this)
                {
                    container.owner = this;
                    adopted++;
                }
            }

            if (adopted > 0)
            {
                Debug.Log($"[Spline Spawner] {adopted} orphaned instances containers were found under {this.name}. So they have been adopted. This may happen when duplicating a Spline Spawner");
            }
            
            if (splineCount != containers.Count)
            {
                //Debug.LogWarning($"Mismatching number of object containers ({containers.Count}) relative to the number of splines ({splineCount}). Synchronizing them now. This may happen if containers are manually deleted, or the spline container was changed.");

                //Remove excess containers
                for (int i = containers.Count - 1; i >= splineCount; i--)
                {
                    containers[i].Destroy();
                    containers.RemoveAt(i);
                }

                //Add missing containers
                for (int i = containers.Count; i < splineCount; i++)
                {
                    containers.Add(SplineInstanceContainer.Create(this, i));
                }
            }
        }
        
        public bool HasMissingPrefabs()
        {
            for (int i = 0; i < inputObjects.Count; i++)
            {
                if (!inputObjects[i].prefab) return true;
            }

            return false;
        }
    }
}