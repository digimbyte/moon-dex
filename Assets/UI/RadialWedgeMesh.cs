using System.Collections.Generic;
using UnityEngine;

public static class RadialWedgeMeshTool
{
	public struct Params
	{
		// meters
		public float innerDiameter;
		public float radialThickness; // outerRadius - innerRadius
		public float extrusionDepth;  // z thickness

		// degrees
		public float wedgeDegrees;       // allocated slice width
		public float offsetDegrees;      // allocated start angle (not center)
		public float neighborGapDegrees; // padding between neighbors (shrink effective arc)

		// detail
		public int arcSegments;

		// options
		public bool recalcNormals;
		public bool recalcBounds;

		public static Params Default => new Params
		{
			innerDiameter = 0.5f,
			radialThickness = 0.15f,
			extrusionDepth = 0.02f,
			wedgeDegrees = 45f,
			offsetDegrees = 0f,
			neighborGapDegrees = 2f,
			arcSegments = 24,
			recalcNormals = true,
			recalcBounds = true
		};
	}

	// Reusable working buffers to reduce allocations (single-thread use).
	// If you build from multiple threads (you shouldn�t in Unity), remove static buffers.
	static readonly List<Vector3> _v = new(1024);
	static readonly List<Vector2> _uv = new(1024);
	static readonly List<Color32> _col = new(1024);
	static readonly List<int> _tris = new(2048);

	static readonly Color32 InnerC = new(255, 255, 255, 0);   // alpha=0
	static readonly Color32 OuterC = new(255, 255, 255, 255); // alpha=255

	/// <summary>
	/// Builds/overwrites the provided Mesh with an open 3D wedge shell.
	/// - Local plane is XY, extruded +Z; only back, inner and outer faces
	/// - Vertex color alpha marks inner/outer radial class:
	///     outer verts alpha=255, inner verts alpha=0
	/// </summary>
	public static void BuildInto(Mesh mesh, in Params p)
	{
		if (mesh == null) return;

		float innerR = Mathf.Max(0f, p.innerDiameter * 0.5f);
		float outerR = Mathf.Max(innerR + 0.0001f, innerR + Mathf.Max(0.0001f, p.radialThickness));
		float z0 = 0f;
		float z1 = Mathf.Max(0.0001f, p.extrusionDepth);

		int seg = Mathf.Clamp(p.arcSegments, 1, 4096);

		// Gap = padding: shrink effective degrees while keeping wedge centered in its allocation
		float effDeg = Mathf.Clamp(p.wedgeDegrees - Mathf.Max(0f, p.neighborGapDegrees), 0.001f, 360f);
		float centerDeg = p.offsetDegrees + (p.wedgeDegrees * 0.5f);
		float startDeg = centerDeg - (effDeg * 0.5f);
		float endDeg = centerDeg + (effDeg * 0.5f);

		// Precompute arc points
		var inner2 = new Vector2[seg + 1];
		var outer2 = new Vector2[seg + 1];

		for (int i = 0; i <= seg; i++)
		{
			float t = (float)i / seg;
			float deg = Mathf.Lerp(startDeg, endDeg, t);
			float rad = deg * Mathf.Deg2Rad;
			float c = Mathf.Cos(rad);
			float s = Mathf.Sin(rad);

			inner2[i] = new Vector2(c * innerR, s * innerR);
			outer2[i] = new Vector2(c * outerR, s * outerR);
		}

		_v.Clear(); _uv.Clear(); _col.Clear(); _tris.Clear();

		void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
					 Vector2 uva, Vector2 uvb, Vector2 uvc, Vector2 uvd,
					 Color32 ca, Color32 cb, Color32 cc, Color32 cd)
		{
			int idx = _v.Count;
			_v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
			_uv.Add(uva); _uv.Add(uvb); _uv.Add(uvc); _uv.Add(uvd);
			_col.Add(ca); _col.Add(cb); _col.Add(cc); _col.Add(cd);

			_tris.Add(idx + 0); _tris.Add(idx + 1); _tris.Add(idx + 2);
			_tris.Add(idx + 0); _tris.Add(idx + 2); _tris.Add(idx + 3);
		}

		// BACK face (z1) reversed winding
		for (int i = 0; i < seg; i++)
		{
			float t0 = (float)i / seg;
			float t1 = (float)(i + 1) / seg;

			var bi0 = new Vector3(inner2[i].x, inner2[i].y, z1);
			var bo0 = new Vector3(outer2[i].x, outer2[i].y, z1);
			var bo1 = new Vector3(outer2[i + 1].x, outer2[i + 1].y, z1);
			var bi1 = new Vector3(inner2[i + 1].x, inner2[i + 1].y, z1);

			AddQuad(bi0, bo0, bo1, bi1,
					new Vector2(t0, 0), new Vector2(t0, 1), new Vector2(t1, 1), new Vector2(t1, 0),
					InnerC, OuterC, OuterC, InnerC);
		}

		// OUTER wall
		for (int i = 0; i < seg; i++)
		{
			float t0 = (float)i / seg;
			float t1 = (float)(i + 1) / seg;

			var o0f = new Vector3(outer2[i].x, outer2[i].y, z0);
			var o1f = new Vector3(outer2[i + 1].x, outer2[i + 1].y, z0);
			var o1b = new Vector3(outer2[i + 1].x, outer2[i + 1].y, z1);
			var o0b = new Vector3(outer2[i].x, outer2[i].y, z1);

			AddQuad(o0f, o1f, o1b, o0b,
					new Vector2(t0, 0), new Vector2(t1, 0), new Vector2(t1, 1), new Vector2(t0, 1),
					OuterC, OuterC, OuterC, OuterC);
		}

		// INNER wall (reverse so normals point into the hole)
		for (int i = 0; i < seg; i++)
		{
			float t0 = (float)i / seg;
			float t1 = (float)(i + 1) / seg;

			var i0f = new Vector3(inner2[i].x, inner2[i].y, z0);
			var i1f = new Vector3(inner2[i + 1].x, inner2[i + 1].y, z0);
			var i1b = new Vector3(inner2[i + 1].x, inner2[i + 1].y, z1);
			var i0b = new Vector3(inner2[i].x, inner2[i].y, z1);

			AddQuad(i1f, i0f, i0b, i1b,
					new Vector2(t1, 0), new Vector2(t0, 0), new Vector2(t0, 1), new Vector2(t1, 1),
					InnerC, InnerC, InnerC, InnerC);
		}

		mesh.Clear();
		mesh.SetVertices(_v);
		mesh.SetTriangles(_tris, 0);
		mesh.SetUVs(0, _uv);
		mesh.SetColors(_col);

		if (p.recalcNormals) mesh.RecalculateNormals();
		if (p.recalcBounds) mesh.RecalculateBounds();
	}
}
