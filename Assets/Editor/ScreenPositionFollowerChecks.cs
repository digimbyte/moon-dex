using System;
using UnityEditor;
using UnityEngine;

public static class ScreenPositionFollowerChecks
{
    [MenuItem("Tools/MoonDex/Run Screen Follower Checks")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play mode.");
        var parent = new GameObject("FollowerCheckParent");
        var cameraObject = new GameObject("FollowerCheckCamera");
        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -20);
            var root = new GameObject("Root").transform;
            root.SetParent(parent.transform, false);
            root.localPosition = new Vector3(1, 2, 3);
            var child = new GameObject("Child").transform;
            child.SetParent(root, false);
            child.localPosition = new Vector3(2, 1, 0);
            var nested = new GameObject("Nested").transform;
            nested.SetParent(child, false);
            nested.localPosition = new Vector3(-1, 2, 0.5f);
            var follower = root.gameObject.AddComponent<ScreenPositionFollower>();
            follower.sourceCamera = camera;
            follower.CaptureOrigin();
            var pivot = new GameObject("WorldPivot").transform;
            pivot.SetParent(parent.transform, false);
            pivot.position = camera.ViewportToWorldPoint(new Vector3(0.3f, 0.65f, 10f));
            follower.SetPivot(pivot);
            var origin = follower.Origin;
            var childPosition = child.localPosition;
            var nestedPosition = nested.localPosition;
            foreach (var anchor in new[] { root, child, nested })
            {
                Check(follower.SetAlignmentAnchor(anchor), "Anchor accepted");

                Check(follower.Evaluate(), "Projection solved");
                var screen = camera.WorldToViewportPoint(anchor.position);
                Check(Mathf.Abs(screen.x - 0.3f) < 0.001f && Mathf.Abs(screen.y - 0.65f) < 0.001f, "Anchor screen alignment");
                Check(child.localPosition == childPosition && nested.localPosition == nestedPosition, "Child offsets preserved");
            }
            parent.transform.SetPositionAndRotation(new Vector3(2, 1, 0), Quaternion.Euler(10, 20, 15));
            parent.transform.localScale = new Vector3(2, 1.5f, 0.8f);
            nested.localPosition += Vector3.right;
            pivot.position = camera.ViewportToWorldPoint(new Vector3(0.3f, 0.65f, 10f));
            Check(follower.Evaluate(), "Transformed parent and moving child");
            var projected = camera.WorldToViewportPoint(nested.position);
            Check(Mathf.Abs(projected.x - 0.3f) < 0.001f && Mathf.Abs(projected.y - 0.65f) < 0.001f, "Nested alignment with parent scale");
            follower.enabled = false;
            follower.enabled = true;
            Check(follower.Origin == origin, "Origin retained");
            follower.limitBox = follower.limitDistance = true;
            follower.boxSize = new Vector3(4, 4, 4);
            follower.maximumDisplacement = 1.5f;
            follower.moveX = false;
            var current = origin + Vector3.right;
            Check(Constrain(follower, current, origin + new Vector3(1, 10, 10), out var result), "Combined limits");
            Check(result.x == current.x && (result - origin).magnitude <= 1.5001f, "Locked axis and radius");
            Check(!Constrain(follower, origin + Vector3.right * 3, origin, out result), "Impossible locked axis rejected");
            follower.limitBox = follower.limitDistance = false;
            follower.moveX = true;
            follower.placement = ScreenPositionFollower.PlacementMode.RayEndpoint;
            follower.rayLength = 8f;
            Check(follower.Evaluate(), "Ray endpoint");
            var ray = camera.ViewportPointToRay(new Vector3(0.3f, 0.65f, 0));
            Check(Vector3.Distance(nested.position, ray.GetPoint(8f)) < 0.001f, "Endpoint alignment");
            follower.placement = ScreenPositionFollower.PlacementMode.SurfaceIntersection;
            pivot.position = ray.GetPoint(2f);
            follower.surfaceLayers = 0;
            Check(follower.Evaluate() && Vector3.Distance(nested.position, ray.GetPoint(8f)) < 0.001f, "Surface miss fallback");
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.transform.SetParent(parent.transform, true);
            surface.transform.position = ray.GetPoint(4f);
            surface.transform.rotation = Quaternion.identity;
            surface.transform.localScale = Vector3.one;
            follower.surfaceLayers = ~0;
            Physics.SyncTransforms();
            Check(follower.Evaluate(), "Surface hit solved");
            Check(Vector3.Distance(nested.position, ray.origin) < 8f, "Surface hit replaces endpoint");
            follower.SetPivot(surface.transform);
            follower.pixelOffset = new Vector2(15, -10);
            follower.boundsPercentOffset = new Vector2(50, 0);
            Check(follower.Evaluate(), "Reference bounds offsets");
            Vector3 referencePixel = camera.WorldToScreenPoint(surface.transform.position);
            Vector3 anchorPixel = camera.WorldToScreenPoint(nested.position);
            Check(anchorPixel.x > referencePixel.x + 15f && Mathf.Abs(anchorPixel.y - referencePixel.y + 10f) < 0.01f, "Projected offsets applied");
            surface.transform.position = camera.transform.position - camera.transform.forward;
            Vector3 beforeInvalid = root.localPosition;
            Check(!follower.Evaluate() && root.localPosition == beforeInvalid, "Behind-camera reference holds position");
            Check(!follower.SetAlignmentAnchor(surface.transform), "Unrelated anchor rejected");
            follower.SetPivot(pivot);
            follower.placement = ScreenPositionFollower.PlacementMode.OriginDepthPlane;
            follower.pixelOffset = Vector2.zero;
            follower.boundsPercentOffset = Vector2.zero;
            parent.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            parent.transform.localScale = Vector3.one;
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, 90, 0));
            root.position = camera.transform.forward * 15f;
            follower.CaptureOrigin();
            follower.moveX = false;
            follower.moveY = true;
            follower.moveZ = false;
            pivot.position = camera.ViewportToWorldPoint(new Vector3(0.8f, 0.7f, 5f));
            Vector3 beforeAxis = camera.WorldToViewportPoint(nested.position);
            Check(follower.Evaluate(), "X-facing camera vertical tracking");
            Vector3 afterAxis = camera.WorldToViewportPoint(nested.position);
            Check(Mathf.Abs(afterAxis.x - beforeAxis.x) < 0.001f && Mathf.Abs(afterAxis.y - 0.7f) < 0.001f
                && Mathf.Abs(afterAxis.z - beforeAxis.z) < 0.001f, "Camera axes preserve horizontal and depth");
            Debug.Log("Screen follower checks passed; invalid-reference checks log expected warnings.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parent);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
    static bool Constrain(ScreenPositionFollower follower, Vector3 current, Vector3 desired, out Vector3 result)
    {
        var method = typeof(ScreenPositionFollower).GetMethod("TryConstrain", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        object[] args = { current, desired, Vector3.zero };
        bool success = (bool)method.Invoke(follower, args);
        result = (Vector3)args[2];
        return success;
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Screen follower check failed: " + message);
    }
}
