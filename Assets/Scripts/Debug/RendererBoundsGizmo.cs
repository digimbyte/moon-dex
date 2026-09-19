using UnityEngine;

[ExecuteAlways]
public class RendererBoundsGizmo : MonoBehaviour
{
	public bool drawWhenNotSelected = false;
	public bool drawRendererBounds = true;
	public bool drawMeshLocalBounds = false;

	[Tooltip("If set, uses this renderer. Otherwise finds one on the same GameObject.")]
	public Renderer targetRenderer;

	MeshFilter meshFilter;

	void OnEnable()
	{
		if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
		meshFilter = GetComponent<MeshFilter>();
	}

	void OnDrawGizmos()
	{
		if (!drawWhenNotSelected) return;
		Draw();
	}

	void OnDrawGizmosSelected()
	{
		if (drawWhenNotSelected) return;
		Draw();
	}

	void Draw()
	{
		if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
		if (!targetRenderer) return;

		if (drawRendererBounds)
		{
			Bounds b = targetRenderer.bounds; // WORLD bounds used for culling
			Gizmos.DrawWireCube(b.center, b.size);
		}

		if (drawMeshLocalBounds && meshFilter && meshFilter.sharedMesh)
		{
			// Draw mesh.bounds transformed into world
			Bounds lb = meshFilter.sharedMesh.bounds; // LOCAL
			Matrix4x4 m = transform.localToWorldMatrix;

			// Approx world-aligned AABB by transforming the 8 corners
			Vector3[] corners = GetBoundsCorners(lb);
			Vector3 wMin = m.MultiplyPoint3x4(corners[0]);
			Vector3 wMax = wMin;

			for (int i = 1; i < corners.Length; i++)
			{
				Vector3 p = m.MultiplyPoint3x4(corners[i]);
				wMin = Vector3.Min(wMin, p);
				wMax = Vector3.Max(wMax, p);
			}

			Bounds wb = new Bounds();
			wb.SetMinMax(wMin, wMax);

			Gizmos.DrawWireCube(wb.center, wb.size);
		}
	}

	static Vector3[] GetBoundsCorners(Bounds b)
	{
		Vector3 c = b.center;
		Vector3 e = b.extents;
		return new Vector3[]
		{
			c + new Vector3(-e.x, -e.y, -e.z),
			c + new Vector3(-e.x, -e.y,  e.z),
			c + new Vector3(-e.x,  e.y, -e.z),
			c + new Vector3(-e.x,  e.y,  e.z),
			c + new Vector3( e.x, -e.y, -e.z),
			c + new Vector3( e.x, -e.y,  e.z),
			c + new Vector3( e.x,  e.y, -e.z),
			c + new Vector3( e.x,  e.y,  e.z),
		};
	}
}
