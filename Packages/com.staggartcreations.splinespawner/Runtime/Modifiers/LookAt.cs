using System;
using sc.splines.spawner.runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Splines;

namespace sc.splines.spawner.runtime
{
    [Serializable]
    [Modifier("Look At", "Rotates objects towards a certain element")]
    public class LookAt : Modifier
    {
        public enum Target
        {
            SplineCurve,
            Transform,
            MainCamera,
            SceneViewCamera
        }

        public Target target;
        public Transform targetTransform;
        public bool reverse;
        public bool3 angleLock;
        
        [BurstCompile]
        private struct Job : IJobParallelFor
        {
            private readonly Target target;
            private readonly float3 targetTransformPosition;
            private readonly bool reverse;
            private readonly bool3 angleLock;
            
            [NativeDisableParallelForRestriction]
            private NativeList<SpawnPoint> spawnPoints;

            public Job(LookAt settings, ref NativeList<SpawnPoint> spawnPoints)
            {
                this.spawnPoints = spawnPoints;

                this.target = settings.target;
                this.targetTransformPosition = settings.targetTransform ? settings.targetTransform.position : math.float3(0f);
                
                if (target == Target.MainCamera)
                {
                    Camera mainCamera = Camera.main;
                    
                    if(mainCamera) targetTransformPosition = mainCamera.transform.position;
                }
                #if UNITY_EDITOR
                if (target == Target.SceneViewCamera)
                {
                    UnityEditor.SceneView sceneView = UnityEditor.SceneView.lastActiveSceneView;

                    if (sceneView)
                    {
                        targetTransformPosition = sceneView.camera.transform.position;
                    }
                }
                #endif
                
                this.reverse = settings.reverse;
                this.angleLock = settings.angleLock;
            }
            
            public void Execute(int i)
            {
                SpawnPoint spawnPoint = spawnPoints[i];
                
                //Skip any spawn points that were previously invalidated
                if(spawnPoint.isValid == false) return;

                //if (target == Target.Transform && math.any(targetTransformPosition)) return;
                
                //Current position of spawn point
                float3 position = spawnPoint.position;
                
                //Nearest point on spline
                float3 targetPosition = spawnPoint.context.position;

                if (target != Target.SplineCurve)
                {
                    targetPosition = targetTransformPosition;
                }

                float3 direction = math.normalize(targetPosition - position);
                if (math.length(direction) < 0.02f) return;

                if (reverse) direction = -direction;
                
                quaternion targetRotation = quaternion.LookRotationSafe(direction, math.up());

                quaternion newRotation = SplineFunctions.LockRotationAngle(spawnPoint.rotation, targetRotation, angleLock);
                
                spawnPoint.rotation = newRotation;

                //Reassign the modified spawn point back
                spawnPoints[i] = spawnPoint;
            }
        }
        
        public override JobHandle CreateJob(SplineSpawner spawner, ref NativeList<SpawnPoint> spawnPoints)
        {
            Job job = new Job(this, ref spawnPoints);
            
            JobHandle jobHandle = job.Schedule(spawnPoints.Length, DEFAULT_BATCHSIZE);

            return jobHandle;
        }
    }
}