using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Run outside Play mode; uses temporary objects and never saves the scene.
public static class RadialMenuLifecycleChecks
{
    [MenuItem("Tools/MoonDex/Run Lifecycle Checks")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run lifecycle checks outside Play mode.");
        var host = new GameObject("LifecycleCheck");
        var prefab = new GameObject("LifecycleCheckItem");
        var infoPanel = new GameObject("LifecycleInfoPanel");
        Mesh foreign = null;
        Material material = null;
        try
        {
            host.SetActive(false);
            prefab.SetActive(false);
            prefab.AddComponent<MenuButton>();
            prefab.AddComponent<LifecycleFailureHost>();
            var foreignObject = new GameObject("TextMesh");
            foreignObject.transform.SetParent(prefab.transform, false);
            foreign = new Mesh { hideFlags = HideFlags.DontSave };
            foreign.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            foreignObject.AddComponent<MeshFilter>().sharedMesh = foreign;
            material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            var tracks = host.AddComponent<SplineSync>();
            var menu = host.AddComponent<RadialMenuFromYaml>();
            menu.rebuildOnEnable = false;
            menu.enableFallbackPhysicsInput = false;
            menu.leafInfoPanel = infoPanel;
            menu.itemRootPrefab = prefab;
            menu.wedgeMaterial = material;
            var leaf = new RadialMenuFromYaml.MenuNode { id = "leaf", label = "Leaf" };
            var mining = new RadialMenuFromYaml.MenuNode { id = "mining", label = "Mining" };
            mining.children.Add(leaf);
            mining.children.Add(new RadialMenuFromYaml.MenuNode { id = "second_leaf", label = "Second Leaf" });
            var tools = new RadialMenuFromYaml.MenuNode { id = "tools", label = "Tools" };
            tools.children.Add(mining);
            var vehicles = new RadialMenuFromYaml.MenuNode { id = "vehicles", label = "Vehicles" };
            vehicles.children.Add(new RadialMenuFromYaml.MenuNode { id = "rover", label = "Rover" });
            var root = new RadialMenuFromYaml.MenuNode { id = "root", label = "Root" };
            root.children.Add(tools);
            root.children.Add(vehicles);
            host.SetActive(true);
            // Activate the template only after its builder is configured.
            prefab.SetActive(true);
            menu.Open(root);
            var rootRing = host.transform.Find("Ring_0");
            Check(rootRing != null && rootRing.Find("Gimbal/Contents").childCount == 2, "Complete root ring inside gimbal");
            var ringFollower = rootRing.Find("Gimbal").GetComponent<ScreenPositionFollower>();
            Check(ringFollower != null && ringFollower.moveX && !ringFollower.moveY && !ringFollower.moveZ
                && ringFollower.placement == ScreenPositionFollower.PlacementMode.OriginDepthPlane,
                "Ring gimbal moves only screen left/right at its own depth");
            Check(Mathf.Approximately(tracks.Percentage, 0f), "Root track position");
            for (int i = 0; i < 5; i++)
            {
                Find(menu, "tools").OnMeshClicked(null);
                Check(Mathf.Approximately(tracks.Percentage, 1f / 3f), "Cached Tools track position");
                Find(menu, "mining").OnMeshClicked(null);
                Check(Mathf.Approximately(tracks.Percentage, 2f / 3f), "Cached Mining track position");
                var leafButton = Find(menu, "leaf");
                var events = leafButton.MeshChild.GetComponent<RadialMenuMeshEvents>();
                var resolved = typeof(RadialMenuMeshEvents).GetProperty("Button", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(events);
                Check(ReferenceEquals(resolved, leafButton) && leafButton is ItemButton, "Leaf raycast target");
                var stationary = leafButton.transform.Find("StationaryHitArea");
                var stationaryCollider = stationary.GetComponent<MeshCollider>();
                var animatedCollider = leafButton.MeshChild.GetComponent<MeshCollider>();
                Check(stationaryCollider != null && animatedCollider != null
                    && stationaryCollider.sharedMesh != leafButton.MeshChild.GetComponent<MeshFilter>().sharedMesh, "Independent button colliders");
                var restingVertices = stationaryCollider.sharedMesh.vertices;
                var restingPosition = stationary.localPosition;
                leafButton.OnMeshHoverEnter(null);
                leafButton.MeshChild.localPosition += Vector3.forward;
                Check(stationary.localPosition == restingPosition, "Stationary collider does not animate");
                var afterHover = stationaryCollider.sharedMesh.vertices;
                Check(restingVertices.Length == afterHover.Length, "Stationary geometry retained");
                for (int v = 0; v < restingVertices.Length; v++)
                    Check(restingVertices[v] == afterHover[v], "Stationary vertices do not deform");
                var stationaryEvents = stationary.GetComponent<RadialMenuMeshEvents>();
                var stationaryTarget = typeof(RadialMenuMeshEvents).GetProperty("Button", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stationaryEvents);
                Check(ReferenceEquals(stationaryTarget, leafButton), "Both colliders target the same button");
                leafButton.OnMeshClicked(stationaryEvents);
                Check(menu.ActiveNodeId == "leaf", "Stationary collider selects leaf");
                Check(infoPanel.activeSelf, "Leaf shows information panel");
                Check(Mathf.Approximately(tracks.Percentage, 1f), "Leaf track endpoint");
                Find(menu, "second_leaf").OnMeshClicked(null);
                Check(menu.ActiveNodeId == "second_leaf" && infoPanel.activeSelf, "Leaf-to-leaf replaces selection and keeps panel open");
                menu.Back();
                Check(menu.ActiveNodeId == "mining", "Back after switching leaves returns to branch, not previous leaf");
                Check(!infoPanel.activeSelf, "Back hides information panel");
                leafButton.OnMeshHoverExit(null);
                leafButton.OnMeshClicked(null);
                menu.Back();
                menu.Back();
                Check(host.transform.Find("Ring_2") == null, "Deeper ring removed");
                var retired = Find(menu, "mining");
                retired.OnMeshHoverEnter(null);
                Find(menu, "vehicles").OnMeshClicked(null);
                Check(Mathf.Approximately(tracks.Percentage, 0.5f), "Shorter branch track position");
                retired.OnMeshHoverExit(null);
                Check(host.transform.Find("Ring_0") == rootRing, "Ancestor retained");
                Check(host.transform.Find("Ring_1/Gimbal/Contents").childCount == 1 && Find(menu, "rover") != null, "Complete replacement");
                menu.Back();
            }
            Check(foreign != null && foreign.vertexCount == 3, "Foreign mesh untouched");
            var get = typeof(RadialMenuFromYaml).GetMethod("GetPooledMesh", BindingFlags.Instance | BindingFlags.NonPublic);
            var release = typeof(RadialMenuFromYaml).GetMethod("ReleasePooledMesh", BindingFlags.Instance | BindingFlags.NonPublic);
            var dead = (Mesh)get.Invoke(menu, null);
            release.Invoke(menu, new object[] { dead });
            UnityEngine.Object.DestroyImmediate(dead);
            var live = (Mesh)get.Invoke(menu, null);
            Check(live != null, "Destroyed pool entry skipped");
            release.Invoke(menu, new object[] { live });
            // Fail after one item was created, exercising rollback of partial geometry.
            var broken = new RadialMenuFromYaml.MenuNode { id = "broken" };
            broken.children.Add(leaf);
            broken.children.Add(new RadialMenuFromYaml.MenuNode { id = "fail", label = "Fail" });
            string activeBefore = menu.ActiveNodeId;
            menu.Open(broken); // One expected exception is logged by the builder.
            Check(menu.ActiveNodeId == activeBefore && host.transform.Find("Ring_0") == rootRing
                && host.transform.childCount == 1, "Failed build rolled back");
            var owned = (IEnumerable)typeof(RadialMenuFromYaml).GetField("_ownedMeshes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
            var meshes = new System.Collections.Generic.List<Mesh>();
            foreach (Mesh mesh in owned) meshes.Add(mesh);
            UnityEngine.Object.DestroyImmediate(host);
            foreach (var mesh in meshes) Check(mesh == null, "Owned mesh disposed");
            Check(foreign != null, "Foreign mesh survives builder disposal");
            Debug.Log("MoonDex lifecycle checks passed. The deliberately failed build logs one expected exception.");
        }
        finally
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(prefab);
            UnityEngine.Object.DestroyImmediate(infoPanel);
            if (foreign != null) UnityEngine.Object.DestroyImmediate(foreign);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }
    }

    static MenuButtonBase Find(RadialMenuFromYaml owner, string id)
    {
        foreach (var button in owner.GetComponentsInChildren<MenuButtonBase>())
            if (button.enabled && button.NodeId == id) return button;
        throw new Exception("Missing menu item: " + id);
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Lifecycle check failed: " + message);
    }
}

public sealed class LifecycleFailureHost : MonoBehaviour, IRadialMenuItemHost
{
    public void Bind(RadialMenuFromYaml.MenuNode node, int index, float startDeg, float wedgeDeg)
    {
        if (node.id == "fail") throw new InvalidOperationException("Expected lifecycle test failure.");
    }
}
