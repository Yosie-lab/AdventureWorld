using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 『Rust & Float』古代の風脈クリスタル（Ancient Wind Vent）
/// 相棒Rustとの協力ギミック。
/// 未起動のクリスタルに近づいて【E / 左クリック】でRustに送電チャージを指示すると、
/// Rustがビームを照射してクリスタルを覚醒させ、巨大な上昇気流（サーマル）と空中加速リングを起動する。
/// </summary>
public class AdventureAncientWindVent : MonoBehaviour
{
    public string ventId = "Vent_WestCliff_01";
    public string ventName = "古代の風脈クリスタル（西の断崖）";
    public Vector3 ventPosition = new Vector3(210f, 16.5f, 310f); // 西側の美しい岩場テラス
    public bool isActivated = false;
    public bool IsActivated => isActivated;

    // 視覚オブジェクト
    private GameObject _crystalCoreGo;
    private Material _crystalMaterial;
    private GameObject _updraftGo;
    private List<GameObject> _windRings = new List<GameObject>();
    private bool _isCharging = false;

    // UIプロンプト
    private bool _isPlayerNear = false;

    void Start()
    {
        // セーブ状態の復元
        isActivated = PlayerPrefs.GetInt("RustFloat_VentActivated_" + ventId, 0) == 1;

        AlignVentToTerrain();
        BuildStructure();
        BuildFlightCourse();

        if (isActivated)
        {
            SetActivatedVisual(instant: true);
        }
        else
        {
            SetDormantVisual();
        }
    }

    public static bool IsPlayerNearAnyVent(Vector3 playerPos)
    {
        var vent = Object.FindAnyObjectByType<AdventureAncientWindVent>();
        if (vent != null && !vent.isActivated)
        {
            float dist = Vector3.Distance(playerPos, vent.transform.position);
            return dist <= 5.5f;
        }
        return false;
    }

    void Update()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        float dist = Vector3.Distance(player.transform.position, transform.position);
        _isPlayerNear = dist <= 5.2f;

        // 未起動かつプレイヤーが接近中なら、インタラクト（E / F / クリック）を検知
        if (!isActivated && !_isCharging && _isPlayerNear && !AdventureRustWorkshopUI.IsOpen)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            bool fPressed = kb != null && kb.fKey.wasPressedThisFrame;
            bool ePressed = AdventureInputReader.InteractDown || AdventureInputReader.MouseLeftDown;

