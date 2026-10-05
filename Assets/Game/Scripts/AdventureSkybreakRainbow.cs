using System.Collections;
using UnityEngine;

/// <summary>
/// エピローグ「箱庭の外には」のシーンで大空の正面に架かる雄大で美しい大虹（主虹＋副虹のダブルレインボー）。
/// プロシージャルな大円弧メッシュと7色スペクトルグラデーションにより、
/// 青空と雲の彼方に神々しくフェードインする。
/// </summary>
public class AdventureSkybreakRainbow : MonoBehaviour
{
    static AdventureSkybreakRainbow _instance;

    [Header("Rainbow Mesh Settings")]
    const int ArcSegments = 84;
    const int WidthSlices = 8;
    const float ArcStartDeg = 14f;
    const float ArcEndDeg = 166f;

    // 主虹（Primary Rainbow）
    const float PrimaryInnerRadius = 220f;
    const float PrimaryOuterRadius = 268f;
    const float PrimaryBaseAlpha = 0.92f;

    // 副虹（Secondary Rainbow）
    const float SecondaryInnerRadius = 295f;
    const float SecondaryOuterRadius = 332f;
    const float SecondaryBaseAlpha = 0.38f;

    Material _primaryMat;
    Material _secondaryMat;
    Texture2D _primaryTex;
    Texture2D _secondaryTex;
    GameObject _primaryGo;
    GameObject _secondaryGo;

    float _currentAlpha = 0f;
    float _targetAlpha = 1f;
    float _fadeDuration = 3.2f;
    float _fadeTimer = 0f;
    bool _isFading = false;

    /// <summary>
    /// エピローグの指定位置・向き正面に大虹を生成してフェードイン開始
    /// </summary>
    public static AdventureSkybreakRainbow Spawn(Vector3 playerPos, Vector3 forwardDir)
    {
        if (_instance != null && _instance.gameObject != null)
        {
            _instance.Reposition(playerPos, forwardDir);
            _instance.StartFadeIn(3.0f);
            return _instance;
        }

        var existing = GameObject.Find("EpilogueGrandRainbow");
        if (existing != null)
            Destroy(existing);

        var root = new GameObject("EpilogueGrandRainbow");
        _instance = root.AddComponent<AdventureSkybreakRainbow>();
        _instance.BuildRainbow(playerPos, forwardDir);
        _instance.StartFadeIn(3.2f);
        return _instance;
    }

    /// <summary>
    /// 虹が存在していれば破棄
    /// </summary>
    public static void DestroyRainbow()
    {
        if (_instance != null && _instance.gameObject != null)
        {
            Destroy(_instance.gameObject);
            _instance = null;
        }
        var existing = GameObject.Find("EpilogueGrandRainbow");
        if (existing != null)
            Destroy(existing);
    }

    void BuildRainbow(Vector3 playerPos, Vector3 forwardDir)
    {
        Reposition(playerPos, forwardDir);

        Shader unlitSh = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Transparent");

        // 1. 主虹（Primary Rainbow）の生成
        _primaryTex = GenerateSpectrumTexture(isSecondary: false);
        _primaryMat = new Material(unlitSh);
        _primaryMat.mainTexture = _primaryTex;
        _primaryMat.color = new Color(1f, 1f, 1f, 0f);

        _primaryGo = new GameObject("PrimaryRainbowArch");
        _primaryGo.transform.SetParent(transform, false);
        _primaryGo.transform.localPosition = Vector3.zero;
        _primaryGo.transform.localRotation = Quaternion.identity;
        var mfPrimary = _primaryGo.AddComponent<MeshFilter>();
        mfPrimary.sharedMesh = CreateArchMesh(PrimaryInnerRadius, PrimaryOuterRadius);
        var mrPrimary = _primaryGo.AddComponent<MeshRenderer>();
        mrPrimary.sharedMaterial = _primaryMat;
        mrPrimary.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrPrimary.receiveShadows = false;

        // 2. 副虹（Secondary Rainbow）の生成（外側に淡く色の順序が逆転して架かる）
        _secondaryTex = GenerateSpectrumTexture(isSecondary: true);
        _secondaryMat = new Material(unlitSh);
        _secondaryMat.mainTexture = _secondaryTex;
        _secondaryMat.color = new Color(1f, 1f, 1f, 0f);

        _secondaryGo = new GameObject("SecondaryRainbowArch");
        _secondaryGo.transform.SetParent(transform, false);
        _secondaryGo.transform.localPosition = Vector3.zero;
        _secondaryGo.transform.localRotation = Quaternion.identity;
        var mfSecondary = _secondaryGo.AddComponent<MeshFilter>();
        mfSecondary.sharedMesh = CreateArchMesh(SecondaryInnerRadius, SecondaryOuterRadius);
        var mrSecondary = _secondaryGo.AddComponent<MeshRenderer>();
        mrSecondary.sharedMaterial = _secondaryMat;
        mrSecondary.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrSecondary.receiveShadows = false;
    }

