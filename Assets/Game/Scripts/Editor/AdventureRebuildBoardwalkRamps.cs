using UnityEngine;
using UnityEditor;

public static class AdventureRebuildBoardwalkRamps
{
    [MenuItem("Adventure/Beach/Rebuild Smooth Boardwalk Ramps")]
    public static void RebuildRamps()
    {
        Terrain land = Terrain.activeTerrain ?? Object.FindFirstObjectByType<Terrain>();
        if (land == null)
        {
            Debug.LogError("No terrain found!");
            return;
        }

        // 既存の BeachEscapeStructures を探して削除
        var existingStructures = GameObject.Find("BeachEscapeStructures");
        if (existingStructures != null)
        {
            Undo.DestroyObjectImmediate(existingStructures);
        }

        var oldManagers = Object.FindObjectsByType<AdventureBeachEscapeManager>(FindObjectsSortMode.None);
        foreach (var m in oldManagers)
        {
            Undo.DestroyObjectImmediate(m.gameObject);
        }

        // 新規作成
        var go = new GameObject("AdventureBeachEscapeManager");
        Undo.RegisterCreatedObjectUndo(go, "Rebuild Beach Boardwalk Ramps");
        var manager = go.AddComponent<AdventureBeachEscapeManager>();

        // EditModeで木道と上昇気流を構築
        var root = new GameObject("BeachEscapeStructures");
        root.transform.SetParent(go.transform, false);

        float waterY = 5.5f;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var woodMat = new Material(shader);
        woodMat.SetColor("_BaseColor", new Color(0.50f, 0.40f, 0.30f, 1.0f));

        var postMat = new Material(shader);
        postMat.SetColor("_BaseColor", new Color(0.38f, 0.29f, 0.20f, 1.0f));

        Vector3 center = new Vector3(512f, 0f, 512f);
        if (land != null && land.terrainData != null)
        {
            Vector3 origin = land.transform.position;
            Vector3 size = land.terrainData.size;
            center = new Vector3(origin.x + size.x * 0.5f, 0f, origin.z + size.z * 0.5f);
        }

        var rampsRoot = new GameObject("BeachBoardwalkRamps");
        rampsRoot.transform.SetParent(root.transform, false);

        float[] angles = {
            180f, 205f, 228f, 245f, 270f, 315f, 0f, 60f
        };

        for (int i = 0; i < angles.Length; i++)
        {
            float deg = angles[i];
            float rad = deg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)).normalized;

            float rBeach = 452f;
            float rInland = 320f;
            while (rBeach > 390f && (land.SampleHeight(center + dir * rBeach) + land.transform.position.y) < waterY + 0.25f)
                rBeach -= 3f;

            Vector3 beachPoint = center + dir * rBeach;
            Vector3 inlandPoint = center + dir * rInland;

            beachPoint.y = land.SampleHeight(beachPoint) + land.transform.position.y;
            inlandPoint.y = land.SampleHeight(inlandPoint) + land.transform.position.y;

            if (Mathf.Abs(inlandPoint.y - beachPoint.y) < 0.6f && inlandPoint.y < waterY + 3.5f)
                continue;

            // 最大約8°になるよう水平距離を確保
            {
                Vector3 flat = inlandPoint - beachPoint; flat.y = 0f;
                float horiz = flat.magnitude;
                float rise = inlandPoint.y - beachPoint.y;
                float need = Mathf.Abs(rise) / 0.14f;
                if (need > horiz && horiz > 0.1f)
                {
                    inlandPoint = beachPoint - dir * need; // dir外向き → 内陸は -dir
                    inlandPoint.y = land.SampleHeight(inlandPoint) + land.transform.position.y;
                }
            }

            var rampGo = new GameObject($"BoardwalkRamp_{Mathf.RoundToInt(deg)}deg");
            rampGo.transform.SetParent(rampsRoot.transform, false);

            int segments = 36;
            float width = 4.2f;
            float startY = beachPoint.y;
            float endY = inlandPoint.y;

            Vector3[] points = new Vector3[segments + 1];
            for (int s = 0; s <= segments; s++)
            {
                float t = (float)s / segments;
                float u = t * t * (3f - 2f * t);
                Vector3 p = Vector3.Lerp(beachPoint, inlandPoint, t);
                float smoothY = Mathf.Lerp(startY, endY, u);
                float ty = land.SampleHeight(p) + land.transform.position.y;
                float lift = Mathf.Lerp(-0.06f, 0.06f, Mathf.Clamp01(t * 5f));
                p.y = Mathf.Max(ty + lift, smoothY + lift);
                points[s] = p;
            }