            if (fPressed || ePressed)
            {
                TriggerRustCharge();
            }
        }

        // デバッグ・テスト用ショートカット
        CheckDebugShortcuts(player);
    }

    void CheckDebugShortcuts(AdventurePlayerController player)
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;

        // 【F6】キー: 風脈クリスタルの前へ即時ワープ
        if (kb.f6Key.wasPressedThisFrame)
        {
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (shift)
            {
                // Shift+F6: ギミック状態をリセット（未起動に戻す）
                ResetGimmickState();
                Debug.Log("[AdventureAncientWindVent] 風脈ギミックを未起動状態にリセットしました");
            }
            else
            {
                // F6: 風脈クリスタルの手前へ安全にワープ
                Vector3 warpTarget = ventPosition + new Vector3(0f, 0f, -3.8f);
                var land = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
                float groundY = land != null ? (land.SampleHeight(warpTarget) + land.transform.position.y) : ventPosition.y;
                Vector3 warpPos = new Vector3(warpTarget.x, groundY + 0.65f, warpTarget.z);

                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = warpPos;
                player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                if (cc != null) cc.enabled = true;

                // Rustも近くへ引き寄せる
                var drone = AdventureRustDrone.Instance;
                if (drone != null)
                {
                    drone.transform.position = warpPos + new Vector3(1.2f, 1.6f, -0.5f);
                }

                Debug.Log($"[AdventureAncientWindVent] テスト用ワープ実行: {warpPos}");
            }
        }
    }

    /// <summary>テスト検証用にギミック状態を初期化</summary>
    public void ResetGimmickState()
    {
        isActivated = false;
        _isCharging = false;
        PlayerPrefs.DeleteKey("RustFloat_VentActivated_" + ventId);
        PlayerPrefs.Save();
        SetDormantVisual();
    }

    void OnGUI()
    {
        if (AdventureStoryFlow.HideWorldInteractPrompts) return;
        if (!isActivated && !_isCharging && _isPlayerNear)
        {
            // プロンプトUI
            DrawInteractionPrompt();
        }
    }

    void DrawInteractionPrompt()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null || Camera.main == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.2f);
        if (screenPos.z < 0) return;

        float w = 320f;
        float h = 48f;
        float x = screenPos.x - w * 0.5f;
        float y = Screen.height - screenPos.y - h * 0.5f;

        var boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = Texture2D.whiteTexture;

        var prevColor = GUI.color;
        GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.88f);
        GUI.Box(new Rect(x, y, w, h), GUIContent.none, boxStyle);

        // 外枠ライン
        GUI.color = new Color(0.35f, 0.95f, 0.85f, 0.95f);
        GUI.Box(new Rect(x, y, w, 2.5f), GUIContent.none, boxStyle);

        // テキスト
        var labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 14;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.normal.textColor = Color.white;

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y + 2f, w, h - 4f), "🐾 【E / F / クリック】Rustに送電チャージを指示", labelStyle);
        GUI.color = prevColor;
    }

    public void TriggerRustCharge()
    {
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone == null || drone.IsPerformingGimmick) return;

        _isCharging = true;

        // Rustに送電チャージ指示を発行
        Vector3 targetPos = _crystalCoreGo != null ? _crystalCoreGo.transform.position : transform.position + Vector3.up * 1.5f;

        drone.CommandChargeCrystal(targetPos,
            onProgress: () =>
            {
                // ビーム照射中のクリスタル発光脈動
                if (_crystalMaterial != null)
                {
                    float glow = 1.0f + Mathf.PingPong(Time.time * 6f, 1.8f);
                    _crystalMaterial.SetColor("_EmissionColor", new Color(0.3f, 0.9f, 1.0f) * glow);
                }
            },
            onComplete: () =>
            {
                OnChargeComplete();
            }
        );
    }

    void OnChargeComplete()
    {
        _isCharging = false;
        ActivateVentAndRings(persist: true, instant: false);
    }

    /// <summary>断崖麓クリスタル等から連動起動（空中リングコースを必ず出す）</summary>
    public void ActivateFromLinkedGimmick()
    {
        ActivateVentAndRings(persist: true, instant: true);
    }

    void ActivateVentAndRings(bool persist, bool instant)
    {
        isActivated = true;
        if (persist)
        {
            PlayerPrefs.SetInt("RustFloat_VentActivated_" + ventId, 1);
            PlayerPrefs.Save();
        }

        SetActivatedVisual(instant);
    }

    void AlignVentToTerrain()
    {
        var land = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        if (land == null) return;

        float groundY = land.SampleHeight(ventPosition) + land.transform.position.y;
        ventPosition.y = groundY + 0.35f;
    }

    void SetDormantVisual()
    {
        if (_crystalMaterial != null)
        {
            _crystalMaterial.SetColor("_BaseColor", new Color(0.15f, 0.40f, 0.55f, 0.8f));
            _crystalMaterial.SetColor("_EmissionColor", new Color(0.1f, 0.3f, 0.5f) * 0.5f);
        }

        if (_updraftGo != null) _updraftGo.SetActive(false);
        foreach (var ring in _windRings)
        {
            if (ring != null) ring.SetActive(false);
        }
    }

    void SetActivatedVisual(bool instant)
    {
        EnsureFlightCourseExists();

        if (_crystalMaterial != null)
        {
            _crystalMaterial.SetColor("_BaseColor", new Color(0.35f, 0.98f, 0.85f, 1.0f));
            _crystalMaterial.SetColor("_EmissionColor", new Color(0.4f, 1.8f, 1.2f) * 2.5f);
        }

        // 上昇気流（サーマル）を起動
        if (_updraftGo != null)
        {
            _updraftGo.SetActive(true);
        }

        // 空中の風リングフライトコースを起動
        foreach (var ring in _windRings)
        {
            if (ring == null) continue;
            ring.SetActive(true);
            ring.GetComponent<AdventureGlidingWindRing>()?.EnsureVisualBuilt();
        }
    }

    void EnsureFlightCourseExists()
    {
        _windRings.RemoveAll(r => r == null);
        if (_windRings.Count >= 3) return;
        BuildFlightCourse();
    }

    void BuildStructure()
    {
        transform.position = ventPosition;

        // 1. 円形石造り台座（地形に沿わせ、物理コリジョン引っかかりを防ぐためコライダーは削除）
        var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseGo.name = "VentStonePedestal";
        baseGo.transform.SetParent(transform, false);
        baseGo.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        baseGo.transform.localScale = new Vector3(4.5f, 0.25f, 4.5f);
        var baseCol = baseGo.GetComponent<Collider>();
        if (baseCol != null) Destroy(baseCol);

        var baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        baseMat.color = new Color(0.22f, 0.26f, 0.32f);
        baseGo.GetComponent<Renderer>().sharedMaterial = baseMat;

        // 2. 中央の古代エネルギークリスタル柱
        _crystalCoreGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _crystalCoreGo.name = "AncientEnergyCrystal";
        _crystalCoreGo.transform.SetParent(transform, false);
        _crystalCoreGo.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        _crystalCoreGo.transform.localScale = new Vector3(0.75f, 1.1f, 0.75f);

        // クリスタルマテリアル
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Unlit/Color");
        _crystalMaterial = new Material(shader);
        _crystalMaterial.EnableKeyword("_EMISSION");
        _crystalCoreGo.GetComponent<Renderer>().sharedMaterial = _crystalMaterial;

        // コライダー（プレイヤーが引っかかってスタックしないようトリガー化）
        var col = _crystalCoreGo.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // 3. 通風孔から立ち上る上昇気流（AdventureThermalUpdraft）
        _updraftGo = new GameObject("ThermalUpdraft");
        _updraftGo.transform.SetParent(transform, false);
        _updraftGo.transform.localPosition = Vector3.zero;

        var updraft = _updraftGo.AddComponent<AdventureThermalUpdraft>();
        updraft.radius = 6.5f;
        updraft.height = 54.0f;
        updraft.liftSpeed = 17.5f;
        updraft.autoLaunch = false;
        updraft.pullToCenter = false; // 地上での引っ張られスタックを防止
        updraft.liftWhenAirborneInColumn = true;
    }

    void BuildFlightCourse()
    {
        for (int i = 0; i < _windRings.Count; i++)
        {
            if (_windRings[i] != null)
                Destroy(_windRings[i]);
        }
        _windRings.Clear();

        // 上昇気流（高度16.5m〜55m）から高台テラスへと続く爽快な空中リングコース（3連リング）
        Vector3 ringPos1 = ventPosition + new Vector3(0f, 32f, 18f);
        Vector3 ringPos2 = ventPosition + new Vector3(-12f, 38f, 42f);
        Vector3 ringPos3 = ventPosition + new Vector3(-24f, 44f, 68f);

        _windRings.Add(CreateWindRing("Ring1", ringPos1, (ringPos2 - ringPos1).normalized));
        _windRings.Add(CreateWindRing("Ring2", ringPos2, (ringPos3 - ringPos2).normalized));
        _windRings.Add(CreateWindRing("Ring3", ringPos3, Vector3.forward));
    }

    GameObject CreateWindRing(string name, Vector3 worldPos, Vector3 forwardDir)
    {
        var ringGo = new GameObject(name);
        ringGo.transform.SetParent(transform, true);
        ringGo.transform.position = worldPos;
        if (forwardDir.sqrMagnitude > 0.001f)
            ringGo.transform.rotation = Quaternion.LookRotation(forwardDir);

        var ringComp = ringGo.AddComponent<AdventureGlidingWindRing>();
        ringComp.ringRadius = 2.35f;
        ringComp.boostMultiplier = 1.50f;
        ringComp.boostDuration = 4.0f;
        ringComp.magnetRadiusMultiplier = 5.2f;
        ringComp.magnetAxialHalfLength = 18f;
        ringComp.magnetPullSpeed = 30f;

        return ringGo;
    }
}