    void Reposition(Vector3 playerPos, Vector3 forwardDir)
    {
        Vector3 forwardFlat = Vector3.ProjectOnPlane(forwardDir, Vector3.up);
        if (forwardFlat.sqrMagnitude < 0.001f)
            forwardFlat = Vector3.forward;
        forwardFlat.Normalize();

        // プレイヤーの視界正面290m先、地平線近く（Y=15m）を着地点として配置
        Vector3 center = playerPos + forwardFlat * 290f;
        center.y = 15f;
        transform.position = center;

        // アーチの平面がカメラ正面に対して垂直に美しくそびえ立つよう回転
        transform.rotation = Quaternion.LookRotation(forwardFlat, Vector3.up);
    }

    public void StartFadeIn(float duration)
    {
        _fadeDuration = Mathf.Max(0.5f, duration);
        _fadeTimer = 0f;
        _currentAlpha = 0f;
        _targetAlpha = 1f;
        _isFading = true;
    }

    void Update()
    {
        if (_isFading)
        {
            _fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_fadeTimer / _fadeDuration);
            _currentAlpha = Mathf.SmoothStep(0f, _targetAlpha, t);
            if (t >= 1f)
                _isFading = false;
        }

        // 太陽光と大気の反射による微細な光のきらめき・呼吸感（ブリージング）
        float shimmer = 1f + Mathf.Sin(Time.time * 1.8f) * 0.04f + Mathf.Cos(Time.time * 3.1f) * 0.02f;

        if (_primaryMat != null)
        {
            float a = _currentAlpha * PrimaryBaseAlpha * shimmer;
            _primaryMat.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }

        if (_secondaryMat != null)
        {
            float a = _currentAlpha * SecondaryBaseAlpha * shimmer;
            _secondaryMat.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }
    }

    void OnDestroy()
    {
        if (_primaryMat != null) Destroy(_primaryMat);
        if (_secondaryMat != null) Destroy(_secondaryMat);
        if (_primaryTex != null) Destroy(_primaryTex);
        if (_secondaryTex != null) Destroy(_secondaryTex);
    }

    /// <summary>
    /// 半円アーチ状のプロシージャルメッシュを生成
    /// </summary>
    Mesh CreateArchMesh(float innerRadius, float outerRadius)
    {
        var mesh = new Mesh();
        mesh.name = "RainbowArchMesh";

        int vertCount = (ArcSegments + 1) * (WidthSlices + 1);
        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Color[] colors = new Color[vertCount];

        int idx = 0;
        for (int s = 0; s <= ArcSegments; s++)
        {
            float tArc = (float)s / ArcSegments;
            float angleDeg = Mathf.Lerp(ArcStartDeg, ArcEndDeg, tArc);
            float rad = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            // 地平線両端での自然な大気フェードアウト（接地部が消える）
            float horizonFade = Mathf.Sin(tArc * Mathf.PI);
            float groundAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(horizonFade * 2.2f));

            for (int w = 0; w <= WidthSlices; w++)
            {
                float tWidth = (float)w / WidthSlices;
                float r = Mathf.Lerp(innerRadius, outerRadius, tWidth);

                // localX = 左右, localY = 高さ, localZ = 0
                vertices[idx] = new Vector3(cos * r, sin * r, 0f);
                uvs[idx] = new Vector2(tArc, tWidth);

                // 内外エッジの滑らかなフェード（端で0、帯中央で1）
                float edgeFade = Mathf.Sin(tWidth * Mathf.PI);
                float widthAlpha = Mathf.SmoothStep(0f, 1f, edgeFade);

                colors[idx] = new Color(1f, 1f, 1f, groundAlpha * widthAlpha);
                idx++;
            }
        }

        // 三角形インデックス
        int quadCount = ArcSegments * WidthSlices;
        int[] triangles = new int[quadCount * 6];
        int tIdx = 0;
        int sliceStride = WidthSlices + 1;

        for (int s = 0; s < ArcSegments; s++)
        {
            for (int w = 0; w < WidthSlices; w++)
            {
                int c0 = s * sliceStride + w;
                int c1 = c0 + 1;
                int c2 = (s + 1) * sliceStride + w;
                int c3 = c2 + 1;

                // 表面
                triangles[tIdx++] = c0;
                triangles[tIdx++] = c2;
                triangles[tIdx++] = c1;

                triangles[tIdx++] = c1;
                triangles[tIdx++] = c2;
                triangles[tIdx++] = c3;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 虹の7色スペクトルグラデーションテクスチャを生成
    /// </summary>
    Texture2D GenerateSpectrumTexture(bool isSecondary)
    {
        const int width = 256;
        const int height = 64;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.name = isSecondary ? "SecondaryRainbowSpectrum" : "PrimaryRainbowSpectrum";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];

        // 虹の純粋スペクトル基準色（赤・橙・黄・緑・シアン・青・紫）
        Color cRed     = new Color(1.00f, 0.18f, 0.18f);
        Color cOrange  = new Color(1.00f, 0.54f, 0.10f);
        Color cYellow  = new Color(1.00f, 0.92f, 0.15f);
        Color cGreen   = new Color(0.20f, 0.90f, 0.32f);
        Color cCyan    = new Color(0.12f, 0.88f, 0.96f);
        Color cBlue    = new Color(0.18f, 0.44f, 1.00f);
        Color cViolet  = new Color(0.70f, 0.22f, 0.92f);

        for (int y = 0; y < height; y++)
        {
            float v = (float)y / (height - 1);
            // 副虹は色の並びが自然界と同様に逆（内側が赤、外側が紫）
            float specT = isSecondary ? (1f - v) : v;

            Color baseColor;
            if (specT < 0.16f)
                baseColor = Color.Lerp(cViolet, cBlue, specT / 0.16f);
            else if (specT < 0.32f)
                baseColor = Color.Lerp(cBlue, cCyan, (specT - 0.16f) / 0.16f);
            else if (specT < 0.50f)
                baseColor = Color.Lerp(cCyan, cGreen, (specT - 0.32f) / 0.18f);
            else if (specT < 0.68f)
                baseColor = Color.Lerp(cGreen, cYellow, (specT - 0.50f) / 0.18f);
            else if (specT < 0.84f)
                baseColor = Color.Lerp(cYellow, cOrange, (specT - 0.68f) / 0.16f);
            else
                baseColor = Color.Lerp(cOrange, cRed, (specT - 0.84f) / 0.16f);

            // 帯の内外境界でのソフトな減衰
            float bandAlpha = Mathf.Sin(v * Mathf.PI);
            bandAlpha = Mathf.SmoothStep(0f, 1f, bandAlpha);

            for (int x = 0; x < width; x++)
            {
                float u = (float)x / (width - 1);
                // 角度両端のソフト減衰
                float arcAlpha = Mathf.Sin(u * Mathf.PI);
                arcAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(arcAlpha * 3.0f));

                Color finalColor = baseColor;
                finalColor.a = bandAlpha * arcAlpha;
                pixels[y * width + x] = finalColor;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