            for (int s = 0; s < segments; s++)
            {
                Vector3 p0 = points[s];
                Vector3 p1 = points[s + 1];
                Vector3 segCenter = (p0 + p1) * 0.5f;
                Vector3 forward = (p1 - p0);
                float length = forward.magnitude;
                if (length < 0.01f) continue;

                Vector3 fwdNorm = forward.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwdNorm).normalized;

                var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plank.name = $"Plank_{s}";
                plank.transform.SetParent(rampGo.transform, false);
                plank.transform.position = segCenter - Vector3.up * 0.25f;
                plank.transform.rotation = Quaternion.LookRotation(fwdNorm, Vector3.up);
                plank.transform.localScale = new Vector3(width, 0.80f, length * 1.08f);

                var mr = plank.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = woodMat;

                if (s % 4 == 0 || s == segments - 1)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        post.name = $"RailingPost_{s}_{(side < 0 ? "L" : "R")}";
                        post.transform.SetParent(rampGo.transform, false);
                        post.transform.position = segCenter + right * (side * (width * 0.5f - 0.15f)) + Vector3.up * 0.55f;
                        post.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);

                        var postMr = post.GetComponent<MeshRenderer>();
                        if (postMr != null) postMr.sharedMaterial = postMat;

                        var col = post.GetComponent<Collider>();
                        if (col != null) Object.DestroyImmediate(col);
                    }
                }
            }
        }

        // スタート西浜ピン留め1本
        PlaceEditorPinnedRamp(rampsRoot.transform, land, waterY, woodMat, postMat,
            new Vector2(152f, 268f), new Vector2(300f, 355f), "BoardwalkRamp_SpawnWest");

        EditorUtility.SetDirty(go);
        Debug.Log("Successfully rebuilt gentle boardwalk ramps (~half count, ~8° max grade)!");
    }

    static void PlaceEditorPinnedRamp(
        Transform parent, Terrain land, float waterY,
        Material woodMat, Material postMat,
        Vector2 beachXZ, Vector2 inlandXZ, string name)
    {
        Vector3 beachPoint = new Vector3(beachXZ.x, waterY + 0.35f, beachXZ.y);
        Vector3 inlandPoint = new Vector3(inlandXZ.x, waterY + 8f, inlandXZ.y);
        beachPoint.y = Mathf.Max(waterY + 0.2f, land.SampleHeight(beachPoint) + land.transform.position.y);
        inlandPoint.y = land.SampleHeight(inlandPoint) + land.transform.position.y;

        var rampGo = new GameObject(name);
        rampGo.transform.SetParent(parent, false);
        int segments = 28;
        float width = 4.2f;
        Vector3[] points = new Vector3[segments + 1];
        for (int s = 0; s <= segments; s++)
        {
            float t = (float)s / segments;
            Vector3 p = Vector3.Lerp(beachPoint, inlandPoint, t);
            float ty = land.SampleHeight(p) + land.transform.position.y;
            float lift = Mathf.Lerp(-0.08f, 0.08f, Mathf.Clamp01(t * 6f));
            if (t > 0.85f) lift = Mathf.Lerp(0.08f, 0.02f, (t - 0.85f) / 0.15f);
            p.y = ty + lift;
            points[s] = p;
        }
        for (int s = 0; s < segments; s++)
        {
            Vector3 p0 = points[s];
            Vector3 p1 = points[s + 1];
            Vector3 segCenter = (p0 + p1) * 0.5f;
            Vector3 forward = p1 - p0;
            float length = forward.magnitude;
            if (length < 0.01f) continue;
            Vector3 fwdNorm = forward.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwdNorm).normalized;
            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = $"Plank_{s}";
            plank.transform.SetParent(rampGo.transform, false);
            plank.transform.position = segCenter - Vector3.up * 0.25f;
            plank.transform.rotation = Quaternion.LookRotation(fwdNorm, Vector3.up);
            plank.transform.localScale = new Vector3(width, 0.80f, length * 1.08f);
            var mr = plank.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = woodMat;
            if (s % 4 == 0 || s == segments - 1)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = $"RailingPost_{s}_{(side < 0 ? "L" : "R")}";
                    post.transform.SetParent(rampGo.transform, false);
                    post.transform.position = segCenter + right * (side * (width * 0.5f - 0.15f)) + Vector3.up * 0.55f;
                    post.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
                    var postMr = post.GetComponent<MeshRenderer>();
                    if (postMr != null) postMr.sharedMaterial = postMat;
                    var col = post.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);
                }
            }
        }
    }
}
