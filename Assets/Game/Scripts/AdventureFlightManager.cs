using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 島内の大滑空スカイライン（風のリング・上昇気流サーマル）を管理・配置するマネージャー
/// 地形高さを正確にサンプリングし、スタート地点正面や北崖にダイナミックに配置
/// </summary>
public class AdventureFlightManager : MonoBehaviour
{
    static AdventureFlightManager _instance;
    public static AdventureFlightManager Instance => _instance;

    public static void Ensure()
    {
        var existingList = FindObjectsByType<AdventureFlightManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ex in existingList)
        {
            if (ex != null && ex.gameObject != null)
                Destroy(ex.gameObject);
        }
        _instance = null;

        var go = new GameObject("AdventureFlightManager");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureFlightManager>();
    }

    void Awake()
    {
        _instance = this;
        BuildAllFlightCourses();
    }

    void BuildAllFlightCourses()
    {
        var land = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        var root = new GameObject("FlightCoursesRoot");
        root.transform.SetParent(transform, false);

        BuildMeadowCourse(root.transform, land);
        BuildNorthCliffHighway(root.transform, land);
        BuildCanyonCourse(root.transform, land);
    }

    /// <summary>コース1: スタート地点正面〜大草原フライトライン（開始直後に正面で一目で体験可能）</summary>
    void BuildMeadowCourse(Transform parent, Terrain land)
    {
        var course = new GameObject("Course_Meadows");
        course.transform.SetParent(parent, false);

        // 1. スタート地点の真正面（北東へ約10m）：開始時に画面中央にドカンと映るチュートリアルリング！
        // プレイヤー(265, 330)の正面55度方向。地上走行でど真ん中を貫通する高さ
        CreateRingAt(course.transform, new Vector3(273f, 0f, 336f), 1.4f, Quaternion.Euler(0f, 55f, 0f), land);

        // 2. そのすぐ先、東の緩やかな丘へ登るリング（ブースト滑空でそのまま突入）
        CreateRingAt(course.transform, new Vector3(292f, 0f, 350f), 2.5f, Quaternion.Euler(0f, 50f, 0f), land);

        // 3. 丘の上から池の対岸へ飛び出すリング
        CreateRingAt(course.transform, new Vector3(310f, 0f, 370f), 3.5f, Quaternion.Euler(0f, 40f, 0f), land);

        // 4. 池上空を滑空するリング
        CreateRingAt(course.transform, new Vector3(285f, 0f, 395f), 5.0f, Quaternion.Euler(0f, -60f, 0f), land);

        // 5. せせらぎ池のほとりの上昇気流サーマル（飛び降りた後、高度を+45m吹き上げる！）
        CreateThermalAt(course.transform, new Vector3(275f, 0f, 370f), 8.5f, 45f, land);
    }

    /// <summary>コース2: 北の大滑空崖 エクストリーム・スカイハイウェイ（標高92m崖から海への大降下＆海上サーマル）</summary>
    void BuildNorthCliffHighway(Transform parent, Terrain land)
    {
        var course = new GameObject("Course_NorthCliff");
        course.transform.SetParent(parent, false);

        // 崖の先端（x=512, z=725, 標高約92m）から海（z=930）へ向かって一直線に降下する6連リング
        float[] zPositions = { 745f, 780f, 815f, 850f, 885f, 920f };
        float[] heights    = { 88f,  74f,  60f,  46f,  32f,  20f };

        for (int i = 0; i < zPositions.Length; i++)
        {
            Vector3 pos = new Vector3(512f, heights[i], zPositions[i]);
            CreateWindRingDirect(course.transform, pos, Quaternion.Euler(14f, 0f, 0f));
        }

        // 海面直前（z=945, 標高6m）の巨大な海上サーマル（高度を+60m吹き上げる！）
        Vector3 thermalPos = new Vector3(512f, 6.0f, 945f);
        CreateThermalDirect(course.transform, thermalPos, 12.0f, 65f);
    }

    /// <summary>コース3: 大渓流〜カルデラ湖 キャニオンコース</summary>
    void BuildCanyonCourse(Transform parent, Terrain land)
    {
        var course = new GameObject("Course_Canyon");
        course.transform.SetParent(parent, false);

        CreateRingAt(course.transform, new Vector3(540f, 0f, 455f), 5.0f, Quaternion.Euler(0f, -90f, 0f), land);
        CreateRingAt(course.transform, new Vector3(490f, 0f, 450f), 5.5f, Quaternion.Euler(0f, -90f, 0f), land);
        CreateRingAt(course.transform, new Vector3(440f, 0f, 445f), 6.0f, Quaternion.Euler(0f, -90f, 0f), land);

        // カルデラ湖畔のサーマル
        CreateThermalAt(course.transform, new Vector3(400f, 0f, 440f), 9.0f, 48f, land);
    }

    void CreateRingAt(Transform parent, Vector3 xzPos, float heightAboveGround, Quaternion rotation, Terrain land)
    {
        float y = heightAboveGround;
        if (land != null)
        {
            y += land.SampleHeight(xzPos) + land.transform.position.y;
        }
        else
        {
            y += 15f;
        }
        Vector3 pos = new Vector3(xzPos.x, y, xzPos.z);
        CreateWindRingDirect(parent, pos, rotation);
    }

    void CreateThermalAt(Transform parent, Vector3 xzPos, float radius, float height, Terrain land)
    {
        float y = 0f;
        if (land != null)
        {
            y = land.SampleHeight(xzPos) + land.transform.position.y;
        }
        else
        {
            y = 15f;
        }
        Vector3 pos = new Vector3(xzPos.x, y, xzPos.z);
        CreateThermalDirect(parent, pos, radius, height);
    }

    void CreateWindRingDirect(Transform parent, Vector3 position, Quaternion rotation)
    {
        var ringGo = new GameObject("WindRing");
        ringGo.transform.SetParent(parent, false);
        ringGo.transform.position = position;
        ringGo.transform.rotation = rotation;
        ringGo.AddComponent<AdventureWindRing>();
    }

    void CreateThermalDirect(Transform parent, Vector3 position, float radius, float height)
    {
        var thermalGo = new GameObject("ThermalUpdraft");
        thermalGo.transform.SetParent(parent, false);
        thermalGo.transform.position = position;
        var updraft = thermalGo.AddComponent<AdventureThermalUpdraft>();
        updraft.radius = radius;
        updraft.height = height;
    }
}
