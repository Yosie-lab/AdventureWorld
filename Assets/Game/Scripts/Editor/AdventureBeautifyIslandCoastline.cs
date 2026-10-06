using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 『Rust & Float』島の縁を丸い白砂ビーチで囲み、四角い海底部分を完全消去して美しい青い海にするツール。
/// 1. 島の縁（海岸線）をなだらかな曲線の美しい白砂（SandLayer）でぐるりと丸く囲む。
/// 2. 島の外側の四角い直線海底部分（X:0~1000, Z:0~1000）を Terrain Holes（SetHoles）で完全にくり抜いて消去。
/// 3. 砂浜から先は、四角い敷地のない、どこまでも広がるエメラルド〜ブルーの海と曲線の水平線になる。
/// </summary>
public static class AdventureBeautifyIslandCoastline
{
    public const float WaterY = 5.50f;
    public static readonly Vector2 IslandCenter = new Vector2(512f, 512f);

    [MenuItem("Adventure/🏝️ Beautify Island Coastline & Remove Square Sea Bed (島の縁を丸い白砂にし四角い海底を消去)", false, 5)]
    public static void BeautifyCoastlineAndRemoveSquareSeaBed()
    {
        var terrainGo = GameObject.Find("LandTerrain");
        if (terrainGo == null)
        {
            Debug.LogError("[AdventureBeautifyIslandCoastline] LandTerrain が見つかりません。");
            return;
        }

        var terrain = terrainGo.GetComponent<Terrain>();
        var td = terrain != null ? terrain.terrainData : null;
        if (td == null)
        {
            Debug.LogError("[AdventureBeautifyIslandCoastline] TerrainData が見つかりません。");
            return;
        }

        Undo.RecordObject(td, "Beautify Island Coastline");

        // 1. テクスチャレイヤー（Alphamap）の更新：島の縁を丸く白砂で囲む
        UpdateCoastlineSandLayers(td, terrain);

        // 2. Terrain Holes（カッター）で、島の外側の四角い海底を完全に消去
        CutoutSquareOuterSeaBed(td, terrain);

        EditorUtility.SetDirty(td);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("<color=#00ffc8><b>[AdventureBeautifyIslandCoastline]</b> 島の縁を丸い白砂ビーチで囲み、四角い海底を完全に消去しました！あとは美しい青い海が広がります。</color>");
    }

    private static void UpdateCoastlineSandLayers(TerrainData td, Terrain land)
    {
        var sand = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/RustAndFloat/Terrain/SandLayer.terrainlayer");
        var grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Idyllic Fantasy Nature/Terrain Layer/Grass_Layer.terrainlayer");
        var rock = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Idyllic Fantasy Nature/Terrain Layer/Rock_Layer.terrainlayer");
        if (sand == null || grass == null || rock == null)
        {
            Debug.LogError("[AdventureBeautifyIslandCoastline] TerrainLayer のロードに失敗しました。");
            return;
        }

        td.terrainLayers = new[] { grass, sand, rock };
        int aRes = td.alphamapResolution;
        float[,,] a = new float[aRes, aRes, 3];

        for (int z = 0; z < aRes; z++)
        {
            for (int x = 0; x < aRes; x++)
            {
                float nx = x / (float)(aRes - 1);
                float nz = z / (float)(aRes - 1);
                float wx = nx * td.size.x;
                float wz = nz * td.size.z;
                float hy = land.SampleHeight(new Vector3(wx, 0f, wz));
                float ny = td.GetInterpolatedNormal(nx, nz).y;

                // 崖（急傾斜）は岩肌
                float steepness = 1f - Mathf.Clamp01((ny - 0.32f) / 0.15f);
                float rockW = Mathf.Clamp01(steepness * 2.0f);

                // 島の縁の白砂ビーチ：海面(5.5m)付近から波打ち際・海岸線(8.5m)をなだらかな白砂に
                float sandW = 0f;
                if (hy <= WaterY + 3.2f)
                {
                    // 標高 5.5m 付近の海岸線を 100% 綺麗な白砂に
                    sandW = Mathf.Clamp01(1f - (hy - (WaterY + 0.3f)) / 2.9f);
                }

                // 陸地内部（丘陵・平原）は大草原
                float grassW = Mathf.Clamp01(1f - Mathf.Max(sandW, rockW));

                float sum = grassW + sandW + rockW;
                if (sum < 0.001f)
                {
                    grassW = 1f;
                    sum = 1f;
                }

                a[z, x, 0] = grassW / sum; // grass
                a[z, x, 1] = sandW / sum;  // sand
                a[z, x, 2] = rockW / sum;  // rock
            }
        }

        td.SetAlphamaps(0, 0, a);
    }

    private static void CutoutSquareOuterSeaBed(TerrainData td, Terrain land)
    {
        int hRes = td.holesResolution;
        bool[,] holes = new bool[hRes, hRes];

        // 島の陸地判定：中心(512, 512)からの距離および標高
        // 陸地・白砂ビーチ部分は true（表示）、四角い海底部分は false（完全に穴あけ消去）
        for (int z = 0; z < hRes; z++)
        {
            for (int x = 0; x < hRes; x++)
            {
                float nx = x / (float)(hRes - 1);
                float nz = z / (float)(hRes - 1);
                float wx = nx * td.size.x;
                float wz = nz * td.size.z;

                float hy = land.SampleHeight(new Vector3(wx, 0f, wz));
                float distFromCenter = Vector2.Distance(new Vector2(wx, wz), IslandCenter);

                // 陸地（標高 > 5.4m）または島近郊の浅瀬（半径440m以内かつ水深1.5m以内）は表示
                bool isIslandLand = hy >= (WaterY - 1.2f) && distFromCenter <= 480f;
                // 西ビーチの緩やかな砂浜スロープ（X: 130~250, Z: 200~350）を保護
                bool isWestBeach = (wx >= 130f && wx <= 250f && wz >= 200f && wz <= 350f && hy >= 4.0f);

                // 島の陸地・砂浜のみを残し、外側の四角い海底は穴（非表示）に！
                holes[z, x] = (isIslandLand || isWestBeach);
            }
        }

        td.SetHoles(0, 0, holes);
    }
}
