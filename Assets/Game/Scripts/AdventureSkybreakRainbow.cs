using System.Collections;
using UnityEngine;

/// <summary>
/// エピローグ冒頭「わぁぁ……！見て、Float！世界はこんなにも広かったんだ……！！」のシーンで
/// 大空の正面に神々しく架かる雄大な大虹（主虹＋副虹のダブルレインボー＋内光グロー）。
/// 500mを超える巨大アーチと加算合成・HDRエミッションにより、
/// 太陽光を透かして自ら輝く奇跡の光の架け橋を創出する。
/// </summary>
public class AdventureSkybreakRainbow : MonoBehaviour
{
    static AdventureSkybreakRainbow _instance;

    [Header("Rainbow Mesh Settings")]
    const int ArcSegments = 96;
    const int WidthSlices = 8;
    const float ArcStartDeg = 12f;
    const float ArcEndDeg = 168f;

    // 主虹（Primary Rainbow）：鮮烈な7色プリズム光条
    const float PrimaryInnerRadius = 310f;
    const float PrimaryOuterRadius = 385f;
    const float PrimaryBaseAlpha = 0.98f;

    // 副虹（Secondary Rainbow）：外側に淡く逆順スペクトルで架かる
    const float SecondaryInnerRadius = 425f;
    const float SecondaryOuterRadius = 475f;
    const float SecondaryBaseAlpha = 0.48f;

    // 内光アーチ（Inner Sky Glow）：主虹の内側の大空が神々しく輝く大気現象
    const float GlowInnerRadius = 20f;
    const float GlowOuterRadius = 310f;

    Material _primaryMat;
    Material _secondaryMat;
    Material _glowMat;
    Texture2D _primaryTex;
    Texture2D _secondaryTex;
    Texture2D _glowTex;
    GameObject _primaryGo;
    GameObject _secondaryGo;
    GameObject _glowGo;

    float _currentAlpha = 0f;
    float _targetAlpha = 1f;
    float _fadeDuration = 4.2f;
    float _fadeTimer = 0f;
    bool _isFading = false;
    Vector3 _cachedForward = Vector3.forward;

    /// <summary>
    /// エピローグの指定位置・向き正面に大虹を生成してフェードイン開始
    /// </summary>
    public static AdventureSkybreakRainbow Spawn(Vector3 playerPos, Vector3 forwardDir)
    {
        if (_instance != null && _instance.gameObject != null)
        {
            _instance.Reposition(playerPos, forwardDir);
            _instance.StartFadeIn(4.2f);
            return _instance;
        }

        var existing = GameObject.Find("EpilogueGrandRainbow");
        if (existing != null)
            Destroy(existing);

        var root = new GameObject("EpilogueGrandRainbow");
        _instance = root.AddComponent<AdventureSkybreakRainbow>();
        _instance.BuildRainbow(playerPos, forwardDir);
        _instance.StartFadeIn(4.2f);
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

        Shader unlitSh = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Transparent");

        // 0. 内光アーチ（Inner Glow）：主虹の内側を淡く黄金〜シアンに満たす
        _glowTex = GenerateInnerGlowTexture();
        _glowMat = CreateRainbowMaterial(unlitSh, _glowTex, isAdditive: true);
        _glowGo = new GameObject("InnerRainbowGlow");
        _glowGo.transform.SetParent(transform, false);
        _glowGo.transform.localPosition = new Vector3(0f, 0f, 2.0f); // わずかに奥
        var mfGlow = _glowGo.AddComponent<MeshFilter>();
        mfGlow.sharedMesh = CreateArchMesh(GlowInnerRadius, GlowOuterRadius);
        var mrGlow = _glowGo.AddComponent<MeshRenderer>();
        mrGlow.sharedMaterial = _glowMat;
        mrGlow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrGlow.receiveShadows = false;

        // 1. 主虹（Primary Rainbow）の生成（加算合成＋鮮烈スペクトル）
        _primaryTex = GenerateSpectrumTexture(isSecondary: false);
        _primaryMat = CreateRainbowMaterial(unlitSh, _primaryTex, isAdditive: true);

        _primaryGo = new GameObject("PrimaryRainbowArch");
        _primaryGo.transform.SetParent(transform, false);
        _primaryGo.transform.localPosition = Vector3.zero;
        var mfPrimary = _primaryGo.AddComponent<MeshFilter>();
        mfPrimary.sharedMesh = CreateArchMesh(PrimaryInnerRadius, PrimaryOuterRadius);
        var mrPrimary = _primaryGo.AddComponent<MeshRenderer>();
        mrPrimary.sharedMaterial = _primaryMat;
        mrPrimary.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrPrimary.receiveShadows = false;

        // 2. 副虹（Secondary Rainbow）の生成（外側に淡く色の順序が逆転して架かる）
        _secondaryTex = GenerateSpectrumTexture(isSecondary: true);
        _secondaryMat = CreateRainbowMaterial(unlitSh, _secondaryTex, isAdditive: true);

        _secondaryGo = new GameObject("SecondaryRainbowArch");
        _secondaryGo.transform.SetParent(transform, false);
        _secondaryGo.transform.localPosition = new Vector3(0f, 0f, 1.0f);
        var mfSecondary = _secondaryGo.AddComponent<MeshFilter>();
        mfSecondary.sharedMesh = CreateArchMesh(SecondaryInnerRadius, SecondaryOuterRadius);
        var mrSecondary = _secondaryGo.AddComponent<MeshRenderer>();
        mrSecondary.sharedMaterial = _secondaryMat;
        mrSecondary.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrSecondary.receiveShadows = false;
    }

