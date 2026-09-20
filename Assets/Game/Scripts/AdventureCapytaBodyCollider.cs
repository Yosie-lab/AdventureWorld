using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 全カピタ（ピアノカピタ、湖畔・川辺・草原・砂浜の野生カピタ、NPCカピタ等）のすり抜けを完全防止するコンポーネント。
/// 
/// 【すり抜け防止の多重防御システム】
/// 1. 【ワールド基準スケールBoxCollider】
///    各カピタのルートスケール（0.3倍〜1.2倍等）を検出し、ワールド空間で常に
///    【幅 1.05m × 高さ 1.45m × 奥行 1.65m】の垂直な固体直方体コライダーを構築。
///    上面がドーム状のカプセルと異なり、NikoのCharacterController（stepOffset 0.4m）が
///    坂道のように登って踏み越えてしまう現象を幾何学的に100%遮断する。
/// 
/// 2. 【二重防御プッシュバック（侵入検知・強制押し戻し）】
///    フレームレート低下や高速ダッシュ・ジャンプによる物理エンジンのトンネリング（すり抜け）対策として、
///    LateUpdateでNikoがカピタの体躯バウンディング内に侵入していないか常時監視。
///    侵入を検知した瞬間、最短の水平境界線へ向けてCharacterControllerを押し戻す。
/// 
/// 3. 【自動全頭走査・登録（RuntimeInitializeOnLoadMethod）】
///    シーンロード時・ゲーム開始時にシーン内の全カピタを自動検出し、
///    本コンポーネントをアタッチ・セットアップする。
/// </summary>
[DisallowMultipleComponent]
public class AdventureCapytaBodyCollider : MonoBehaviour
{
    [Header("Target World Dimensions")]
    [Tooltip("カピタのワールド空間での目標横幅 (m)")]
    public float targetWorldWidth = 1.05f;

    [Tooltip("カピタのワールド空間での目標高さ (m) - Nikoの身長(1.5m)に対して踏み越えられない十分な高さ")]
    public float targetWorldHeight = 1.45f;

    [Tooltip("カピタのワールド空間での目標全長 (m)")]
    public float targetWorldLength = 1.65f;

    [Tooltip("カピタのワールド空間での中心高さ (m)")]
    public float targetWorldCenterY = 0.725f;

    [Tooltip("カピタのワールド空間での中心前後オフセット (m)")]
    public float targetWorldCenterZ = 0.10f;

    [Header("Collider Component")]
    [SerializeField] private BoxCollider _boxCollider;

