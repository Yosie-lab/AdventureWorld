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
        var existingList = FindObjectsByType<AdventureFlightManager>(FindObjectsInactive.Include);
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

        BuildBeachCourse(root.transform, land);
        BuildMeadowCourse(root.transform, land);
        BuildNorthCliffHighway(root.transform, land);
        BuildCanyonCourse(root.transform, land);
        BuildTowerOrbitCourse(root.transform, land);
        BuildCalderaLakeCourse(root.transform, land);
        BuildMountainGorgeSlalomCourse(root.transform, land);
        BuildSouthCoastCourse(root.transform, land);
    }

    /// <summary>西海岸コース: メガジャンプ（高度78m）からの内陸向け高空3連リング ＋ 海岸散策ライン</summary>
    void BuildBeachCourse(Transform parent, Terrain land)
    {
        var course = new GameObject("Course_Beach");
        course.transform.SetParent(parent, false);

        // ★西海岸70mメガジャンプ（X:172, Z:265, 頂点高度78m）から内陸断崖へ向かう高空3連リング
        CreateWindRingDirect(course.transform, new Vector3(198f, 74.0f, 281f), Quaternion.Euler(6f, 58f, 0f));
        CreateWindRingDirect(course.transform, new Vector3(230f, 67.5f, 301f), Quaternion.Euler(7f, 58f, 0f));
        CreateWindRingDirect(course.transform, new Vector3(264f, 60.5f, 322f), Quaternion.Euler(8f, 58f, 0f));

        // 西砂浜中央〜木道テラス手前（地上低空リング）
        Vector3 terraceRing = new Vector3(145f, 0f, 345f);
        CreateRingAt(course.transform, terraceRing, 3.5f, Quaternion.Euler(0f, -5f, 0f), land);

        // 南岬の海風ライン
        Vector3 capeRing = new Vector3(195f, 0f, 205f);
        CreateRingAt(course.transform, capeRing, 3.5f, Quaternion.Euler(0f, 25f, 0f), land);
    }

    /// <summary>コース1: スタート地点正面〜大草原フライトライン（開始直後に正面で一目で体験可能）</summary>
    void BuildMeadowCourse(Transform parent, Terrain land)
    {
        var course = new GameObject("Course_Meadows");
        course.transform.SetParent(parent, false);

        // 1. 東の小高い丘から池へ向けて飛び出す空中リング（滑空でくぐる高さ）
        CreateRingAt(course.transform, new Vector3(292f, 0f, 350f), 4.8f, Quaternion.Euler(0f, 50f, 0f), land);

        // 2. 丘の上から池の対岸へ滑空するリング
        CreateRingAt(course.transform, new Vector3(310f, 0f, 370f), 5.5f, Quaternion.Euler(0f, 40f, 0f), land);

        // 3. 池上空を優雅に横断する空中リング
        CreateRingAt(course.transform, new Vector3(285f, 0f, 395f), 6.5f, Quaternion.Euler(0f, -60f, 0f), land);

        // 4. せせらぎ池上空の上昇気流サーマル（滑空中に飛び込むと高度を吹き上げる）
        CreateThermalAt(course.transform, new Vector3(275f, 0f, 370f), 8.5f, 45f, land);
    }

    /// <summary>コース2: 北の大滑空崖（北海への大降下コース ＋ ★メガジャンプ南下高空3連リング）</summary>
    void BuildNorthCliffHighway(Transform parent, Terrain land)
    {
        var course = new GameObject("Course_NorthCliff");
        course.transform.SetParent(parent, false);

        // ★北の最高峰70mメガジャンプ（X:512, Z:660, 頂点高度162m）から南下・全島縦断の高空3連リング
        CreateWindRingDirect(course.transform, new Vector3(512f, 155.0f, 620f), Quaternion.Euler(7f, 180f, 0f));
        CreateWindRingDirect(course.transform, new Vector3(512f, 141.0f, 570f), Quaternion.Euler(8f, 180f, 0f));
        CreateWindRingDirect(course.transform, new Vector3(512f, 126.0f, 520f), Quaternion.Euler(9f, 180f, 0f));

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

    /// <summary>コース4: 中央聖域タワー（★タワー麓70mメガジャンプ南西ダイブ高空3連リング ＋ 周遊ライン）</summary>
    void BuildTowerOrbitCourse(Transform parent, Terrain land)
    {
        var course = new GameObject("Course_SanctuaryTower");
        course.transform.SetParent(parent, false);

        // ★中央タワー麓70mメガジャンプ（X:480, Z:485, 頂点高度137m）から大カルデラ湖へダイブする高空3連リング
        CreateWindRingDirect(course.transform, new Vector3(454f, 131.0f, 459f), Quaternion.Euler(7f, -135f, 0f));
        CreateWindRingDirect(course.transform, new Vector3(418f, 117.5f, 423f), Quaternion.Euler(8f, -135f, 0f));
        CreateWindRingDirect(course.transform, new Vector3(378f, 101.5f, 383f), Quaternion.Euler(9f, -135f, 0f));

        // タワー周遊オービット（タワー中腹〜上空を旋回する4連リング）
        CreateWindRingDirect(course.transform, new Vector3(475f, 78f, 535f), Quaternion.Euler(0f, 65f, 0f));
        CreateWindRingDirect(course.transform, new Vector3(535f, 76f, 545f), Quaternion.Euler(0f, 155f, 0f));
        CreateWindRingDirect(course.transform, new Vector3(545f, 74f, 480f), Quaternion.Euler(0f, 245f, 0f));
        CreateWindRingDirect(course.transform, new Vector3(480f, 72f, 475f), Quaternion.Euler(0f, 335f, 0f));

        // タワー台座〜西側大草原への高速ダイビングライン
        CreateWindRingDirect(course.transform, new Vector3(470f, 66f, 485f), Quaternion.Euler(12f, -135f, 0f));
        CreateWindRingDirect(course.transform, new Vector3(430f, 52f, 445f), Quaternion.Euler(14f, -135f, 0f));
        CreateWindRingDirect(course.transform, new Vector3(390f, 38f, 410f), Quaternion.Euler(10f, -135f, 0f));
        CreateWindRingDirect(course.transform, new Vector3(350f, 28f, 375f), Quaternion.Euler(5f, -135f, 0f));

        // タワー南西の上昇気流サーマル（台座付近から一気に上空へ舞い上がる）
        CreateThermalDirect(course.transform, new Vector3(490f, 62.5f, 490f), 12.0f, 55f);
    }

    /// <summary>コース5: 大カルデラ湖〜オアシス横断 スカイクルーズ</summary>
    void BuildCalderaLakeCourse(Transform parent, Terrain land)
    {
        var course = new GameObject("Course_CalderaLake");
        course.transform.SetParent(parent, false);

        // 湖上を渡る低空〜中空の優雅なリングライン
        CreateRingAt(course.transform, new Vector3(425f, 0f, 480f), 7.5f, Quaternion.Euler(8f, -150f, 0f), land);
        CreateRingAt(course.transform, new Vector3(410f, 0f, 445f), 6.5f, Quaternion.Euler(2f, -165f, 0f), land);
        CreateRingAt(course.transform, new Vector3(395f, 0f, 410f), 6.0f, Quaternion.Euler(0f, -180f, 0f), land);

        // 湖心のサーマル（水面から一気に上昇）
        CreateThermalAt(course.transform, new Vector3(405f, 0f, 430f), 10.0f, 48f, land);
    }

    /// <summary>コース6: 東部山岳渓谷 スラロームコース</summary>
    void BuildMountainGorgeSlalomCourse(Transform parent, Terrain land)
    {
        var course = new GameObject("Course_MountainGorgeSlalom");
        course.transform.SetParent(parent, false);

        // 高山から渓流の谷間を縫って一気に下るスラローム
        CreateRingAt(course.transform, new Vector3(705f, 0f, 565f), 8.0f, Quaternion.Euler(10f, -120f, 0f), land);
        CreateRingAt(course.transform, new Vector3(660f, 0f, 535f), 7.5f, Quaternion.Euler(12f, -140f, 0f), land);
        CreateRingAt(course.transform, new Vector3(615f, 0f, 500f), 7.0f, Quaternion.Euler(8f, -150f, 0f), land);
        CreateRingAt(course.transform, new Vector3(575f, 0f, 470f), 6.5f, Quaternion.Euler(5f, -160f, 0f), land);

        // 渓流出口の大滝サーマル
        CreateThermalAt(course.transform, new Vector3(565f, 0f, 460f), 11.0f, 50f, land);
    }

    /// <summary>コース7: 南海岸オーシャンパノラマコース</summary>
    void BuildSouthCoastCourse(Transform parent, Terrain land)
    {
        var course = new GameObject("Course_SouthCoast");
        course.transform.SetParent(parent, false);

        // 南海岸線と岬を巡るオーシャンビューライン
        CreateRingAt(course.transform, new Vector3(180f, 0f, 185f), 5.5f, Quaternion.Euler(0f, 80f, 0f), land);
        CreateRingAt(course.transform, new Vector3(235f, 0f, 190f), 6.0f, Quaternion.Euler(0f, 85f, 0f), land);
        CreateRingAt(course.transform, new Vector3(290f, 0f, 195f), 6.5f, Quaternion.Euler(0f, 90f, 0f), land);
        CreateRingAt(course.transform, new Vector3(345f, 0f, 205f), 7.0f, Quaternion.Euler(0f, 85f, 0f), land);

        // 南海岸の海風サーマル
        CreateThermalAt(course.transform, new Vector3(260f, 0f, 180f), 12.0f, 55f, land);
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