    static Material CreateRainbowMaterial(Shader sh, Texture2D tex, bool isAdditive)
    {
        var mat = new Material(sh);
        mat.mainTexture = tex;
        mat.SetTexture("_BaseMap", tex);
        mat.SetTexture("_MainTex", tex);
        var c = new Color(1f, 1f, 1f, 0f);
        mat.color = c;
        mat.SetColor("_BaseColor", c);
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", isAdditive ? 1f : 0f); // Additive or Alpha blend
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (isAdditive)
        {
            mat.EnableKeyword("_BLENDMODE_ADD");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        }
        else
        {
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 120;
        return mat;
    }

    void Reposition(Vector3 playerPos, Vector3 forwardDir)
    {
        Vector3 forwardFlat = Vector3.ProjectOnPlane(forwardDir, Vector3.up);
        if (forwardFlat.sqrMagnitude < 0.001f)
            forwardFlat = Vector3.forward;
        forwardFlat.Normalize();

        _cachedForward = forwardFlat;

        // プレイヤーの視界正面560m先、地平線近く（Y=10m）を着地点として配置
        Vector3 center = playerPos + forwardFlat * 560f;
        center.y = 10f;
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

        // 遠景スカイボックスのように、プレイヤーの移動に対してゆったりと相対距離を維持
        var player = AdventurePlayerController.Instance;
        if (player != null)
        {
            Vector3 targetCenter = player.transform.position + _cachedForward * 560f;
            targetCenter.y = 10f;
            transform.position = Vector3.Lerp(transform.position, targetCenter, Time.deltaTime * 1.8f);
        }

        // 太陽光と大気の反射による微細な光のきらめき・呼吸感（ブリージング）
        float shimmer = 1f + Mathf.Sin(Time.time * 1.6f) * 0.05f + Mathf.Cos(Time.time * 2.8f) * 0.03f;

        if (_primaryMat != null)
        {
            float a = _currentAlpha * PrimaryBaseAlpha * shimmer;
            // 加算合成＋HDR（1.4倍輝度）でBloomと連動し、空に浮かぶ発光プリズムを創出
            Color hdrColor = new Color(1.35f, 1.35f, 1.35f, Mathf.Clamp01(a));
            _primaryMat.color = hdrColor;
            _primaryMat.SetColor("_BaseColor", hdrColor);
        }

        if (_secondaryMat != null)
        {
            float a = _currentAlpha * SecondaryBaseAlpha * shimmer;
            Color hdrColor = new Color(1.20f, 1.20f, 1.20f, Mathf.Clamp01(a));
            _secondaryMat.color = hdrColor;
            _secondaryMat.SetColor("_BaseColor", hdrColor);
        }

        if (_glowMat != null)
        {
            float a = _currentAlpha * 0.22f * shimmer;
            Color glowColor = new Color(1.0f, 0.95f, 0.85f, Mathf.Clamp01(a));
            _glowMat.color = glowColor;
            _glowMat.SetColor("_BaseColor", glowColor);
        }
    }

    void OnDestroy()
    {
        if (_primaryMat != null) Destroy(_primaryMat);
        if (_secondaryMat != null) Destroy(_secondaryMat);
        if (_glowMat != null) Destroy(_glowMat);
        if (_primaryTex != null) Destroy(_primaryTex);
        if (_secondaryTex != null) Destroy(_secondaryTex);
        if (_glowTex != null) Destroy(_glowTex);
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
    /// 虹の内側の大空を神々しく満たす内光グローテクスチャ
    /// </summary>
    Texture2D GenerateInnerGlowTexture()
    {
        const int width = 128;
        const int height = 64;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.name = "RainbowInnerGlow";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = (float)y / (height - 1);
            // 内側ほど淡く、主虹の境界付近で最も美しく光る
            float radGlow = Mathf.Pow(v, 2.2f) * 0.85f;

            for (int x = 0; x < width; x++)
            {
                float u = (float)x / (width - 1);
                float arcGlow = Mathf.Sin(u * Mathf.PI);
                arcGlow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(arcGlow * 2.5f));

                // 暖かなシャンパンゴールド〜天上のシアンホワイト
                Color glow = new Color(1.0f, 0.94f, 0.82f, radGlow * arcGlow);
                pixels[y * width + x] = glow;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// 虹の7色スペクトルグラデーションテクスチャを生成
    /// </summary>
    Texture2D GenerateSpectrumTexture(bool isSecondary)
    {
        const int width = 512;
        const int height = 128;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.name = isSecondary ? "SecondaryRainbowSpectrum" : "PrimaryRainbowSpectrum";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];

        // 息を呑む極上7色プリズムスペクトル（鮮明・彩度アップ・透明感）
        Color cRed     = new Color(1.00f, 0.14f, 0.20f); // 真紅
        Color cOrange  = new Color(1.00f, 0.50f, 0.06f); // 鮮烈なオレンジ
        Color cYellow  = new Color(1.00f, 0.95f, 0.12f); // 輝くサンゴールド
        Color cGreen   = new Color(0.12f, 0.96f, 0.40f); // 瑞々しいエメラルド
        Color cCyan    = new Color(0.08f, 0.92f, 1.00f); // 抜けるようなシアンブルー
        Color cBlue    = new Color(0.12f, 0.46f, 1.00f); // 澄み渡るコバルト
        Color cViolet  = new Color(0.74f, 0.20f, 0.98f); // 幻想的なアメジストバイオレット

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
                arcAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(arcAlpha * 3.5f));

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
