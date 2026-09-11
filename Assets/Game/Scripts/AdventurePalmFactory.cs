using UnityEngine;

public static class AdventurePalmFactory
{
    public static void ResetMaterials()
    {
        _trunk = null;
        _leaf = null;
        _nut = null;
    }

    public static GameObject Create(Transform parent, Vector3 pos, int seed)
    {
        var rng = new System.Random(seed);
        float h = 3.2f + (float)rng.NextDouble() * 1.6f;
        float lean = ((float)rng.NextDouble() - 0.5f) * 12f;

        var palm = new GameObject("Palm");
        palm.transform.SetParent(parent);
        palm.transform.position = pos;
        palm.transform.rotation = Quaternion.Euler(lean * 0.35f, (float)rng.NextDouble() * 360f, lean);

        var trunkGo = new GameObject("Trunk");
        trunkGo.transform.SetParent(palm.transform, false);
        var trunkFilter = trunkGo.AddComponent<MeshFilter>();
        var trunkRend = trunkGo.AddComponent<MeshRenderer>();
        trunkFilter.sharedMesh = MakeTrunk(h, rng);
        trunkRend.sharedMaterial = TrunkMat();

        int fronds = 15;
        for (int i = 0; i < fronds; i++)
        {
            var frond = new GameObject("Frond");
            frond.transform.SetParent(palm.transform, false);
            float yaw = i * (360f / fronds) + (float)rng.NextDouble() * 18f;
            float dip = 6f + (float)rng.NextDouble() * 16f;
            float twist = ((float)rng.NextDouble() - 0.5f) * 10f;
            frond.transform.localPosition = new Vector3(0f, h * 0.98f, 0f);
            frond.transform.localRotation = Quaternion.Euler(dip, yaw, twist);
            var filter = frond.AddComponent<MeshFilter>();
            var rend = frond.AddComponent<MeshRenderer>();
            filter.sharedMesh = MakeFrond(2.8f + (float)rng.NextDouble() * 0.9f, rng);
            rend.sharedMaterial = LeafMat();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        int nuts = 2 + rng.Next(2);
        for (int i = 0; i < nuts; i++)
        {
            var nut = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nut.name = "Coconut";
            nut.transform.SetParent(palm.transform, false);
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            nut.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.18f, h * 0.92f, Mathf.Sin(a) * 0.18f);
            nut.transform.localScale = Vector3.one * (0.16f + (float)rng.NextDouble() * 0.06f);
            Object.DestroyImmediate(nut.GetComponent<Collider>());
            nut.GetComponent<Renderer>().sharedMaterial = NutMat();
        }

        return palm;
    }

    static Mesh MakeTrunk(float height, System.Random rng)
    {
        const int rings = 18;
        const int sides = 10;
        var verts = new Vector3[rings * sides];
        var nrms = new Vector3[rings * sides];
        var uvs = new Vector2[rings * sides];
        var tris = new int[(rings - 1) * sides * 6];
        int t = 0;
        float lean = ((float)rng.NextDouble() - 0.5f) * 0.35f;

        for (int r = 0; r < rings; r++)
        {
            float u = r / (float)(rings - 1);
            float y = u * height;
            float ring = 1f + 0.1f * Mathf.Sin(u * 42f + (float)rng.NextDouble());
            float rad = Mathf.Lerp(0.28f, 0.11f, u * u * 0.35f + u * 0.65f) * ring;
            float xOff = lean * u * u * height;
            for (int s = 0; s < sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f;
                int i = r * sides + s;
                var p = new Vector3(Mathf.Cos(a) * rad + xOff, y, Mathf.Sin(a) * rad);
                verts[i] = p;
                nrms[i] = new Vector3(Mathf.Cos(a), 0.08f, Mathf.Sin(a)).normalized;
                uvs[i] = new Vector2(s / (float)sides, u);
            }
        }

        for (int r = 0; r < rings - 1; r++)
        {
            for (int s = 0; s < sides; s++)
            {
                int a = r * sides + s;
                int b = r * sides + (s + 1) % sides;
                int c = (r + 1) * sides + s;
                int d = (r + 1) * sides + (s + 1) % sides;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
        }

        var mesh = new Mesh { name = "PalmTrunk" };
        mesh.vertices = verts;
        mesh.normals = nrms;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    static Mesh MakeFrond(float length, System.Random rng)
    {
        const int segs = 14;
        const int pairs = 16;
        var verts = new System.Collections.Generic.List<Vector3>();
        var nrms = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var tris = new System.Collections.Generic.List<int>();

        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = verts.Count;
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            nrms.Add(n); nrms.Add(n); nrms.Add(n); nrms.Add(n);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
            tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 3);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i + 1); tris.Add(i + 3); tris.Add(i + 2);
        }

