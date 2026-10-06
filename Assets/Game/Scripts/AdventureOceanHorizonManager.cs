using UnityEngine;

/// <summary>
/// 直線的で不自然な四角い海面と水平線を解消し、
/// 遥か地平線まで360度広がる「リアルで自然な曲線の丸い水平線（Curved Ocean Horizon）」を構築するマネージャー。
/// </summary>
[ExecuteAlways]
public class AdventureOceanHorizonManager : MonoBehaviour
{
    private const string HorizonDiscName = "OceanHorizonCurvedDisc";
    private const float HorizonRadius = 10000f; // 半径10kmの雄大な円形大洋
    private const int Segments = 128;            // 128角形の滑らかな円弧

    private void Awake()
    {
        EnsureCurvedHorizon();
    }

    private void Start()
    {
        EnsureCurvedHorizon();
    }

    [ContextMenu("Ensure Curved Horizon")]
    public void EnsureCurvedHorizon()
    {
        // 1. メイン海面 OceanPlane のスケール拡張（12,000m）
        var oceanGo = GameObject.Find("OceanPlane");
        Material oceanMat = null;
        if (oceanGo != null)
        {
            oceanGo.transform.position = new Vector3(512f, 5.5f, 512f);
            oceanGo.transform.localScale = new Vector3(1200f, 1f, 1200f);

            var oceanMf = oceanGo.GetComponent<MeshFilter>();
            if (oceanMf != null && (oceanMf.sharedMesh == null || oceanMf.sharedMesh.name != "OceanHorizonDiscMesh"))
            {
                oceanMf.sharedMesh = CreateHorizonDiscMesh();
            }

            var rend = oceanGo.GetComponent<Renderer>();
            if (rend != null)
            {
                oceanMat = rend.sharedMaterial;
                if (oceanMat != null)
                {
                    oceanMat.SetFloat("_Depth_Distance", 2.5f);
                    oceanMat.SetFloat("_Depth", 5.5f);
                    oceanMat.SetFloat("_Distance", 24.0f);
                    oceanMat.SetFloat("_Coast_Opacity", 0.002f);
                    oceanMat.SetFloat("_CoastOpacity", 0.004f);
                    oceanMat.SetFloat("_EdgeFade", 0.95f);
                    oceanMat.SetFloat("_Normal_Strength", 0.55f);
                    oceanMat.SetFloat("_Water_Speed", 0.55f);
                    oceanMat.SetFloat("_Wave_Strength", 0.42f);
                    oceanMat.SetFloat("_Smoothness", 0.98f);
                    oceanMat.SetColor("_Shallow_Color", new Color(0.18f, 0.88f, 0.94f, 0.38f));
                    oceanMat.SetColor("_Deep_Color", new Color(0.04f, 0.48f, 0.82f, 0.80f));
                    oceanMat.SetColor("_Surface_Color", new Color(0.15f, 0.72f, 0.88f, 0.55f));
                    oceanMat.SetColor("_SpecColor", new Color(1.0f, 1.0f, 1.0f, 1.0f));
                }
            }
        }

        // 2. カメラの farClipPlane を 12,000m に保証
        var mainCam = Camera.main;
        if (mainCam != null && mainCam.farClipPlane < 8000f)
        {
            mainCam.farClipPlane = 12000f;
        }

        // 3. 円盤状の曲面大洋メッシュ（OceanHorizonCurvedDisc）の構築
        if (oceanGo != null)
        {
            var discTr = oceanGo.transform.Find(HorizonDiscName);
            GameObject discGo = discTr != null ? discTr.gameObject : null;

            if (discGo == null)
            {
                discGo = new GameObject(HorizonDiscName);
                discGo.transform.SetParent(oceanGo.transform, false);
                discGo.transform.localPosition = Vector3.zero;
                discGo.transform.localRotation = Quaternion.identity;
                // 親がスケール1200なので、ローカルスケールは HorizonRadius / (1200 * 5) など適切に正規化
                discGo.transform.localScale = Vector3.one;
            }

            var mf = discGo.GetComponent<MeshFilter>();
            if (mf == null) mf = discGo.AddComponent<MeshFilter>();

            var mr = discGo.GetComponent<MeshRenderer>();
            if (mr == null) mr = discGo.AddComponent<MeshRenderer>();

            if (oceanMat != null)
            {
                mr.sharedMaterial = oceanMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = true;
            }

            if (mf.sharedMesh == null || mf.sharedMesh.name != "OceanHorizonDiscMesh")
            {
                mf.sharedMesh = CreateHorizonDiscMesh();
            }

            // 4. 深海底プレート（OceanFloorExtendedDisc）の構築
            // 海底の深さを水深5.5m（標高0.0m）の完全な深海に均一化し、Terrain外枠の直線深度断絶をゼロにする
            EnsureDeepOceanFloor(oceanGo);
        }
    }

    private static void EnsureDeepOceanFloor(GameObject oceanGo)
    {
        const string FloorDiscName = "OceanFloorExtendedDisc";
        var floorTr = oceanGo.transform.Find(FloorDiscName);
        GameObject floorGo = floorTr != null ? floorTr.gameObject : null;

        if (floorGo == null)
        {
            floorGo = new GameObject(FloorDiscName);
            floorGo.transform.SetParent(oceanGo.transform, false);
            // 親(OceanPlane Y=5.5m)に対して、標高0.0mにするためローカルY = -5.5f / 1f = -5.5f
            floorGo.transform.localPosition = new Vector3(0f, -5.5f, 0f);
            floorGo.transform.localRotation = Quaternion.identity;
            floorGo.transform.localScale = Vector3.one;
        }

        var mf = floorGo.GetComponent<MeshFilter>();
        if (mf == null) mf = floorGo.AddComponent<MeshFilter>();

        var mr = floorGo.GetComponent<MeshRenderer>();
        if (mr == null) mr = floorGo.AddComponent<MeshRenderer>();

        // 深海底用マテリアル（暗いインディゴ・深海色：グレーの岩肌枠を完全排除）
        var floorMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/RustAndFloat/Materials/OceanDeepFloor_URP.mat")
            ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/RustAndFloat/Materials/ParadiseOcean_URP.mat");
        if (floorMat != null)
        {
            mr.sharedMaterial = floorMat;
        }

        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        if (mf.sharedMesh == null || mf.sharedMesh.name != "OceanFloorDiscMesh")
        {
            mf.sharedMesh = CreateHorizonDiscMesh();
        }
    }

    /// <summary>
    /// 360度どこを見ても四角い直線境界が見えない、滑らかな円弧を描く巨大円盤メッシュを生成
    /// </summary>
    private static Mesh CreateHorizonDiscMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "OceanHorizonDiscMesh";

        // 親の OceanPlane (scale 1200, Unity Planeサイズ 10m x 10m = 幅12000m)
        // ローカル座標系での半径（親ローカルで 1.0 = 5m なので、半径 6000m 〜 10000m をカバー）
        float localRadius = 1.0f; // 親の正方形（-0.5〜+0.5）の外周外接円

        int vertexCount = Segments + 1;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[Segments * 3];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        float angleStep = (Mathf.PI * 2f) / Segments;
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * angleStep;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // ローカルスケールで 1.15倍（外接円）
            float r = localRadius * 1.15f;
            vertices[i + 1] = new Vector3(cos * r, 0f, sin * r);
            uvs[i + 1] = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);

            int triIndex = i * 3;
            triangles[triIndex] = 0;
            triangles[triIndex + 1] = (i + 1 == Segments) ? 1 : i + 2;
            triangles[triIndex + 2] = i + 1;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}