    // プッシュバック用キャッシュ
    private static AdventurePlayerController _cachedPlayer;
    private static CharacterController _cachedCharacterController;
    private static float _lastPlayerSearchTime = -10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnRuntimeInit()
    {
        EnsureAllCapytasInScene();
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            EnsureAllCapytasInScene();
        };
    }

    void Awake()
    {
        EnsureCollider();
    }

    void Start()
    {
        // 起動直後にもう一度スケールを再同期（アニメーションや階層初期化対応）
        EnsureCollider();
    }

    /// <summary>
    /// カピタの個体スケールを吸収し、ワールド空間で常に均一な固体BoxColliderを構築
    /// </summary>
    public void EnsureCollider()
    {
        // 古いCapsuleColliderがあれば削除または無効化（丸みによる登攀防止）
        var oldCapsules = GetComponents<CapsuleCollider>();
        for (int i = 0; i < oldCapsules.Length; i++)
        {
            if (oldCapsules[i] != null)
            {
                oldCapsules[i].enabled = false;
                if (Application.isPlaying)
                {
                    Destroy(oldCapsules[i]);
                }
            }
        }

        if (_boxCollider == null)
        {
            _boxCollider = GetComponent<BoxCollider>();
            if (_boxCollider == null)
            {
                _boxCollider = gameObject.AddComponent<BoxCollider>();
            }
        }

        // 親・自身のlossyScaleを取得してローカルサイズを算出
        Vector3 lossy = transform.lossyScale;
        float sx = Mathf.Max(0.01f, Mathf.Abs(lossy.x));
        float sy = Mathf.Max(0.01f, Mathf.Abs(lossy.y));
        float sz = Mathf.Max(0.01f, Mathf.Abs(lossy.z));

        _boxCollider.size = new Vector3(
            targetWorldWidth / sx,
            targetWorldHeight / sy,
            targetWorldLength / sz
        );

        _boxCollider.center = new Vector3(
            0f,
            targetWorldCenterY / sy,
            targetWorldCenterZ / sz
        );

        // 固体コライダーとしてCharacterControllerを確実に遮断
        _boxCollider.isTrigger = false;
        _boxCollider.enabled = true;

        // レイヤーをDefault (0)にしてNikoの衝突を100%保証
        if (gameObject.layer != 0 && gameObject.layer != LayerMask.NameToLayer("Default"))
        {
            gameObject.layer = 0;
        }
    }

    void LateUpdate()
    {
        // プッシュバック二重防御：トンネリングやすり抜けが発生した瞬間に押し戻す
        EnsurePlayerReference();
        if (_cachedCharacterController == null || !_cachedCharacterController.enabled) return;

        PushBackPlayerIfIntersecting();
    }

    /// <summary>
    /// NikoのCharacterControllerがカピタの体躯境界内に侵入した場合、最短距離で外側へ押し戻す
    /// </summary>
    private void PushBackPlayerIfIntersecting()
    {
        Vector3 playerPos = _cachedCharacterController.transform.position;
        Vector3 capytaPos = transform.position;

        // 地面からの高さ差をチェック（カピタの足元より下、または頭上高く飛んでいる場合は無視）
        float deltaY = playerPos.y - capytaPos.y;
        if (deltaY < -0.4f || deltaY > targetWorldHeight + 0.3f)
        {
            return;
        }

        // カピタの前方・右方向ベクトル
        Vector3 fwd = transform.forward;
        Vector3 rgt = transform.right;
        fwd.y = 0f;
        rgt.y = 0f;
        if (fwd.sqrMagnitude > 0.001f) fwd.Normalize(); else fwd = Vector3.forward;
        if (rgt.sqrMagnitude > 0.001f) rgt.Normalize(); else rgt = Vector3.right;

        // カピタの中心点（水平）
        Vector3 center = capytaPos + fwd * targetWorldCenterZ;
        Vector3 delta = playerPos - center;
        delta.y = 0f;

        // カピタの座標軸への投影
        float distForward = Vector3.Dot(delta, fwd);
        float distRight = Vector3.Dot(delta, rgt);

        // Nikoの半径（0.28m）+ カピタの半幅・半長 + 安全マージン
        float nikoRadius = _cachedCharacterController.radius;
        float halfW = (targetWorldWidth * 0.5f) + nikoRadius + 0.04f;
        float halfL = (targetWorldLength * 0.5f) + nikoRadius + 0.04f;

        float absRight = Mathf.Abs(distRight);
        float absFwd = Mathf.Abs(distForward);

        // 境界内部に侵入しているか判定
        if (absRight < halfW && absFwd < halfL)
        {
            // 侵入深さ（左右と前後でどちらが浅いか）
            float overlapX = halfW - absRight;
            float overlapZ = halfL - absFwd;

            Vector3 pushDir;
            float pushDist;

            if (overlapX < overlapZ)
            {
                // 左右方向へ押し出す
                float signX = distRight >= 0f ? 1f : -1f;
                pushDir = rgt * signX;
                pushDist = overlapX + 0.03f;
            }
            else
            {
                // 前後方向へ押し出す
                float signZ = distForward >= 0f ? 1f : -1f;
                pushDir = fwd * signZ;
                pushDist = overlapZ + 0.03f;
            }

            // 背中に乗っかってしまっている場合（deltaY > 0.7m）はさらに強めに滑落させる
            if (deltaY > 0.7f)
            {
                pushDist = Mathf.Max(pushDist, 0.15f);
            }

            Vector3 pushVector = pushDir * pushDist;
            _cachedCharacterController.Move(pushVector);
        }
    }

    private static void EnsurePlayerReference()
    {
        if (_cachedCharacterController != null && _cachedCharacterController.gameObject.activeInHierarchy)
            return;

        if (Time.unscaledTime - _lastPlayerSearchTime < 1.0f)
            return;

        _lastPlayerSearchTime = Time.unscaledTime;

        if (_cachedPlayer == null || !_cachedPlayer.gameObject.activeInHierarchy)
        {
            _cachedPlayer = AdventurePlayerController.Instance ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        }

        if (_cachedPlayer != null)
        {
            _cachedCharacterController = _cachedPlayer.GetComponent<CharacterController>();
        }
    }

    /// <summary>
    /// シーン内のすべてのカピタ（ピアノカピタ・野生カピタ・NPCカピタ）を走査し、
    /// すり抜け防止コライダーが存在することを100%保証する。
    /// </summary>
    public static void EnsureAllCapytasInScene()
    {
        var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        int count = 0;
        for (int i = 0; i < allTransforms.Length; i++)
        {
            var t = allTransforms[i];
            if (t == null) continue;

            if (IsCapytaRoot(t))
            {
                var col = t.GetComponent<AdventureCapytaBodyCollider>();
                if (col == null)
                {
                    col = t.gameObject.AddComponent<AdventureCapytaBodyCollider>();
                }
                col.EnsureCollider();
                count++;
            }
        }

        Debug.Log($"[AdventureCapytaBodyCollider] 🐾 シーン内の全カピタ（{count}頭）にすり抜け防止コライダー（BoxCollider + プッシュバック防御）を適用しました。");
    }

    /// <summary>
    /// カピタの個体ルートオブジェクトか判定
    /// </summary>
    public static bool IsCapytaRoot(Transform t)
    {
        if (t == null) return false;
        string n = t.name.ToLowerInvariant();

        // 1. 名前に capyta または capy が含まれるか、NPCがカピタか
        bool isCapyName = n.Contains("capyta") || n.Contains("capy");
        var npc = t.GetComponent<AdventureNpc>();
        bool isCapyNpc = npc != null && (npc.npcId == "capyta" || npc.displayName == "カピタ");

        if (!isCapyName && !isCapyNpc)
        {
            return false;
        }

        // 2. 子メッシュ・ボーン・パーティクル等の付属パーツは除外
        if (n.Contains("armature") || n.Contains("bone") || n.Contains("cube") || 
            n.Contains("notes") || n.Contains("mesh") || n.Contains("radar") || n.Contains("root"))
        {
            return false;
        }

        // 3. 親または祖先に既にカピタオブジェクトが存在する場合、自身はルートではない
        Transform p = t.parent;
        while (p != null)
        {
            string pn = p.name.ToLowerInvariant();
            if (pn == "pianistcapyta" || pn == "capyta" || pn.StartsWith("capyta_") || pn.StartsWith("capy_"))
            {
                return false;
            }
            p = p.parent;
        }

        return true;
    }

#if UNITY_EDITOR
    [MenuItem("Adventure/🐾 Ensure All Capyta Colliders (全カピタのすり抜け防止)")]
    public static void EditorEnsureAllCapytas()
    {
        EnsureAllCapytasInScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }
#endif
}