        Vector3 PointOnSpine(float s)
        {
            float z = s * length;
            float y = -0.06f * s * s * length;
            return new Vector3(0f, y, z);
        }

        for (int i = 0; i < segs; i++)
        {
            float s0 = i / (float)segs;
            float s1 = (i + 1) / (float)segs;
            Vector3 p0 = PointOnSpine(s0);
            Vector3 p1 = PointOnSpine(s1);
            Vector3 side = Vector3.Cross(Vector3.up, (p1 - p0).normalized);
            if (side.sqrMagnitude < 0.01f)
                side = Vector3.right;
            side.Normalize();
            float w = Mathf.Lerp(0.045f, 0.016f, s0);
            AddQuad(p0 - side * w, p0 + side * w, p1 - side * w, p1 + side * w);
        }

        for (int i = 0; i < pairs; i++)
        {
            float s = 0.08f + i / (float)(pairs - 1) * 0.86f;
            Vector3 p = PointOnSpine(s);
            Vector3 next = PointOnSpine(Mathf.Min(1f, s + 0.06f));
            Vector3 along = (next - p).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, along);
            if (side.sqrMagnitude < 0.01f)
                side = Vector3.right;
            side.Normalize();
            float leaflet = Mathf.Lerp(1.05f, 0.18f, s * s) * (0.9f + (float)rng.NextDouble() * 0.2f);
            float half = Mathf.Lerp(0.07f, 0.03f, s);
            for (int dir = -1; dir <= 1; dir += 2)
            {
                Vector3 outer = p + side * (dir * leaflet) + along * 0.08f + Vector3.down * (0.04f + s * 0.05f);
                Vector3 innerA = p + along * half + side * (dir * 0.03f);
                Vector3 innerB = p - along * half + side * (dir * 0.03f);
                Vector3 outerA = outer + along * (half * 0.45f);
                Vector3 outerB = outer - along * (half * 0.35f);
                AddQuad(innerB, innerA, outerB, outerA);
            }
        }

        var mesh = new Mesh { name = "PalmFrond" };
        mesh.SetVertices(verts);
        mesh.SetNormals(nrms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    static Material _trunk;
    static Material _leaf;
    static Material _nut;

    static Material TrunkMat()
    {
        if (_trunk != null)
            return _trunk;
        _trunk = MakeLit(new Color(0.52f, 0.34f, 0.16f), 0.04f, 0.22f);
        return _trunk;
    }

    static Material LeafMat()
    {
        if (_leaf != null)
            return _leaf;
        _leaf = MakeLit(new Color(0.30f, 0.68f, 0.18f), 0f, 0.38f);
        _leaf.SetFloat("_Cull", 0f);
        return _leaf;
    }

    static Material NutMat()
    {
        if (_nut != null)
            return _nut;
        _nut = MakeLit(new Color(0.28f, 0.16f, 0.08f), 0.1f, 0.35f);
        return _nut;
    }

    static Material MakeLit(Color color, float metallic, float smooth)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smooth);
        mat.SetFloat("_Glossiness", smooth);
        return mat;
    }
}
