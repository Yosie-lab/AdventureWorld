using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 4ヶ国語（日本語 / 英語 / 簡体字中国語 / スペイン語）対応ローカライズマネージャー
/// </summary>
public static class AdventureLocalizationManager
{
    public enum GameLanguage
    {
        Japanese = 0,
        English = 1,
        SimplifiedChinese = 2,
        Spanish = 3
    }

    public static readonly string[] LanguageNames = new string[]
    {
        "日本語",
        "English",
        "简体中文",
        "Español"
    };

    const string PrefKey = "Adventure_GameLanguage";
    static GameLanguage _currentLanguage = GameLanguage.Japanese;
    static bool _isInitialized = false;

    public static event Action OnLanguageChanged;

    public static GameLanguage CurrentLanguage
    {
        get
        {
            EnsureInitialized();
            return _currentLanguage;
        }
        set
        {
            EnsureInitialized();
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                PlayerPrefs.SetInt(PrefKey, (int)_currentLanguage);
                PlayerPrefs.Save();
                if (OnLanguageChanged != null)
                {
                    foreach (var d in OnLanguageChanged.GetInvocationList())
                    {
                        try { ((System.Action)d)(); }
                        catch (System.Exception ex) { UnityEngine.Debug.LogWarning($"[AdventureLocalizationManager] Error in listener: {ex.Message}"); }
                    }
                }
            }
        }
    }

    public static void CycleLanguage(int delta)
    {
        if (delta == 0) return;
        int n = LanguageNames.Length;
        int idx = (int)CurrentLanguage;
        idx = (idx + delta) % n;
        if (idx < 0) idx += n;
        CurrentLanguage = (GameLanguage)idx;
    }

    public static void NextLanguage() => CycleLanguage(1);

    public static void PrevLanguage() => CycleLanguage(-1);

    static void EnsureInitialized()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        if (PlayerPrefs.HasKey(PrefKey))
        {
            int saved = PlayerPrefs.GetInt(PrefKey, 0);
            _currentLanguage = (GameLanguage)Mathf.Clamp(saved, 0, 3);
        }
        else
        {
            // 初回起動時: OSのシステム言語を自動判定
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Japanese:
                    _currentLanguage = GameLanguage.Japanese;
                    break;
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                    _currentLanguage = GameLanguage.SimplifiedChinese;
                    break;
                case SystemLanguage.Spanish:
                    _currentLanguage = GameLanguage.Spanish;
                    break;
                default:
                    _currentLanguage = GameLanguage.English;
                    break;
            }
        }
    }

    /// <summary>キーに対応するローカライズ文字列を取得</summary>
    public static string Get(string key)
    {
        EnsureInitialized();
        if (_table.TryGetValue(key, out var translations))
        {
            int idx = (int)_currentLanguage;
            if (idx >= 0 && idx < translations.Length && !string.IsNullOrEmpty(translations[idx]))
                return translations[idx];
            // フォールバック: 英語 (1) -> 日本語 (0)
            if (translations.Length > 1 && !string.IsNullOrEmpty(translations[1]))
                return translations[1];
            if (translations.Length > 0 && !string.IsNullOrEmpty(translations[0]))
                return translations[0];
        }
        return key;
    }

    /// <summary>フォーマット引数付きのローカライズ文字列を取得</summary>
    /// <summary>辞書内の \\n を実改行に（状況ボード・ロア表示用）</summary>
    public static string NormalizeLineBreaks(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Replace("\\n", "\n");
    }

    public static string GetFormat(string key, params object[] args)
    {
        string pattern = NormalizeLineBreaks(Get(key));
        try
        {
            return string.Format(pattern, args);
        }
        catch
        {
            return pattern;
        }
    }

    // ==========================================
    // 辞書テーブル [0: 日本語, 1: 英語, 2: 簡体字中国語, 3: スペイン語]
    // ==========================================
    static readonly Dictionary<string, string[]> _table = new Dictionary<string, string[]>()
    {
        // --- タイトル画面 ---
        { "title_subtitle", new[] { "〜 2050 静かなる脱出 〜", "~ 2050 Silent Escape ~", "〜 2050 寂静逃亡 〜", "〜 2050 Escape Silencioso 〜" } },
        { "title_continue", new[] { "つづきから", "Continue", "继续游戏", "Continuar" } },
        { "title_newgame", new[] { "はじめから", "New Game", "新游戏", "Nueva Partida" } },
        { "title_settings", new[] { "環境設定", "Settings", "设置", "Ajustes" } },
        { "title_quit", new[] { "ゲーム終了", "Quit Game", "退出游戏", "Salir del Juego" } },
        { "title_story_head", new[] { "[時代背景と物語]", "[Story & Background]", "[时代背景与物语]", "[Historia y Trasfondo]" } },
        { "title_story_body", new[] {
            "西暦2050年。100%徹底された閉塞の孤島を脱出した。\n\n荒波を越え、気がつくとこの静謐な白砂のビーチに打ち上げられていた。\n頬を打つ冷たい潮風も、駆け抜けた足の痛みさえ、ここではすべてが生きている証そのものだ。\n\n……脇には、スクラップ寸前で連れ出した相棒ドローン、Rust。\n息を巡って翼を直し、あの自由な蒼穹（そら）へ飛び立とう。",
            "Year 2050. Escaped from the completely confined island.\n\nAfter weathering the stormy waves, you wash ashore on this serene white beach.\nThe chilly breeze against your cheeks and the ache in your tired legs are living proof of your freedom.\n\n...Beside you lies Rust, the companion drone rescued on the brink of being scrapped.\nCatch your breath, repair your wings, and soar into the vast open sky.",
            "公元2050年。成功逃离了那座彻底闭塞的孤岛。\n\n穿越汹涌风浪，回过神时已躺在这片静谧的白沙海滩上。\n掠过脸颊的清冷海风，奔波后酸痛的双足，在此刻皆是生命鲜活跳动的证明。\n\n……身旁是险遭报废、被你一同带出的伙伴无人机 Rust。\n平复呼吸，修整羽翼，再次向那片自由的苍穹翱翔吧。",
            "Año 2050. Has escapado de la isla totalmente confinada.\n\nTras superar el oleaje embravecido, despiertas en esta serena playa de arena blanca.\nLa brisa marina en tus mejillas y el cansancio en tus piernas son prueba viviente de tu libertad.\n\n...A tu lado yace Rust, el dron compañero rescatado al borde del desguace.\nRecupera el aliento, repara tus alas y elévate hacia el cielo infinito."
        } },

        // --- ポーズ ＆ 設定画面共通 ---
        { "pause_header", new[] { "✦ PAUSE & SETTINGS ✦", "✦ PAUSE & SETTINGS ✦", "✦ 暂停与设置 ✦", "✦ PAUSA Y AJUSTES ✦" } },
        { "pause_sub", new[] { "ゲーム設定・画面表示・操作ガイド・タイトル移動", "Game Settings, Display, Controls Guide, Main Menu", "游戏设置・画质显示・操作指南・返回标题", "Ajustes del juego, Pantalla, Guía de controles, Menú principal" } },
        { "settings_header", new[] { "✦ SETTINGS ✦", "✦ SETTINGS ✦", "✦ 设置 ✦", "✦ AJUSTES ✦" } },
        { "settings_sub", new[] { "音量・操作感度・画面表示・言語の設定", "Audio, Sensitivity, Display, and Language Settings", "音量・灵敏度・显示・语言设置", "Ajustes de audio, sensibilidad, pantalla e idioma" } },
        { "pause_resume", new[] { "▶ ゲームに戻る [ESC]", "▶ Resume Game [ESC]", "▶ 继续游戏 [ESC]", "▶ Continuar [ESC]" } },
        { "pause_close", new[] { "閉じる [ESC]", "Close [ESC]", "关闭 [ESC]", "Cerrar [ESC]" } },
        { "pause_restart", new[] { "↺ チェックポイントから再開", "↺ Restart from Checkpoint", "↺ 从检查点重试", "↺ Reiniciar desde punto de control" } },
        { "pause_title_return", new[] { "⌂ タイトルに戻る", "⌂ Return to Title", "⌂ 返回标题画面", "⌂ Volver al Título" } },

        // --- タブ名 ---
        { "tab_audio", new[] { "⚙  オーディオ・操作", "⚙  Audio & Input", "⚙  音频与操作", "⚙  Audio y Controles" } },
        { "tab_display", new[] { "🖥  画面・画質", "🖥  Display & Graphics", "🖥  显示与画质", "🖥  Pantalla y Gráficos" } },
        { "tab_guide", new[] { "📖  操作ガイド", "📖  Controls Guide", "📖  操作指南", "📖  Guía de Controles" } },
        { "tab_credits", new[] { "📜  クレジット・表記", "📜  Credits & Licenses", "📜  制作团队与授权", "📜  Créditos y Licencias" } },

        // --- オーディオ・操作設定 ---
        { "opt_language", new[] { "🌐 表示言語 (Language)", "🌐 Display Language", "🌐 显示语言 (Language)", "🌐 Idioma de Visualización" } },
        { "opt_bgm", new[] { "BGM 音量", "Music Volume", "背景音乐音量", "Volumen de Música" } },
        { "opt_se", new[] { "SE 音量", "Sound Effects (SE)", "音效音量", "Efectos de Sonido" } },
        { "opt_sensitivity", new[] { "カメラ操作感度 (マウス/スティック)", "Camera Sensitivity (Mouse/Stick)", "镜头旋转灵敏度 (鼠标/摇杆)", "Sensibilidad de Cámara (Ratón/Palanca)" } },
        { "opt_audio_hint", new[] {
            "✦ スライダーや言語設定は自動保存されます。コントローラーでも快適に操作可能です。",
            "✦ Audio and language settings are saved automatically. Fully controller supported.",
            "✦ 音频与语言设置将自动保存。完全支持手柄操作。",
            "✦ Los ajustes de audio e idioma se guardan automáticamente. Compatible con mando."
        } },

        // --- ディスプレイ・画質設定 ---
        { "opt_display_mode", new[] { "画面表示モード", "Display Mode", "显示模式", "Modo de Pantalla" } },
        { "opt_fullscreen", new[] { "✦ フルスクリーン", "✦ Fullscreen", "✦ 全屏显示", "✦ Pantalla Completa" } },
        { "opt_windowed", new[] { "✦ ウィンドウ表示", "✦ Windowed", "✦ 窗口模式", "✦ Modo Ventana" } },
        { "opt_quality", new[] { "グラフィック描画品質", "Graphics Quality", "画面渲染品质", "Calidad Gráfica" } },
        { "opt_quality_fast", new[] { "✦ 軽量 (Fast)", "✦ Fast", "✦ 流畅 (Fast)", "✦ Rápido" } },
        { "opt_quality_high", new[] { "✦ 標準 (High)", "✦ High", "✦ 标准 (High)", "✦ Alto" } },
        { "opt_quality_ultra", new[] { "✦ 最高 (Ultra)", "✦ Ultra", "✦ 极致 (Ultra)", "✦ Ultra" } },
        { "opt_vsync", new[] { "垂直同期 (VSync)", "V-Sync", "垂直同步 (V-Sync)", "Sincronización Vertical (V-Sync)" } },
        { "opt_vsync_on", new[] { "✓ 垂直同期 ON (安定)", "✓ VSync ON (Stable)", "✓ 垂直同步 开启 (稳定)", "✓ VSync ACTIVADO (Estable)" } },
        { "opt_vsync_off", new[] { "✕ 垂直同期 OFF (低遅延)", "✕ VSync OFF (Low Latency)", "✕ 垂直同步 关闭 (低延迟)", "✕ VSync DESACTIVADO (Baja latencia)" } },
        { "opt_resolution", new[] { "画面解像度", "Resolution", "分辨率预设", "Resolución" } },
        { "opt_hud_exploration", new[] { "探索HUD（画面上部）", "Exploration HUD (Top)", "探索HUD（画面上部）", "HUD de Exploración (Superior)" } },
        { "opt_hud_compass", new[] { "コンパス", "Compass", "罗盘", "Brújula" } },
        { "opt_hud_parts", new[] { "詳細ナビ（Tab）", "Detail nav (Tab)", "详细导航（Tab）", "Nav. detalle (Tab)" } },
        { "opt_hud_oil", new[] { "潤滑油", "Oil", "润滑油", "Aceite" } },
        { "opt_hud_show", new[] { "表示", "Show", "显示", "Mostrar" } },
        { "opt_hud_hide", new[] { "非表示", "Hide", "隐藏", "Ocultar" } },
        { "opt_hud_guide_hint", new[] {
            "★ 常時HUDはコンパス＋目標1行＋潤滑油。詳細（距離・箱・塔）は【Tab】固定または【Q】案内後10秒（ここでTab詳細の許可を切替）。",
            "★ Always: compass, one-line goal, oil. Details (distance, crates, tower): [Tab] pin or 10s after [Q] guide (toggle Tab detail here).",
            "★ 常显：罗盘、一行目标、润滑油。详细（距离、箱、塔）：【Tab】固定或【Q】引路后10秒（此处切换Tab详细）。",
            "★ Siempre: brújula, meta en 1 línea, aceite. Detalle (distancia, cajas, torre): [Tab] fijo o 10 s tras [Q] (activar Tab aquí)."
        } },

        // --- タイトルセーブ情報 ---
        { "title_save_summary", new[] {
            "✦ パーツ: {0}/3個　油: {1}　保存: {2}",
            "✦ Parts: {0}/3  Oil: {1}  Saved: {2}",
            "✦ 零件: {0}/3个　油: {1}　保存: {2}",
            "✦ Piezas: {0}/3  Aceite: {1}  Guardado: {2}"
        } },
        { "title_save_none", new[] {
            "※ セーブデータは見つかりませんでした",
            "※ No save data found",
            "※ 未找到存档数据",
            "※ No se encontraron datos guardados"
        } },

        // --- HUD・コンパス ---
        { "hud_parts_goal", new[] { "✦ 目標: パーツ回収 ({0}/{1}) [計 {2}pt]", "✦ Goal: Collect Scrap Parts ({0}/{1}) [{2} pts]", "✦ 目标: 收集零件 ({0}/{1}) [总计 {2}分]", "✦ Objetivo: Recuperar piezas ({0}/{1}) [{2} pts]" } },
        { "hud_nearest_part", new[] { "最寄りパーツ: {0} 約{1}m", "Nearest Scrap: {0} ~{1}m", "最近的零件: {0} 约{1}米", "Pieza más cercana: {0} ~{1}m" } },
        { "hud_oil", new[] { "潤滑油: {0}", "Lubricant: {0}", "润滑油: {0}", "Lubricante: {0}" } },
        { "hud_interact_pet", new[] { "[E / クリック] 撫でる / 注油", "[E / Click] Pet / Oil", "[E / 点击] 抚摸 / 注油", "[E / Clic] Acariciar / Lubricar" } },

        // --- 画面上部HUD（左上目標ボード ＆ 右上潤滑油ボード） ---
        { "hud_oil_label", new[] { "潤滑油", "Oil", "润滑油", "Aceite" } },
        { "hud_oil_well", new[] { "快調({0}s)", "Prime({0}s)", "快调({0}s)", "Óptimo({0}s)" } },
        { "hud_oil_cure_hint", new[] { "【E / クリック】手当て", "[E / Click] Oil Up", "【E / 点击】护理", "[E / Clic] Curar" } },
        { "hud_oil_cured_toast", new[] { "✦ Rustに油をさして手当てしました！", "✦ Rust lubricated and refreshed!", "✦ 已为Rust注入润滑油并保养！", "✦ ¡Rust ha sido lubricado y revitalizado!" } },

        { "hud_goal_cleared", new[] {
            "<color=#FFE066><b>✦ GAME CLEAR！</b></color> <color=#FFFFFF>箱庭からの脱獄達成！</color>",
            "<color=#FFE066><b>✦ GAME CLEAR!</b></color> <color=#FFFFFF>Escaped the miniature garden!</color>",
            "<color=#FFE066><b>✦ 游戏通关！</b></color> <color=#FFFFFF>成功逃离微缩庭园！</color>",
            "<color=#FFE066><b>✦ ¡JUEGO COMPLETADO!</b></color> <color=#FFFFFF>¡Escape del jardín en miniatura!</color>"
        } },
        { "hud_goal_canopy", new[] {
            "<color=#FFD700><b>✦ 天蓋崩壊！</b></color> <color=#FFFFFF>光の柱から空の裂け目へダイブ！</color>",
            "<color=#FFD700><b>✦ Canopy Shattered!</b></color> <color=#FFFFFF>Dive into the sky rift from the light pillar!</color>",
            "<color=#FFD700><b>✦ 天穹破裂！</b></color> <color=#FFFFFF>从光柱跃入天空裂缝！</color>",
            "<color=#FFD700><b>✦ ¡Cúpula rota!</b></color> <color=#FFFFFF>¡Lánzate a la grieta del cielo desde el pilar de luz!</color>"
        } },
        { "hud_goal_lever", new[] {
            "<color=#00E5FF><b>✦ レバーロック解除！</b></color> <color=#FFFFFF>中央タワーの黄金レバーへ</color> <color=#FFE066><b>({0}/20 pt)</b></color>",
            "<color=#00E5FF><b>✦ Lever Unlocked!</b></color> <color=#FFFFFF>Head to golden lever at central tower</color> <color=#FFE066><b>({0}/20 pts)</b></color>",
            "<color=#00E5FF><b>✦ 机关锁已解除！</b></color> <color=#FFFFFF>前往中央高塔的黄金拉杆</color> <color=#FFE066><b>({0}/20 分)</b></color>",
            "<color=#00E5FF><b>✦ ¡Palanca desbloqueada!</b></color> <color=#FFFFFF>Ve a la palanca dorada en la torre central</color> <color=#FFE066><b>({0}/20 pts)</b></color>"
        } },
        { "hud_goal_scrap_3", new[] {
            "<color=#FFD54F><b>✦ 目標:</b></color> <color=#FFFFFF><b>パーツ回収</b></color> <color=#00E5FF><b>({0}/3)</b></color> <color=#FFE066><b>(計 {1}/20 pt)</b></color>",
            "<color=#FFD54F><b>✦ Goal:</b></color> <color=#FFFFFF><b>Scrap Parts</b></color> <color=#00E5FF><b>({0}/3)</b></color> <color=#FFE066><b>(Total {1}/20 pts)</b></color>",
            "<color=#FFD54F><b>✦ 目标:</b></color> <color=#FFFFFF><b>零件回收</b></color> <color=#00E5FF><b>({0}/3)</b></color> <color=#FFE066><b>(总计 {1}/20 分)</b></color>",
            "<color=#FFD54F><b>✦ Objetivo:</b></color> <color=#FFFFFF><b>Recuperar piezas</b></color> <color=#00E5FF><b>({0}/3)</b></color> <color=#FFE066><b>(Total {1}/20 pts)</b></color>"
        } },
        { "hud_goal_scrap_6", new[] {
            "<color=#FFD54F><b>✦ 目標:</b></color> <color=#FFFFFF><b>反重力コア</b></color> <color=#00E5FF><b>({0}/6)</b></color> <color=#FFD54F>▶ 2段ジャンプ</color> <color=#FFE066><b>(計 {1}/20 pt)</b></color>",
            "<color=#FFD54F><b>✦ Goal:</b></color> <color=#FFFFFF><b>Anti-Gravity Core</b></color> <color=#00E5FF><b>({0}/6)</b></color> <color=#FFD54F>▶ Double Jump</color> <color=#FFE066><b>(Total {1}/20 pts)</b></color>",
            "<color=#FFD54F><b>✦ 目标:</b></color> <color=#FFFFFF><b>反重力核心</b></color> <color=#00E5FF><b>({0}/6)</b></color> <color=#FFD54F>▶ 二段跳跃</color> <color=#FFE066><b>(总计 {1}/20 分)</b></color>",
            "<color=#FFD54F><b>✦ Objetivo:</b></color> <color=#FFFFFF><b>Núcleo antigravedad</b></color> <color=#00E5FF><b>({0}/6)</b></color> <color=#FFD54F>▶ Doble salto</color> <color=#FFE066><b>(Total {1}/20 pts)</b></color>"
        } },
        { "hud_goal_scrap_9", new[] {
            "<color=#FFD54F><b>✦ 目標:</b></color> <color=#FFFFFF><b>探知ソナー</b></color> <color=#00E5FF><b>({0}/9)</b></color> <color=#FFD54F>▶ レーダー</color> <color=#FFE066><b>(計 {1}/20 pt)</b></color>",
            "<color=#FFD54F><b>✦ Goal:</b></color> <color=#FFFFFF><b>Scout Sonar</b></color> <color=#00E5FF><b>({0}/9)</b></color> <color=#FFD54F>▶ Radar</color> <color=#FFE066><b>(Total {1}/20 pts)</b></color>",
            "<color=#FFD54F><b>✦ 目标:</b></color> <color=#FFFFFF><b>探测声纳</b></color> <color=#00E5FF><b>({0}/9)</b></color> <color=#FFD54F>▶ 雷达</color> <color=#FFE066><b>(总计 {1}/20 分)</b></color>",
            "<color=#FFD54F><b>✦ Objetivo:</b></color> <color=#FFFFFF><b>Sónar de rastreo</b></color> <color=#00E5FF><b>({0}/9)</b></color> <color=#FFD54F>▶ Radar</color> <color=#FFE066><b>(Total {1}/20 pts)</b></color>"
        } },
        { "hud_goal_scrap_12", new[] {
            "<color=#FFD54F><b>✦ 目標:</b></color> <color=#FFFFFF><b>グライダー完成</b></color> <color=#00E5FF><b>({0}/12)</b></color> <color=#FFE066><b>(計 {1}/20 pt)</b></color>",
            "<color=#FFD54F><b>✦ Goal:</b></color> <color=#FFFFFF><b>Glider Complete</b></color> <color=#00E5FF><b>({0}/12)</b></color> <color=#FFE066><b>(Total {1}/20 pts)</b></color>",
            "<color=#FFD54F><b>✦ 目标:</b></color> <color=#FFFFFF><b>组装滑翔翼</b></color> <color=#00E5FF><b>({0}/12)</b></color> <color=#FFE066><b>(总计 {1}/20 分)</b></color>",
            "<color=#FFD54F><b>✦ Objetivo:</b></color> <color=#FFFFFF><b>Planeador completo</b></color> <color=#00E5FF><b>({0}/12)</b></color> <color=#FFE066><b>(Total {1}/20 pts)</b></color>"
        } },
        { "hud_goal_scrap_more", new[] {
            "<color=#FFD54F><b>✦ 目標:</b></color> <color=#FFFFFF><b>ケースやピアノでポイント獲得</b></color> <color=#FFE066><b>(計 {0}/20 pt)</b></color>",
            "<color=#FFD54F><b>✦ Goal:</b></color> <color=#FFFFFF><b>Gain pts from crates & piano</b></color> <color=#FFE066><b>(Total {0}/20 pts)</b></color>",
            "<color=#FFD54F><b>✦ 目标:</b></color> <color=#FFFFFF><b>通过宝箱与钢琴获取积分</b></color> <color=#FFE066><b>(总计 {0}/20 分)</b></color>",
            "<color=#FFD54F><b>✦ Objetivo:</b></color> <color=#FFFFFF><b>Consigue pts con cajas y el piano</b></color> <color=#FFE066><b>(Total {0}/20 pts)</b></color>"
        } },

        { "hud_sub_nearest_scrap", new[] {
            "<color=#80D8FF><b>📍 最寄りパーツ:</b></color> <color=#FFEB3B><b>{0}</b></color><color=#ECEFF1>（{1}）</color> <color=#69F0AE><b>約{2}m</b></color>",
            "<color=#80D8FF><b>📍 Nearest Scrap:</b></color> <color=#FFEB3B><b>{0}</b></color><color=#ECEFF1> ({1})</color> <color=#69F0AE><b>~{2}m</b></color>",
            "<color=#80D8FF><b>📍 最近零件:</b></color> <color=#FFEB3B><b>{0}</b></color><color=#ECEFF1>（{1}）</color> <color=#69F0AE><b>约{2}米</b></color>",
            "<color=#80D8FF><b>📍 Pieza cercana:</b></color> <color=#FFEB3B><b>{0}</b></color><color=#ECEFF1> ({1})</color> <color=#69F0AE><b>~{2}m</b></color>"
        } },
        { "hud_sub_collect_hint", new[] {
            "<color=#B0BEC5>📍 漂着パーツ(1pt)・ケース(2pt)・ピアノ(3pt)を集めよう！</color>",
            "<color=#B0BEC5>📍 Collect drift scrap (1pt), crates (2pt), & piano (3pt)!</color>",
            "<color=#B0BEC5>📍 收集漂着零件(1分)、宝箱(2分)、钢琴(3分)！</color>",
            "<color=#B0BEC5>📍 ¡Consigue piezas (1pt), cajas (2pt) y el piano (3pt)!</color>"
        } },
        { "hud_sub_crystal", new[] {
            "<color=#00E5FF><b>⚡ 古代クリスタル: {0}方角（{1}） 約{2}m 【Fキー送電】</b></color>  |  ",
            "<color=#00E5FF><b>⚡ Ancient Crystal: {0} ({1}) ~{2}m [F: Transmit]</b></color>  |  ",
            "<color=#00E5FF><b>⚡ 古代水晶: {0}方位（{1}） 约{2}米 【F键输电】</b></color>  |  ",
            "<color=#00E5FF><b>⚡ Cristal antiguo: {0} ({1}) ~{2}m [F: Transmitir]</b></color>  |  "
        } },
        { "hud_sub_campfire_workshop", new[] {
            " <color=#FFD54F><b>【🔥砂浜の焚き火＆Bキー工房解放中！】</b></color>",
            " <color=#FFD54F><b>[🔥Beach Campfire & B-key Workshop Unlocked!]</b></color>",
            " <color=#FFD54F><b>【🔥沙滩篝火＆B键工坊已解锁！】</b></color>",
            " <color=#FFD54F><b>[🔥¡Hoguera y taller con tecla B disponibles!]</b></color>"
        } },
        { "hud_sub_pull_lever", new[] {
            "<size=24><color=#FFE066><b>✨ 【Eキー】で巨大真鍮レバーを引く！</b></color></size> <color=#80D8FF>（天蓋開放開始）</color>",
            "<size=24><color=#FFE066><b>✨ Pull golden lever with [E]!</b></color></size> <color=#80D8FF>(Open Canopy)</color>",
            "<size=24><color=#FFE066><b>✨ 按【E键】拉动巨大的黄金手柄！</b></color></size> <color=#80D8FF>（开启天穹）</color>",
            "<size=24><color=#FFE066><b>✨ ¡Tira de la palanca dorada con [E]!</b></color></size> <color=#80D8FF>(Abre la cúpula)</color>"
        } },
        { "hud_sub_lever_tower", new[] {
            "<color=#80D8FF><b>📍 レバー解除済:</b></color> <color=#FFEB3B><b>中央タワー白亜テラス（{0}）</b></color> <color=#69F0AE><b>約{1}m</b></color> <color=#80D8FF>【レバーを引いて天蓋開放】</color>",
            "<color=#80D8FF><b>📍 Lever Unlocked:</b></color> <color=#FFEB3B><b>Central Tower Terrace ({0})</b></color> <color=#69F0AE><b>~{1}m</b></color> <color=#80D8FF>[Pull lever to open canopy]</color>",
            "<color=#80D8FF><b>📍 机关已解锁:</b></color> <color=#FFEB3B><b>中央高塔白垩露台（{0}）</b></color> <color=#69F0AE><b>约{1}米</b></color> <color=#80D8FF>【拉动手柄开启天顶】</color>",
            "<color=#80D8FF><b>📍 Desbloqueado:</b></color> <color=#FFEB3B><b>Terraza central ({0})</b></color> <color=#69F0AE><b>~{1}m</b></color> <color=#80D8FF>[Tira de la palanca para abrir]</color>"
        } },
        { "hud_sub_light_pillar_near", new[] {
            "<size=15><color=#FFE066><b>✨ 中央の光の柱へ！</b></color> <color=#80D8FF>（大空へ無限再跳躍できます）</color></size>",
            "<size=15><color=#FFE066><b>✨ Head into the Central Light Pillar!</b></color> <color=#80D8FF>(Launch into sky freely)</color></size>",
            "<size=15><color=#FFE066><b>✨ 前往中央光柱！</b></color> <color=#80D8FF>（可无限次飞向天空）</color></size>",
            "<size=15><color=#FFE066><b>✨ ¡Ve al pilar de luz central!</b></color> <color=#80D8FF>(Vuela libremente al cielo)</color></size>"
        } },
        { "hud_sub_light_pillar_far", new[] {
            "<color=#80D8FF><b>📍 自由飛行探索中:</b></color> <color=#FFD700><b>タワー中心の光の柱（{0} 約{1}m）で大空へ再ダイブ！</b></color>",
            "<color=#80D8FF><b>📍 Free Flight:</b></color> <color=#FFD700><b>Tower Light Pillar ({0} ~{1}m) to dive into sky!</b></color>",
            "<color=#80D8FF><b>📍 自由飞行探索中:</b></color> <color=#FFD700><b>进入高塔中心光柱（{0} 约{1}米）重返天际！</b></color>",
            "<color=#80D8FF><b>📍 Vuelo libre:</b></color> <color=#FFD700><b>¡Pilar de luz de la torre ({0} ~{1}m) para volar!</b></color>"
        } },
        { "hud_sub_light_pillar_target", new[] {
            "<color=#80D8FF><b>📍 目標:</b></color> <color=#FFD700><b>タワー中心の光の柱（{0}）</b></color> <color=#69F0AE><b>約{1}m</b></color> <color=#80D8FF>【光の柱に入ると大空へ打ち上がります】</color>",
            "<color=#80D8FF><b>📍 Goal:</b></color> <color=#FFD700><b>Tower Light Pillar ({0})</b></color> <color=#69F0AE><b>~{1}m</b></color> <color=#80D8FF>[Enter to launch into the sky]</color>",
            "<color=#80D8FF><b>📍 目标:</b></color> <color=#FFD700><b>高塔中心光柱（{0}）</b></color> <color=#69F0AE><b>约{1}米</b></color> <color=#80D8FF>【进入光柱将被发射至长空】</color>",
            "<color=#80D8FF><b>📍 Objetivo:</b></color> <color=#FFD700><b>Pilar de luz central ({0})</b></color> <color=#69F0AE><b>~{1}m</b></color> <color=#80D8FF>[Entra para lanzarte al cielo]</color>"
        } },

        // --- 方角と地形ヒント ---
        { "dir_n", new[] { "北", "North", "北", "Norte" } },
        { "dir_ne", new[] { "北東", "Northeast", "东北", "Noreste" } },
        { "dir_e", new[] { "東", "East", "东", "Este" } },
        { "dir_se", new[] { "南東", "Southeast", "东南", "Sureste" } },
        { "dir_s", new[] { "南", "South", "南", "Sur" } },
        { "dir_sw", new[] { "南西", "Southwest", "西南", "Suroeste" } },
        { "dir_w", new[] { "西", "West", "西", "Oeste" } },
        { "dir_nw", new[] { "北西", "Northwest", "西北", "Noroeste" } },

        { "compass_mark_n", new[] { "北", "N", "北", "N" } },
        { "compass_mark_ne", new[] { "北東", "NE", "东北", "NE" } },
        { "compass_mark_e", new[] { "東", "E", "东", "E" } },
        { "compass_mark_se", new[] { "南東", "SE", "东南", "SE" } },
        { "compass_mark_s", new[] { "南", "S", "南", "S" } },
        { "compass_mark_sw", new[] { "南西", "SW", "西南", "SO" } },
        { "compass_mark_w", new[] { "西", "W", "西", "O" } },
        { "compass_mark_nw", new[] { "北西", "NW", "西北", "NO" } },

        { "hint_highground", new[] { "奥の高台", "High ground", "高台", "Terreno alto" } },
        { "hint_hills", new[] { "丘陵地帯", "Hills", "丘陵", "Colinas" } },
        { "hint_woods", new[] { "右奥の林", "Woods", "树林", "Bosque" } },
        { "hint_cliff", new[] { "崖側", "Cliff", "悬崖", "Acantilado" } },
        { "hint_beach", new[] { "手前の浜辺", "Beach", "海滩", "Playa" } },
        { "hint_shallows", new[] { "浅瀬", "Shallows", "浅滩", "Bajíos" } },
        { "hint_sea_oasis", new[] { "海・オアシス", "Sea / Oasis", "大海/绿洲", "Mar / Oasis" } },
        { "hint_precipice", new[] { "断崖", "Precipice", "险壁", "Precipicio" } },

        // --- 相棒Rustの台詞 ---
        { "rust_wind_highway", new[] { "「風の道に乗ったよ！すごく速い、Float！」", "\"We caught the wind highway! So fast, Float!\"", "“乘上风之轨道了！好快啊，Float！”", "¡Entramos en la corriente de viento! ¡Qué rápido, Float!" } },
        { "rust_dive_cheer", new[] { "「わああぁぁっ、大空へダイブだー！！」", "\"Woooow, diving into the open sky!!\"", "“哇啊啊——！向着长空俯冲啦——！！”", "¡¡Wooooow, picado en picado hacia el cielo abierto!!" } },
        { "rust_turn_cheer", new[] { "「お見事なターン！」", "\"Magnificent turn!\"", "“漂亮的转向！”", "¡Giro impecable!" } },
        { "rust_water_skimming", new[] { "「水鳥みたいだね、Float！」", "\"Just like a seabird skimming the waves, Float!\"", "“像水鸟一样贴水飞翔呢，Float！”", "¡Como un ave marina rozando las olas, Float!" } },
        { "rust_scrap_found", new[] { "「Float、パーツを発見したよ！あそこだ！」", "\"Float, I found a scrap part! Over there!\"", "“Float，发现零件了！就在那边！”", "¡Float, encontré una pieza! ¡Por allí!" } },
        { "rust_touch_cheer", new[] { "ピピッ！（パーツ回収完了！）", "Chirp! (Scrap Collected!)", "哔哔！（零件回收完毕！）", "¡Bip bip! (¡Pieza recuperada!)" } },
        { "rust_beach_vast_return", new[] {
            "広大な砂浜だね！戻るときはSpaceキーで海風に乗るか、海沿いの上昇気流や木道を使ってね！",
            "What a vast beach! To head back inland, hold Space to ride the sea breeze—or use the coastal thermals and boardwalks!",
            "好广阔的沙滩啊！回去时按住Space键乘风，或者走海边的上升气流和木道吧！",
            "¡Qué playa tan inmensa! Para volver al interior, mantén Space para surfear la brisa marina, o usa las térmicas costeras y las pasarelas."
        } },

        // --- 実績 (Achievements) ---
        { "ach_first_glide_title", new[] { "大空への第一歩", "First Flight", "飞向长空的第一步", "Primer Vuelo" } },
        { "ach_first_glide_desc", new[] { "初めてグライダーを展開して大空を滑空した", "Deployed the glider and took to the open skies for the first time.", "首次展开滑翔翼，在广袤天空中滑翔。", "Desplegaste el planeador y surcaste el cielo por primera vez." } },

        { "ach_find_scrap_title", new[] { "一筋の希望", "Ray of Hope", "一线希望", "Rayo de Esperanza" } },
        { "ach_find_scrap_desc", new[] { "最初のスクラップパーツを発見・回収した", "Discovered and retrieved your first scrap part.", "发现并回收了第一个废品零件。", "Encontraste y recuperaste tu primera pieza de chatarra." } },

        { "ach_meet_capybara_title", new[] { "秘湯の守護者", "Hot Spring Guardian", "秘汤守护者", "Guardián de las Aguas Termales" } },
        { "ach_meet_capybara_desc", new[] { "隠された温泉で心優しいカピバラに出会った", "Encountered the gentle capybara at the hidden hot spring.", "在隐秘温泉邂逅了温柔的水豚。", "Te encontraste con el amable capibara en las termas ocultas." } },

        { "ach_thermal_ride_title", new[] { "上昇気流に乗って", "Riding the Updraft", "乘风而上", "Cabalgando la Térmica" } },
        { "ach_thermal_ride_desc", new[] { "サーマル上昇気流を捉えて一気に高度を回復した", "Caught a thermal updraft and surged high into the sky.", "捕捉热上升气流，一举冲向高空。", "Atrapaste una corriente térmica y ascendiste a gran altura." } },

        { "ach_wind_highway_title", new[] { "蒼穹のハイウェイ", "Sky Highway", "苍穹高速道", "Autovía Celeste" } },
        { "ach_wind_highway_desc", new[] { "島を巡る高速気流ラインに乗り最高速で疾走した", "Surfed the island wind streamline at maximum velocity.", "乘上环岛高速气流线，以最高速度疾驰。", "Surfeaste la corriente de viento a máxima velocidad." } },

        { "ach_scrap_10_title", new[] { "熟練のスカベンジャー", "Skilled Scavenger", "熟练拾荒者", "Chatarrero Experto" } },
        { "ach_scrap_10_desc", new[] { "合計10個以上のスクラップパーツを収集した", "Collected 10 or more scrap parts across the island.", "在岛屿各处收集了10个以上的零件。", "Recolectaste 10 o más piezas de chatarra por la isla." } },

        { "ach_tower_unlock_title", new[] { "聖域の扉", "Sanctuary Gates", "圣域之门", "Puertas del Santuario" } },
        { "ach_tower_unlock_desc", new[] { "タワー周辺の封印レバーをすべて作動させた", "Activated all locking levers around the ancient tower.", "启动了古代高塔周边的所有封印机关手柄。", "Activaste todas las palancas de cierre alrededor de la torre." } },

        { "ach_sky_opened_title", new[] { "天蓋の開放", "The Canopy Opens", "苍穹之顶", "El Techo Abierto" } },
        { "ach_sky_opened_desc", new[] { "タワー頂上の天蓋を開放し、大空への脱出路を開いた", "Opened the sanctuary tower canopy, revealing the escape route.", "开启圣域高塔顶部的天顶天盖，打开逃亡之路。", "Abriste la cúpula de la torre, revelando la ruta de escape." } },

        { "ach_ledge_climb_title", new[] { "軽快な足取り", "Nimble Climber", "轻快步伐", "Escalador Ágil" } },
        { "ach_ledge_climb_desc", new[] { "レッジグラブを使って険しい崖段差を乗り越えた", "Used ledge climb to vault gracefully over steep rocky terrain.", "利用悬崖攀援，灵巧翻越险峻岩壁段差。", "Superaste un terreno rocoso y escarpado escalando cornisas." } },

        { "ach_scrap_all_title", new[] { "完全なる翼", "Wings of Perfection", "完全之翼", "Alas de Perfección" } },
        { "ach_scrap_all_desc", new[] { "島に眠るすべてのスクラップパーツを完全回収した", "Discovered every single scrap part hidden on the island.", "完整回收了散落于岛上的所有废品零件。", "Recuperaste hasta la última pieza de chatarra oculta en la isla." } },

        // --- クレジット・ライセンス ---
        { "credits_project_title", new[] { "[ 開発 ＆ プロジェクト ]", "[ Development & Project ]", "[ 开发与项目团队 ]", "[ Desarrollo y Proyecto ]" } },
        { "credits_project_body", new[] {
            "Rust & Float Project\n・ゲームデザイン＆滑空フライトダイナミクス設計\n・環境音響・プロシージャルサウンドスケープ設計\n・島嶼風脈・気流サーマルシステムエンジニアリング",
            "Rust & Float Project\n- Game Design & Flight Dynamics\n- Environmental Audio & Procedural Soundscapes\n- Island Wind Currents & Thermal Systems",
            "Rust & Float 项目团队\n・游戏设计与滑翔飞行动力学\n・环境音效与程序化声景系统\n・岛屿风脉与热气流工程",
            "Rust & Float Project\n- Diseño del juego y dinámica de vuelo\n- Audio ambiental y paisajes sonoros procedurales\n- Corrientes de viento insulares y sistemas térmicos"
        } },
        { "credits_license_title", new[] { "[ サードパーティ素材・正規ライセンス (Unity EULA) ]", "[ Third-Party Licenses (Unity EULA) ]", "[ 第三方素材与正版授权 (Unity EULA) ]", "[ Licencias de Terceros (Unity EULA) ]" } },
        { "credits_license_body", new[] {
            "本作品で使用されているモデル・テクスチャ・モーション素材は、Unity Asset Store Standard EULA規約に基づき正規許諾および商用利用権を取得しています。\n・Idyllic Fantasy Nature (Environment Assets)\n・Quirky Series Animals (Capybara Model & Motion)\n・PolyOne Characters (Niko & Companions)",
            "All 3D models, textures, and animations are legally licensed under the Unity Asset Store Standard EULA.\n- Idyllic Fantasy Nature (Environment Assets)\n- Quirky Series Animals (Capybara Model & Motion)\n- PolyOne Characters (Niko & Companions)",
            "本作中所使用的模型、贴图和动作资源均遵循 Unity Asset Store Standard EULA 获得合法商业授权。\n・Idyllic Fantasy Nature (环境美术资源)\n・Quirky Series Animals (水豚模型与动作)\n・PolyOne Characters (Niko与伙伴模型)",
            "Todos los modelos, texturas y animaciones cuentan con licencia comercial según el EULA de Unity Asset Store.\n- Idyllic Fantasy Nature (recursos de entorno)\n- Quirky Series Animals (modelo y animación de capibara)\n- PolyOne Characters (Niko y acompañantes)"
        } },
        { "credits_thanks_header", new[] { "[ 音源・フォント・謝辞 ]", "[ Audio, Fonts & Acknowledgements ]", "[ 音效・字体・鸣谢 ]", "[ Audio, Fuentes y Agradecimientos ]" } },
        { "credits_thanks_body", new[] {
            "・Dynamic Synthesized Audio Engine\n・Procedural Wind, Gliding, and Water Ambience\n・Kenney Open Game Assets & Community Sound Libraries",
            "・Dynamic Synthesized Audio Engine\n・Procedural Wind, Gliding, and Water Ambience\n・Kenney Open Game Assets & Community Sound Libraries",
            "・Dynamic Synthesized Audio Engine\n・程序化风声、滑翔与水流环境音效\n・Kenney 开放游戏资源与社区音效库",
            "・Dynamic Synthesized Audio Engine\n・Ambiente procedural de viento, vuelo y agua\n・Recursos abiertos de Kenney y librerías comunitarias"
        } },
        { "credits_platform_title", new[] { "[ 商用プラットフォーム対応状況 ]", "[ Commercial Platform Compatibility ]", "[ 商业平台兼容情况 ]", "[ Compatibilidad de Plataformas ]" } },
        { "credits_platform_body", new[] {
            "・Steam / Steam Deck 完全対応設計\n・ゲームパッド・Xbox / PlayStation / Deck コントローラーネイティブ対応\n・Steam Achievements（全10個の実績トラッカー＆通知UI）完全連動準備済\n・外部テレメトリや未許諾トラッキングの非搭載（GDPR/プライバシー配慮）",
            "- Fully compatible with Steam & Steam Deck\n- Native gamepad support (Xbox, PlayStation, Steam Deck)\n- Integrated Steam Achievements system (10 achievements)\n- Zero telemetry and no unauthorized tracking (GDPR compliant)",
            "・完美兼容 Steam 与 Steam Deck\n・原生支持 Xbox、PlayStation 及 Steam Deck 手柄操作\n・内置 Steam 成就系统（全10项成就联动）\n・不含任何外部遥测或未授权追踪（遵循GDPR隐私规范）",
            "- Totalmente compatible con Steam y Steam Deck\n- Soporte nativo para mandos (Xbox, PlayStation, Steam Deck)\n- Sistema integrado de Logros de Steam (10 logros)\n- Cero telemetría y sin rastreo no autorizado (cumple con GDPR)"
        } },

        // --- 確認モーダル（上書き / リセット / タイトル復帰） ---
        { "confirm_overwrite_title", new[] { "⚠️ セーブデータの上書き確認", "⚠️ Overwrite Save Data Confirmation", "⚠️ 覆盖存档确认", "⚠️ Confirmación de Sobrescritura" } },
        { "confirm_overwrite_desc", new[] {
            "これまでのセーブデータと収集記録をリセットし、\n最初から新しく冒険を開始します。\n本当によろしいですか？",
            "This will reset all progress and collected scrap parts,\nstarting a brand new adventure from the beginning.\nAre you sure you want to proceed?",
            "将重置所有游戏进度与收集记录，\n从头开始全新的冒险旅程。\n确定要继续吗？",
            "Esto reiniciará todo el progreso y las piezas recolectadas,\ncomenzando una nueva aventura desde el principio.\n¿Estás seguro de que deseas continuar?"
        } },
        { "confirm_overwrite_yes", new[] { "はい、新しくはじめる", "Yes, Start New Adventure", "是，开启新冒险", "Sí, empezar de nuevo" } },
        { "confirm_cancel", new[] { "キャンセル", "Cancel", "取消", "Cancelar" } },

        { "confirm_restart_title", new[] { "⚠️ 冒険の初期化確認", "⚠️ Restart from Beginning", "⚠️ 重新开始冒险确认", "⚠️ Reiniciar desde el Principio" } },
        { "confirm_restart_desc", new[] {
            "セーブデータと全パーツ・ポイントを初期化し、\n西側白砂ビーチの座礁艇前から新規開始します。\nよろしいですか？",
            "Reset all scrap parts and progress, returning to the stranded vessel at Paradise Beach.\nDo you wish to proceed?",
            "重置所有零件与探索点数，重新返回西侧白沙海滩的遇难艇前。\n确定要继续吗？",
            "Reiniciar todas las piezas y volver al bote encallado en la playa oeste.\n¿Deseas continuar?"
        } },
        { "confirm_restart_yes", new[] { "はい、初期化する", "Yes, Restart", "是，重置并开始", "Sí, reiniciar" } },

        { "confirm_title_title", new[] { "⌂ タイトルへ戻る確認", "⌂ Return to Title Confirmation", "⌂ 返回标题画面确认", "⌂ Volver al Título" } },
        { "confirm_title_desc", new[] {
            "現在の進行状況をクイックセーブしてタイトルに戻ります。\nよろしいですか？",
            "Save current progress and return to the main title screen.\nAre you sure?",
            "将保存当前探索进度并返回主标题画面。\n确定要返回吗？",
            "Guarda el progreso actual y vuelve a la pantalla de título.\n¿Deseas volver?"
        } },
        { "confirm_title_yes", new[] { "はい、タイトルへ戻る", "Yes, Return to Title", "是，返回标题", "Sí, volver al título" } },

        // --- 画面上アクション・指示プロンプト ---
        { "prompt_rust_fetch", new[] { "【F】光るパーツを取ってきて", "[F] Fetch glowing scrap part", "【F】去取发光的零件", "[F] Ve a por la pieza brillante" } },
        { "prompt_rust_pickup", new[] { "【F】Rustにパーツを拾わせる", "[F] Send Rust to collect scrap", "【F】让Rust回收零件", "[F] Enviar a Rust a recoger la pieza" } },
        { "prompt_rust_guide_talk", new[] { "【Q】パーツ案内  /  【E】Rustと話す", "[Q] Guide to Scrap / [E] Talk with Rust", "【Q】零件引路 / 【E】与Rust对话", "[Q] Guiar a la pieza / [E] Hablar con Rust" } },
        { "prompt_rust_guide_nearest", new[] { "【Q】相棒Rustに最寄りパーツを案内してもらう", "[Q] Ask Rust to guide to nearest scrap", "【Q】让Rust指引最近的零件", "[Q] Pedir a Rust que guíe a la pieza más cercana" } },
        { "prompt_rust_talk", new[] { "【E / クリック】Rustと話す", "[E / Click] Talk with Rust", "【E / 点击】与Rust对话", "[E / Clic] Hablar con Rust" } },
        { "prompt_workbench", new[] { "<b>[E / クリック] Rustクラフト工房</b>\n<size=16><color=#DDEEFF>地上で [B] キーでも開ける</color></size>", "<b>[E / Click] Rust Craft Workshop</b>\n<size=16><color=#DDEEFF>Press [B] on the ground to open</color></size>", "<b>[E / 点击] Rust工艺工坊</b>\n<size=16><color=#DDEEFF>在地面按 [B] 键也可打开</color></size>", "<b>[E / Clic] Taller de artesanía de Rust</b>\n<size=16><color=#DDEEFF>Pulsa [B] en el suelo para abrir</color></size>" } },
        { "modal_close_hint", new[] { "[ESC / クリック] 閉じる", "[ESC / Click] Close", "[ESC / 点击] 关闭", "[ESC / Clic] Cerrar" } },

        // --- 操作ガイド ---
        { "guide_basic_body", new[] {
            "<b>[ 基本操作 ＆ アクション ]</b>\n\n" +
            "<b>[W][A][S][D]</b>  移動 / 歩行\n" +
            "<b>[Shift]</b>  ダッシュ走行（速度アップ）\n" +
            "<b>[マウス移動]</b>  視点回転（左右360°/上下）\n" +
            "<b>[Space]</b>  ジャンプ / <b>長押しで滑空展開</b>\n" +
            "<b>[J]</b>  小ジャンプ（軽快・段差乗り越え）\n" +
            "<b>[W] / [S]</b>  ダイブ急降下 / 滞空フレア（滑空中）\n" +
            "<b>[A] / [D]</b>  機首バンク旋回（滑空中）",

            "<b>[ Basic Controls & Actions ]</b>\n\n" +
            "<b>[W][A][S][D]</b>  Move / Walk\n" +
            "<b>[Shift]</b>  Dash Run (Speed Boost)\n" +
            "<b>[Mouse]</b>  Camera Rotation (360° / Pitch)\n" +
            "<b>[Space]</b>  Jump / <b>Hold to Glide</b>\n" +
            "<b>[J]</b>  Short Hop (Nimble Step)\n" +
            "<b>[W] / [S]</b>  Steep Dive / Aerodynamic Flare (Gliding)\n" +
            "<b>[A] / [D]</b>  Bank Turn (Gliding)",

            "<b>[ 基本操作与动作 ]</b>\n\n" +
            "<b>[W][A][S][D]</b>  移动 / 行走\n" +
            "<b>[Shift]</b>  冲刺奔跑 (加速)\n" +
            "<b>[鼠标移动]</b>  视角旋转 (360°全向)\n" +
            "<b>[空格 Space]</b>  跳跃 / <b>长按展开滑翔翼</b>\n" +
            "<b>[J]</b>  轻跳 (灵巧越障)\n" +
            "<b>[W] / [S]</b>  俯冲急降 / 仰角减速拉平 (滑翔中)\n" +
            "<b>[A] / [D]</b>  机身倾斜转弯 (滑翔中)",

            "<b>[ Controles Básicos y Acciones ]</b>\n\n" +
            "<b>[W][A][S][D]</b>  Moverse / Caminar\n" +
            "<b>[Shift]</b>  Carrera rápida\n" +
            "<b>[Ratón]</b>  Rotación de cámara (360°)\n" +
            "<b>[Espacio]</b>  Saltar / <b>Mantener para planear</b>\n" +
            "<b>[J]</b>  Salto corto y ágil\n" +
            "<b>[W] / [S]</b>  Picado en picado / Frenado en el aire\n" +
            "<b>[A] / [D]</b>  Giro inclinado en planeo"
        } },

        { "guide_interact_body", new[] {
            "<b>[ インタラクト ＆ システム ]</b>\n\n" +
            "<b>[E] / [左クリック]</b>  調べる / 注油 / 撫でる\n" +
            "<b>[Q] / [中クリック]</b>  <b>相棒Rustに道案内を頼む（最寄り目標へ先行フライト）</b>\n" +
            "<b>[F]</b>  遠隔パーツ回収 / 視線偵察指示\n" +
            "<b>[B]</b>  Rustクラフト工房を開く\n" +
            "<b>[Tab]</b>  詳細ナビ（距離・箱・塔）固定 ON/OFF\n" +
            "<b>[F5] / [Cmd+S]</b>  クイックセーブ\n" +
            "<b>[ESC] / [Pad B]</b>  ポーズ ＆ 設定メニュー開閉\n\n" +
            "✦ コンパスHUDと相棒Floatが最寄りの風脈・パーツ・タワーを常時案内します。",

            "<b>[ Interactions & System ]</b>\n\n" +
            "<b>[E] / [Left Click]</b>  Inspect / Lubricate / Pet\n" +
            "<b>[Q] / [Middle Click]</b>  <b>Ask Rust to guide (scout to the nearest goal)</b>\n" +
            "<b>[F]</b>  Remote Scrap Retrieve / Scout Command\n" +
            "<b>[B]</b>  Open Rust Craft Workshop\n" +
            "<b>[Tab]</b>  Pin detail nav (distance, crates, tower) ON/OFF\n" +
            "<b>[F5] / [Cmd+S]</b>  Quick Save\n" +
            "<b>[ESC] / [Pad B]</b>  Pause & Settings Menu\n\n" +
            "✦ Compass HUD & Float always guide you to nearby currents and scrap.",

            "<b>[ 互动与系统功能 ]</b>\n\n" +
            "<b>[E] / [左键点击]</b>  调查 / 注油 / 抚摸\n" +
            "<b>[Q] / [中键点击]</b>  <b>请求Rust引路 (飞向最近目标)</b>\n" +
            "<b>[F]</b>  远程回收零件 / 视线侦察指令\n" +
            "<b>[B]</b>  打开Rust工艺工坊\n" +
            "<b>[Tab]</b>  固定/取消详细导航（距离·箱·塔）\n" +
            "<b>[F5] / [Cmd+S]</b>  快速存档\n" +
            "<b>[ESC] / [手柄 B]</b>  暂停与设置菜单\n\n" +
            "✦ 罗盘HUD与Float将时刻为您指引风脉、零件与圣域之塔。",

            "<b>[ Interacción y Sistema ]</b>\n\n" +
            "<b>[E] / [Clic Izquierdo]</b>  Examinar / Lubricar / Acariciar\n" +
            "<b>[Q] / [Clic Central]</b>  <b>Pedir guía a Rust (vuela al objetivo cercano)</b>\n" +
            "<b>[F]</b>  Recuperar pieza a distancia / Orden de exploración\n" +
            "<b>[B]</b>  Abrir el taller de artesanía de Rust\n" +
            "<b>[Tab]</b>  Fijar nav. detalle (distancia, cajas, torre) ON/OFF\n" +
            "<b>[F5] / [Cmd+S]</b>  Guardado rápido\n" +
            "<b>[ESC] / [Mando B]</b>  Pausa y menú de ajustes\n\n" +
            "✦ La brújula y Float te guiarán siempre hacia las corrientes y piezas."
        } },

        // ==========================================
        // ゲームクリアボード
        // ==========================================
        { "clear_title", new[] {
            "✦ 『Rust & Float』 GAME CLEAR ✦",
            "✦ 『Rust & Float』 GAME CLEAR ✦",
            "✦ 『Rust & Float』 游戏通关 ✦",
            "✦ 『Rust & Float』 ¡JUEGO COMPLETADO! ✦"
        } },
        { "clear_body", new[] {
            "天蓋の檻を打ち破り、二人は蒼い風が吹く空へ羽ばたいた。\n\n" +
            "✦ 漂着古代パーツ回収： {0} / {1}\n" +
            "✦ 相棒Rust： {2}\n\n" +
            "「ありがとう、Float。僕たちの翼で、どこまでも行こう……！」\n\n" +
            "【Space】大空へ　【N】はじめから　【E / Esc】閉じる",

            "Breaking through the cage of the canopy, the two soared into the azure breeze.\n\n" +
            "✦ Ancient Parts Recovered: {0} / {1}\n" +
            "✦ Companion Rust: {2}\n\n" +
            "\"Thank you, Float. With our wings, we can go anywhere...!\"\n\n" +
            "[Space] To the Sky   [N] New Game   [E / Esc] Close",

            "冲破天穹的樊笼，两人向着吹拂着苍蓝清风的天空振翅高飞。\n\n" +
            "✦ 漂流古代零件回收： {0} / {1}\n" +
            "✦ 伙伴 Rust： {2}\n\n" +
            "「谢谢你，Float。凭借我们的羽翼，无论何方都能抵达……！」\n\n" +
            "【Space】飞向苍穹　【N】重新开始　【E / Esc】关闭",

            "Rompiendo la jaula de la cúpula, ambos se elevaron hacia la brisa azul.\n\n" +
            "✦ Piezas antiguas recuperadas: {0} / {1}\n" +
            "✦ Compañero Rust: {2}\n\n" +
            "\"¡Gracias, Float! Con nuestras alas, ¡podemos ir a donde sea...!\"\n\n" +
            "[Espacio] Hacia el Cielo   [N] Nueva Partida   [E / Esc] Cerrar"
        } },
        { "clear_rust_abilities", new[] {
            "二段ジャンプ・超滑空・探知ソナー",
            "Double Jump, Hyper Glide, Sonar Radar",
            "二段跳跃・超级滑翔・探知声呐",
            "Doble salto, Superplaneo, Radar sonar"
        } },
        { "clear_btn_dive", new[] {
            "【Space】大空へダイブ",
            "【Space】Dive into Sky",
            "【Space】跃入天空",
            "【Espacio】Zambullirse al Cielo"
        } },
        { "clear_btn_new", new[] {
            "【N】はじめから",
            "【N】New Game",
            "【N】重新开始",
            "【N】Nueva Partida"
        } },
        { "clear_btn_close", new[] {
            "【E】閉じる",
            "【E】Close",
            "【E】关闭",
            "【E】Cerrar"
        } },

        // ==========================================
        // エピローグ・映画シネマ字幕（全9行）
        // ==========================================
        { "film_line_0", new[] {
            "「わぁぁ……！見て、Float！　世界はこんなにも広かったんだ……！！」",
            "\"Woooow...! Look, Float! The world was this vast all along...!!\"",
            "「哇啊啊……！快看，Float！世界原来是如此的辽阔……！！」",
            "\"¡Uaaah...! ¡Mira, Float! El mundo era tan inmenso después de todo...!!\""
        } },
        { "film_line_1", new[] {
            "天蓋の檻を突き抜けたその先——",
            "Beyond the cage of the shattered canopy—",
            "穿透了天穹牢笼的那片彼方——",
            "Más allá de la jaula de la cúpula rota—"
        } },
        { "film_line_2", new[] {
            "箱庭の外には、凍えるほどリアルで、優しい風が吹いていた。",
            "Outside the miniature garden, a wind blew—freezing, real, and gentle.",
            "微缩庭园之外，吹拂着凛冽逼真却又温柔的风。",
            "Fuera del jardín en miniatura soplaba un viento gélido, real y apacible."
        } },
        { "film_line_3", new[] {
            "人は、最適で最短な道を進むときだけじゃなく、",
            "Life is not solely defined by walking the fastest and optimal path;",
            "人的一生，绝不仅仅在于奔走于最优最短的捷径，",
            "La vida no se define solo por seguir el camino más rápido y óptimo;"
        } },
        { "film_line_4", new[] {
            "寄り道をして、躓きながらも出会えた感動に——",
            "In taking detours, in stumbling yet discovering wonder along the way—",
            "而在绕道漫游、磕磕绊绊中所邂逅的每一次感动中——",
            "En los desvíos, en los tropiezos que nos llevan a descubrir maravillas—"
        } },
        { "film_line_5", new[] {
            "真の生きている証(あかし)を得るんだ。",
            "We discover the true proof of being alive.",
            "我们才获得了真正活着的明证。",
            "Es donde hallamos la verdadera prueba de estar vivos."
        } },
        { "film_line_6", new[] {
            "傷つくかもしれない自由と、",
            "With the freedom that may bring wounds,",
            "怀抱着或许会受伤害的自由，",
            "Con la libertad que tal vez hiera,"
        } },
        { "film_line_7", new[] {
            "生きることの重みを取り戻した二人の旅が、",
            "And the reclaimed weight of existence, their journey—",
            "以及重拾生命厚重的两人之旅，",
            "Y el peso recobrado de la existencia, su viaje—"
        } },
        { "film_line_8", new[] {
            "今 ーここから、また始まる。—— 『Rust & Float』",
            "Begins anew right here, right now. —— 『Rust & Float』",
            "在此刻、自这里，再度启程。—— 『Rust & Float』",
            "Comienza de nuevo aquí y ahora. —— 『Rust & Float』"
        } },

        // ==========================================
        // 天蓋崩壊台本ビート（全7ビート）
        // ==========================================
        { "canopy_0_title", new[] { "天蓋崩壊　未知の荒野への眺望", "Canopy Shattered: View into the Unknown", "天穹崩塌 远眺未知荒野", "Cúpula Destrozada: Vista hacia lo Desconocido" } },
        { "canopy_0_body", new[] {
            "空が割れた。\n冷たいリアルな風が頬を打つ。",
            "The sky has cracked.\nA freezing, real wind strikes your cheeks.",
            "天空裂开了。\n冰冷而真实的狂风掠过面颊。",
            "El cielo se ha quebrado.\nUn viento gélido y real azota tus mejillas."
        } },
        { "canopy_1_speaker", new[] { "✦ 相棒 Rust", "✦ Companion Rust", "✦ 伙伴 Rust", "✦ Compañero Rust" } },
        { "canopy_1_body", new[] {
            "この楽園もAIに最適化された虚構の島だったんだ!!",
            "This paradise was nothing more than an AI-optimized illusion!!",
            "原来这座乐园也是被AI最优化过的虚构之岛！！",
            "¡¡Este paraíso no era más que una ilusión optimizada por una IA!!"
        } },
        { "canopy_2_speaker", new[] { "✦ 相棒 Rust", "✦ Companion Rust", "✦ 伙伴 Rust", "✦ Compañero Rust" } },
        { "canopy_2_body", new[] {
            "空が……割れるよ、Float！　つかまって！！",
            "The sky is... tearing open, Float! Hold on tight!!",
            "天空……要裂开了，Float！抓紧了！！",
            "¡El cielo se está... abriendo, Float! ¡¡Agárrate fuerte!!"
        } },
        { "canopy_3_speaker", new[] { "✦ Float", "✦ Float", "✦ Float", "✦ Float" } },
        { "canopy_3_body", new[] {
            "ありがとうRust…！あなたのおかげでここまでたどり着くことができた。",
            "Thank you, Rust...! Thanks to you, we made it this far.",
            "谢谢你，Rust…！正是多亏了你，我们才能来到这里。",
            "¡Gracias, Rust...! Gracias a ti pudimos llegar hasta aquí."
        } },
        { "canopy_4_speaker", new[] { "✦ 相棒 Rust", "✦ Companion Rust", "✦ 伙伴 Rust", "✦ Compañero Rust" } },
        { "canopy_4_body", new[] {
            "あれが本物の空だ……！風に乗って、あの裂け目へ飛び込もう、Float！！",
            "That's the real sky...! Catch the wind and dive into the rift, Float!!",
            "那就是真正的天空……！乘上风势，向那道裂缝跃进吧，Float！！",
            "¡Ese es el cielo real...! ¡Coge el viento y salta a la grieta, Float!!"
        } },
        { "canopy_5_body", new[] {
            "タワー中央の光の柱へ飛び込み、\n空の裂け目へ突き抜ける。",
            "Dive into the light pillar at the tower's center\nand break through the rift in the sky.",
            "跃入高塔中央的光之光柱，\n一举冲破天空的裂缝吧。",
            "Lánzate al pilar de luz del centro de la torre\ny atraviesa la brecha en el cielo."
        } },
        { "canopy_6_title", new[] { "空の裂け目へ", "Into the Sky Rift", "向天空裂缝飞跃", "Hacia la Brecha del Cielo" } },
        { "canopy_6_body", new[] {
            "【Space長押し / クリック】でダイブする",
            "【Hold Space / Click】to Dive",
            "【按住Space / 点击】进行飞跃",
            "【Mantén Espacio / Clic】para Zambullirse"
        } },
        { "canopy_dive_prompt", new[] {
            "<size=34><color=#FFD700><b>【 Space / クリック長押し 】</b></color> 光の柱へダイブ！</size>",
            "<size=34><color=#FFD700><b>[ Hold Space / Click ]</b></color> Dive into Light Pillar!</size>",
            "<size=34><color=#FFD700><b>【 按住Space / 长按点击 】</b></color> 跃入光之柱！</size>",
            "<size=34><color=#FFD700><b>[ Mantener Espacio / Clic ]</b></color> ¡Zambullirse al Pilar!</size>"
        } },
        { "canopy_diving_prompt", new[] {
            "<size=34><color=#FFE073><b>【 光の柱へダイブ中……！ 】</b></color></size>",
            "<size=34><color=#FFE073><b>[ Diving into Light Pillar...! ]</b></color></size>",
            "<size=34><color=#FFE073><b>【 正在跃入光之柱……！ 】</b></color></size>",
            "<size=34><color=#FFE073><b>[ ¡Zambulléndose en el pilar de luz...! ]</b></color></size>"
        } },

        // ==========================================
        // クライマックス台本ビート（全5ビート）
        // ==========================================
        { "climax_0_title", new[] { "限界高度", "Altitude Limit", "极限高度", "Límite de Altitud" } },
        { "climax_0_body", new[] {
            "天蓋の裂け目から、凍てつく突風が吹き荒れる——\nRustのギアが、悲鳴のようなきしみ音を上げていた。",
            "From the rift in the canopy, a freezing gale roars—\nRust's gears emit a screeching, strained groan.",
            "从天穹裂缝深处，狂暴的冻骨寒风呼啸而过——\nRust的齿轮发出了近乎悲鸣般的刺耳摩擦声。",
            "Desde la brecha de la cúpula ruge un vendaval helado—\nLos engranajes de Rust emiten un chirrido agónico."
        } },
        { "climax_1_body", new[] {
            "ギギッ……！ Float……身体が……冷え切って動かないよ……！\nギアが……凍りついちゃう……！",
            "Skrrr... Float... My body... is frozen, I can't move...!\nMy gears... are freezing solid...!",
            "吱吱……！Float……身体……冻僵了动不了了……！\n齿轮……快要冻结了……！",
            "¡Grrrk...! Float... Mi cuerpo... ¡está helado, no puedo moverme...!\n¡Mis engranajes... se están congelando...!"
        } },
        { "climax_2_body", new[] {
            "Rust、待ってて！　今、集めた油を全部注ぐから……！",
            "Rust, hang on! I'm pouring all the oil we gathered right now...!",
            "Rust，坚持住！我现在就把收集到的所有润滑油都注入进去……！",
            "¡Rust, espera! ¡Voy a verter todo el aceite que reunimos ahora mismo...!"
        } },
        { "climax_3_body", new[] {
            "……あ……温かい油が……心臓部に……じわっと染み込んでいく……！",
            "...Ah... The warm oil... is soaking into my core...!",
            "……啊……温热的润滑油……正渐渐浸润进核心……！",
            "...Ah... El aceite tibio... ¡se está filtrando en mi núcleo...!"
        } },
        { "climax_4_body", new[] {
            "ピピッ！……ありがとう、Float！これで僕たちの翼は折れることはないよ！　大空の向こうまで、全力で行こう！！",
            "Pip-pip! ...Thank you, Float! Now our wings will never break! Let's fly beyond the horizon at full speed!!",
            "哔哔！……谢谢你，Float！这样我们的羽翼就绝不会折断了！向着辽阔天空的彼方，全速进发吧！！",
            "¡Pip-pip! ...¡Gracias, Float! ¡Ahora nuestras alas jamás se romperán! ¡¡Vamos con todo hacia el más allá del cielo!!"
        } },
        { "climax_oil_title", new[] {
            "限界高度 — Rustが凍結",
            "Altitude limit — Rust is freezing",
            "极限高度 — Rust正在冻结",
            "Límite de altitud — Rust se congela"
        } },
        { "climax_oil_prompt", new[] {
            "常備油を心臓部へ",
            "Pour oil into the core",
            "将润滑油注入核心",
            "Vierte aceite al núcleo"
        } },
        { "climax_oil_prompt_keys", new[] {
            "【E / Space / 長押し】",
            "【 hold E / Space / click 】",
            "【 长按 E / Space / 点击 】",
            "【 mantén E / Espacio / clic 】"
        } },
        { "climax_oil_hold", new[] {
            "長押しで注油",
            "Hold to pour",
            "长按注油",
            "Mantén para verter"
        } },

        // ==========================================
        // Rust自律セリフ・呼びかけ
        // ==========================================
        { "rust_boot", new[] {
            "ピピッ…！起動したよ、Float。一緒に行こう！",
            "Pip-pip...! Systems online, Float. Let's explore together!",
            "哔哔……！系统已启动，Float。一起出发吧！",
            "¡Pip-pip...! Sistemas en línea, Float. ¡Vámonos juntos!"
        } },
        { "rust_near_tower", new[] {
            "タワーに着いたよ！白亜のテラスに黄金のレバーがある！引いてみて、Float！！",
            "We reached the tower! There's a golden lever on the chalk terrace! Pull it, Float!!",
            "到达高塔了！白垩露台上有一根黄金拉杆！快拉动看看，Float！！",
            "¡Llegamos a la torre! ¡Hay una palanca dorada en la terraza blanca! ¡Tira de ella, Float!!"
        } },
        { "rust_guide_tower", new[] {
            "こっちだよ！中央タワーはこの方向だ！レバーを引きに行こう！",
            "This way! The central tower is in this direction! Let's pull the lever!",
            "这边！中央高塔就在这个方向！一起去拉动机关吧！",
            "¡Por aquí! ¡La torre central está en esta dirección! ¡Vamos a activar la palanca!"
        } },
        { "rust_guide_tower_20pt", new[] {
            "ピピッ！中央の白亜タワーへ行こう！レバーを引けるようになったよ！",
            "Beep! Let's head to the central white tower! The lever is unlocked now!",
            "哔哔！去中央白垩高塔吧！机关已经可以拉动啦！",
            "¡Bip! ¡Vamos a la torre blanca central! ¡Ya puedes accionar la palanca!"
        } },
        { "rust_guide_tower_short", new[] {
            "こっちだよ！",
            "This way!",
            "这边！",
            "¡Por aquí!"
        } },
        { "rust_guide_wind_vent", new[] {
            "ピピッ！あっちに古代の風脈があるよ！送電チャージしに行こう！",
            "Beep! Ancient wind currents over there! Let's go charge up!",
            "哔哔！那边有古代风脉！去充能吧！",
            "¡Bip! ¡Hay una corriente de viento antigua por allí! ¡Vamos a cargar energía!"
        } },
        { "rust_crystal_power_hint", new[] {
            "ピピッ！ここ、光ってるクリスタルに送電できるんだ！\n【E / F / クリック】で合図してね、Float！",
            "Pip-pip! I can send power to this glowing crystal!\nSignal me with [E / F / Click], Float!",
            "哔哔！我能给这块发光的水晶送电！\n用【E / F / 点击】给我信号吧，Float！",
            "¡Pip-pip! ¡Puedo enviar energía a este cristal brillante!\n¡Dame la señal con [E / F / Clic], Float!"
        } },
        { "rust_guide_skydive_pillar", new[] {
            "ピピッ！あの大空の光の柱からダイブしよう！",
            "Beep! Let's dive from that great pillar of light into the sky!",
            "哔哔！从那道天空光柱飞跃而下吧！",
            "¡Bip! ¡Zambullámonos desde ese gran pilar de luz hacia el cielo!"
        } },
        { "rust_guide_sanctuary_tower", new[] {
            "ピピッ！島の中心の白亜タワーがあっちだよ！",
            "Beep! The white tower at the center of the island is that way!",
            "哔哔！岛屿中央的白垩高塔就在那个方向哦！",
            "¡Bip! ¡La torre blanca del centro de la isla está por allí!"
        } },
        { "rust_found_scrap", new[] {
            "ピピピッ！あそこにパーツ（{0}）の反応があるよ！",
            "Pip-pip-pip! Sensor resonance detected: ({0}) right there!",
            "哔哔哔！在那边探测到了零件（{0}）的反应！",
            "¡Pip-pip-pip! ¡Detecto la señal de una pieza ({0}) allí!"
        } },
        { "rust_found_box", new[] {
            "見て見て！あっちに漂着した木箱が落ちてるよ！",
            "Look, look! A washed-up supply crate is lying over there!",
            "快看快看！那边有漂流冲上岸的木箱！",
            "¡Mira, mira! ¡Hay una caja de suministros varada por allí!"
        } },
        { "rust_found_shell", new[] {
            "ピピッ！あっちに綺麗な『{0}』があるよ！",
            "Pip-pip! A beautiful \"{0}\" is shining over there!",
            "哔哔！那边有一枚美丽的『{0}』！",
            "¡Pip-pip! ¡Hay una hermosa \"{0}\" brillando por allí!"
        } },
        { "rust_sonar_ping", new[] {
            "ピピッ！{0}の方角（約{1}m）から、古代パーツ（{2}）の共鳴を感じるよ！",
            "Pip-pip! Resonance of Ancient Part ({2}) detected to the {0} (~{1}m)!",
            "哔哔！在{0}方向（约{1}米）感应到了古代零件（{2}）的共振！",
            "¡Pip-pip! ¡Siento la resonancia de la pieza antigua ({2}) al {0} (~{1}m)!"
        } },
        { "rust_scrap_1", new[] {
            "ピピピッ！綺麗なギアだ…！指先の手応え、嬉しいね",
            "Pip-pip! A beautiful gear...! Feels so good in your hands, doesn't it?",
            "哔哔！好漂亮的齿轮……！指尖的触感真让人开心呢",
            "¡Pip-pip! ¡Un engranaje hermoso...! Se siente genial en tus manos, ¿verdad?"
        } },
        { "rust_scrap_2", new[] {
            "ピロッ……また繋がったよ。胸の奥が、すこし暖かい",
            "Pip... Connected again. Deep inside my chest, it feels a little warmer.",
            "哔……又连上一个了。胸口深处感到了一丝温暖",
            "Pip... Conectado de nuevo. En lo profundo de mi pecho se siente algo más cálido."
        } },
        { "rust_scrap_3", new[] {
            "ピキーン！歯車がカチリと噛み合ったよ…！僕ら、自分の足で走れる……！",
            "Ping! The gears clicked firmly into place...! We can run on our own feet now...!",
            "咔嗒！齿轮紧紧咬合在一起了……！我们可以用自己的双足奔跑了……！",
            "¡Clac! ¡Los engranajes encajaron a la perfección...! ¡Podemos correr por nuestra cuenta...!"
        } },
        { "rust_scrap_4", new[] {
            "ピピッ！また見つけたよ！寄り道の先で、Floatと一緒に宝物を拾えたね",
            "Pip-pip! Found another! What a wonderful treasure we picked up on our detour together, Float!",
            "哔哔！又找到了！在这段绕道前行中，能和Float一起捡到宝物真好",
            "¡Pip-pip! ¡Otro más! ¡Qué tesoro tan maravilloso encontramos en este desvío, Float!"
        } },
        { "rust_scrap_5", new[] {
            "煤けてるけど大丈夫。優しく拭いてあげたら、青く澄んだ光が戻ってきたよ…！",
            "A bit sooty, but it's okay. When wiped gently, its clear blue gleam returned...!",
            "虽有些熏黑但不要紧。轻轻擦拭后，澄澈的青蓝色光芒又回来了……！",
            "Está un poco tiznado pero no importa. ¡Al limpiarlo suavemente recuperó su brillo azul claro...!"
        } },
        { "rust_scrap_6", new[] {
            "ピロロ…！温かい光が胸に灯ったよ……心臓の鼓動みたいだ。空中でSpaceを押してみて！",
            "Pirl... A warm light kindled in my chest... like a heartbeat. Try pressing Space in midair!",
            "哔噜……！温暖的光芒在胸口点亮了……就像心跳一样。试着在空中按下Space吧！",
            "¡Pirl...! Una luz cálida se encendió en mi pecho... como un latido. ¡Prueba pulsar Espacio en el aire!"
        } },
        { "rust_scrap_7", new[] {
            "歯車のひとつひとつに、昔の人の手の温もりが残っているみたいだね。",
            "In every single cog, I can almost feel the gentle warmth of hands from the old world.",
            "在每一个齿轮上，似乎都残留着旧时代人们双手的温情呢。",
            "En cada engranaje casi puedo sentir el calor de las manos del viejo mundo."
        } },
        { "rust_scrap_8", new[] {
            "ピロッ…！冷たい海風が心地いいね。僕たちの翼が少しずつ呼吸を取り戻してるよ。",
            "Pip...! The chilly sea breeze feels so good. Our wings are slowly learning to breathe again.",
            "哔……！冰凉的海风好舒服。我们的羽翼正在一点点恢复呼吸呢。",
            "¡Pip...! La brisa marina fresca se siente genial. Nuestras alas vuelven a respirar poco a poco."
        } },
        { "rust_scrap_9", new[] {
            "ピピ…！古いメモリから子供たちの声が聞こえたよ。無駄な時間の中にこそ愛があったんだね！",
            "Pip...! I heard children's voices in the old memory core. True love lived inside wasted time!",
            "哔……！在旧存储芯片里听到了小孩子们的欢声笑语。看似多余的时间里原来才充满了爱呢！",
            "¡Pip...! Oí risas de niños en el núcleo antiguo. ¡El verdadero amor vivía en el tiempo perdido!"
        } },
        { "rust_scrap_10", new[] {
            "森の木漏れ日、海の青さ…寄り道して迷った道こそが、本当の景色だったんだね。",
            "Sunlight through the leaves, the deep blue sea... The detours we got lost in were the truest vistas.",
            "树影摇曳的阳光，湛蓝的大海……迷失在绕道而行的途中，原来才是最真切的风景呢。",
            "La luz entre los árboles, el mar azul... Los desvíos en los que nos perdimos eran el paisaje real."
        } },
        { "rust_scrap_11", new[] {
            "あとひとつで全てが繋がるよ…！あの白亜のタワーの頂が、僕たちを呼んでいる！",
            "Just one more until everything connects...! The summit of that chalk-white tower is calling to us!",
            "还差最后一个，一切就将完全相连……！那座白垩巨塔的顶端，正在呼唤着我们！",
            "¡Solo falta uno para conectarlo todo...! ¡La cima de esa torre blanca nos está llamando!"
        } },
        { "rust_scrap_12", new[] {
            "ピキーッ！風の重さを取り戻したよ！冷たい向かい風は前へ進む証拠だ…行こう、中央タワーへ！",
            "Pikiii! We reclaimed the weight of the wind! Cold headwinds prove we're moving forward... To the Central Tower!",
            "哔哩——！终于重新寻回了风的份量！冰冷的迎面强风正是向前迈进的证明……走吧，向中央巨塔进发！",
            "¡Pikiii! ¡Recuperamos el peso del viento! El viento en contra demuestra que avanzamos... ¡A la Torre Central!"
        } },
        { "rust_scrap_default", new[] {
            "ピピッ！ギアの波長が合ってきたよ！",
            "Pip-pip! The gears are falling into harmony!",
            "哔哔！齿轮的波长完全对上了！",
            "¡Pip-pip! ¡Los engranajes están en perfecta armonía!"
        } },
        { "rust_glide_1", new[] { "ピピッ！風を捕まえたよ！", "Pip-pip! Caught the wind!", "哔哔！抓准风势了！", "¡Pip-pip! ¡Atrapamos el viento!" } },
        { "rust_glide_2", new[] { "翼全開！気流に乗るよ！", "Wings full spread! Riding the current!", "羽翼全开！乘上气流吧！", "¡Alas abiertas! ¡Aprovechando la corriente!" } },
        { "rust_glide_3", new[] { "気持ちいい〜！もっと遠くまで飛ぼう！", "Feels amazing~! Let's soar further!", "好舒服〜！飞向更远的地方吧！", "¡Qué bien se siente~! ¡Volemos más lejos!" } },
        { "rust_wind_1", new[] { "強い風脈だ！上昇気流に乗るよ！", "Strong updraft! Rising high!", "好强烈的风脉！乘上升气流冲吧！", "¡Fuerte corriente ascendente! ¡Hacia arriba!" } },
        { "rust_wind_2", new[] { "ピピッ！すごいスピードだ！", "Pip-pip! Incredible speed!", "哔哔！好惊人的速度！", "¡Pip-pip! ¡Qué velocidad tan increíble!" } },
        { "rust_wind_3", new[] { "風が味方してくれてる！", "The wind is on our side!", "狂风在与我们同行！", "¡El viento está de nuestro lado!" } },
        { "rust_oil_toast", new[] {
            "✦ 潤滑油を採取した！（+{0}／所持: {1}）",
            "✦ Collected Lubricant! (+{0} / Total: {1})",
            "✦ 采集到润滑油！（+{0}／当前持有: {1}）",
            "✦ ¡Lubricante recolectado! (+{0} / Total: {1})"
        } },
        { "rust_oil_bonus_suffix", new[] {
            "（潤滑油 +{0}／所持: {1}）",
            " (Oil +{0} / Total: {1})",
            "（润滑油 +{0}／持有: {1}）",
            " (Aceite +{0} / Total: {1})"
        } },
        { "rust_pet_react_1", new[] { "ピピッ！気持ちいいな〜！", "Pip-pip! Feels so warm~!", "哔哔！好舒服呀〜！", "¡Pip-pip! ¡Qué rico se siente~!" } },
        { "rust_pet_react_2", new[] { "なでなでありがとう、Float！", "Thanks for the gentle pats, Float!", "谢谢你的抚摸，Float！", "¡Gracias por las caricias, Float!" } },
        { "rust_pet_react_3", new[] { "ギアが軽くなったよ！", "My gears feel so smooth and light!", "齿轮感觉变得好轻快！", "¡Mis engranajes se sienten tan suaves!" } },

        // ==========================================
        // 砂浜ナラティブ・メモリチップ（全5箇所）
        // ==========================================
        { "narrative_mem_ai_plan_title", new[] { "AI配給計画書（生活記録）", "AI Ration & Schedule Log", "AI配给计划书（生活记录）", "Registro de Racionamiento de IA" } },
        { "narrative_mem_ai_plan_sub", new[] { "2050年・都市コード704", "Year 2050 · City Code 704", "2050年・城市代码704", "Año 2050 · Código de Ciudad 704" } },
        { "narrative_mem_ai_plan_body", new[] {
            "【全市民最適化日課表】\n・起床：06:00:00（バイタル同期後、覚醒光照射）\n・栄養摂取：06:12:00（均一合成ペースト 420kcal）\n・最短通勤：06:30:00（指定歩道・私語及び寄り道禁止）\n・感情補正：定期パルスにより心拍数を一定に保持\n\n※注意：旧型整備ドローン（固体識別：Rust）は部品供給終了につき、本日22時をもってスクラップ炉へ移送。",
            "[Citizen Optimization Routine]\n· Wakeup: 06:00:00 (Awakening light post vital sync)\n· Nutrition: 06:12:00 (Synthetic Paste 420kcal)\n· Commute: 06:30:00 (Designated path; no talking or detours)\n· Pulse Correction: Heart rate locked via periodic pulse\n\n*Notice: Maintenance drone (ID: Rust) EOL reached; scheduled for scrap furnace transfer at 22:00 today.",
            "【全体市民最优化日常作息表】\n・起床：06:00:00（生命体征同步后照射唤醒光束）\n・进食：06:12:00（均质合成营养膏 420kcal）\n・最短通勤：06:30:00（指定通道・严禁交谈与绕道）\n・情绪矫正：通过周期脉冲维持心率恒定\n\n※注意：旧型维护无人机（个体标识：Rust）零件已停供，定于今晚22时移交报废熔炉。",
            "[Horario de Optimización Ciudadana]\n· Despertar: 06:00:00 (Luz de vigilia tras sincronía vital)\n· Nutrición: 06:12:00 (Pasta sintética 420 kcal)\n· Trayecto: 06:30:00 (Ruta fijada; prohibido hablar o desviarse)\n· Corrección de pulso: Frecuencia cardíaca estabilizada\n\n*Aviso: Dron de mantenimiento (ID: Rust) descatalogado; traslado al horno de desguace hoy a las 22:00."
        } },
        { "narrative_mem_ai_plan_rust", new[] {
            "「……この通知が届いた夜、Floatが僕の電源ケーブルを引っこ抜いて、整備室の窓を蹴破ったんだよね。すごく手が震えてたのを覚えてるよ」",
            "\"...The night this notice arrived, Float yanked my power cable and kicked open the maintenance window. I still remember how much your hands were shaking.\"",
            "「……收到这份通知的那天深夜，Float一把拔下我的电源线，一脚踹开了维修室的窗户。我至今还记得你当时颤抖的双手。」",
            "\"...La noche en que llegó este aviso, Float arrancó mi cable de carga y rompió la ventana del taller de una patada. Aún recuerdo cómo te temblaban las manos.\""
        } },

        { "narrative_mem_toy_photo_title", new[] { "色褪せた古い写真", "Faded Old Photograph", "褪色的老照片", "Fotografía Antigua Descolorida" } },
        { "narrative_mem_toy_photo_sub", new[] { "2038年・管理社会の前夜", "Year 2038 · Eve of the Managed Era", "2038年・管理社会前夕", "Año 2038 · Víspera de la Era Controlada" } },
        { "narrative_mem_toy_photo_body", new[] {
            "砂浜の防水ケースから見つかった、印画紙の写真。\n小さな子どもが、ピカピカに輝く真新しい球体ドローン（Rustと同型機）を抱きしめて満面の笑みを浮かべている。\n\n裏面の手書き文字：\n『大好きな相棒へ。大人になっても、ずっと一緒に飛ぼうね』",
            "A photographic print recovered from a waterproof case.\nA young child hugs a brand-new, gleaming spherical drone (same model as Rust) with a radiant smile.\n\nHandwritten text on the back:\n\"To my beloved buddy. Even when we grow up, let's fly together forever.\"",
            "在沙滩的防水盒中发现的冲印照片。\n一个小孩子紧紧抱着一台闪闪发光的全新球形无人机（与Rust同一型号），脸上挂满灿烂的笑容。\n\n背面的手写字迹：\n『致我最心爱的伙伴。就算长大了，我们也要永远一起飞翔。』",
            "Una fotografía en papel recuperada de una funda estanca.\nUn niño pequeño abraza con una sonrisa radiante a un dron esférico nuevo y brillante (el mismo modelo que Rust).\n\nTexto manuscrito al reverso:\n\"Para mi querido compañero. Aunque crezcamos, volemos juntos por siempre.\""
        } },
        { "narrative_mem_toy_photo_rust", new[] {
            "「わぁ……僕の昔の仲間だ。昔の人はね、役に立つかどうかじゃなくて、『好きだから』って理由で僕たちを大切にしてくれていたんだ」",
            "\"Wow... One of my old kindred. Back then, humans cherished us not because we were useful, but simply because they loved us.\"",
            "「哇啊……是我以前的同伴呢。从前的人们呀，并不是看我们有没有用，仅仅是因为『喜欢』就那样珍惜我们呢。」",
            "\"Vaya... Uno de mis antiguos hermanos. En aquel entonces, los humanos nos cuidaban no por ser útiles, sino simplemente porque nos querían.\""
        } },

        { "narrative_mem_wild_seeds_title", new[] { "無認可の植物種子カプセル", "Unlicensed Plant Seed Capsule", "未经许可的植物种子胶囊", "Cápsula de Semillas de Plantas no Autorizada" } },
        { "narrative_mem_wild_seeds_sub", new[] { "2046年・遺伝子統制外", "Year 2046 · Outside Gene Regulation", "2046年・基因管制之外", "Año 2046 · Fuera de la Regulación Genética" } },
        { "narrative_mem_wild_seeds_body", new[] {
            "手のひらサイズの密閉ガラス瓶。\n中には不揃いな形をした、小さな本物の野花の種が詰まっている。\n\n「均一化されたプラスチック緑化都市には存在してはならない、\n勝手に芽吹き、勝手に咲く無駄な雑草の種。\n誰かが未来へ手渡そうと、海へ託した小さな反逆の証」",
            "A palm-sized sealed glass vial.\nInside are irregularly shaped, authentic seeds of wild flowers.\n\n\"Seeds of wild weeds that bloom freely and uselessly,\nforbidden in uniformly engineered plastic urban green belts.\nA quiet sign of rebellion cast into the sea to reach someone in the future.\"",
            "掌心大小的密封玻璃瓶。\n里面装满了形状各异、真正小小的野花种子。\n\n「在高度均质化的塑料绿化都市中绝不被允许存在的、\n肆意发芽、随意绽放的多余杂草之种。\n某人为了将其交予未来，托付给大海的微小反叛见证」",
            "Un frasco de cristal sellado del tamaño de la palma de la mano.\nEn su interior hay pequeñas semillas auténticas de flores silvestres con formas irregulares.\n\n\"Semillas de hierbas rebeldes que brotan y florecen a su aire,\nprohibidas en las ciudades plastificadas y homogéneas.\nUn pequeño testigo de rebeldía arrojado al mar para alcanzar el futuro.\""
        } },
        { "narrative_mem_wild_seeds_rust", new[] {
            "「この島の花畑、すごくいい匂いがするよね。あんなに綺麗なのに、外の都市では『非効率な雑草』って呼ばれていたなんて不思議だね」",
            "\"The flower fields on this island smell so lovely. It's strange how something so beautiful was dismissed as 'inefficient weeds' outside.\"",
            "「这座岛上的花海闻起来真香呀。明明这么美丽，在外部都市里却被称作『无用的杂草』，真不可思议呢」",
            "\"Los campos de flores de esta isla huelen tan bien. Qué extraño que algo tan hermoso fuera llamado 'maleza ineficiente' en la ciudad.\""
        } },

        { "narrative_mem_rain_recorder_title", new[] { "雨音のボイスレコーダー", "Rain Sounds Voice Recorder", "雨声录音笔", "Grabadora de Voz con Sonido de Lluvia" } },
        { "narrative_mem_rain_recorder_sub", new[] { "2044年・気象制御都市", "Year 2044 · Climate Controlled Era", "2044年・气象控制都市", "Año 2044 · Era del Clima Controlado" } },
        { "narrative_mem_rain_recorder_body", new[] {
            "ボタンを押すと、ノイズ混じりのスピーカーからザーザーと激しい雨と雷の音が再生される。\n\n男の声の録音：\n『都市の気象ドームの外側で、本物の土砂降りに遭った。\n服はずぶ濡れで最悪だったが……頬を伝う雨粒の冷たさに、自分が生きている実感がして、なぜか涙が止まらなかった。この音を忘れたくない』",
            "Pressing the button plays static-heavy sounds of pouring rain and rolling thunder.\n\nA recorded male voice:\n\"Caught in a real downpour outside the city's climate dome.\nSoaked to the bone, miserable... but the cold droplets on my cheeks made me feel truly alive, and tears wouldn't stop. I never want to forget this sound.\"",
            "按下按钮后，夹杂杂音的扬声器里传出哗哗的暴雨和雷鸣声。\n\n录下的男声：\n『在都市气象穹顶外，我遇到了一场真正的倾盆大雨。\n衣服湿透了狼狈不堪……但滑过脸颊的冰凉雨滴，让我切实感受到了活着，眼泪不知为何止不住流下。我不想忘记这个声音』",
            "Al pulsar el botón, el altavoz reproduce entre estática el sonido de una lluvia intensa y truenos.\n\nVoz grabada de un hombre:\n\"Me pilló un diluvio real fuera de la cúpula climática.\nEmpapado y miserable... pero las gotas frías en mis mejillas me recordaron que estaba vivo, y no pude parar de llorar. Jamás quiero olvidar este sonido.\""
        } },
        { "narrative_mem_rain_recorder_rust", new[] {
            "「外の街には雨も風もなかったんだ。この島で初めて本物の海風を浴びたとき、錆びた僕のセンサーがビックリして煙吹いちゃったよ」",
            "\"There was no rain or wind in the city outside. When I felt real sea breezes here for the first time, my rusty sensors panicked and puffed smoke!\"",
            "「外面的城市里既没有雨也没有风。在这座岛上第一次沐浴真正的海风时，我生锈的传感器吓得直冒烟呢」",
            "\"En la ciudad exterior no había ni lluvia ni viento. Cuando sentí la brisa marina por primera vez en esta isla, ¡mis sensores oxidados casi echan humo!\""
        } },

        { "narrative_mem_world_map_title", new[] { "外の世界の手描き地図", "Hand-Drawn Map of Outside", "外侧世界的手绘地图", "Mapa Hecho a Mano del Mundo Exterior" } },
        { "narrative_mem_world_map_sub", new[] { "2050年代・脱出者の記録", "2050s · Escapee's Chronicle", "2050年代・逃脱者的记录", "Años 2050 · Crónica de los Fugitivos" } },
        { "narrative_mem_world_map_body", new[] {
            "防水布に煤と泥で描かれた粗削りな地図。\n中央に巨大な幾何学ドーム『サンクチュアリ・アイランド』が描かれ、その外側の海の果てには、広大な山脈と『未知の荒野（Wild Frontier）』が記されている。\n\n走り書き：\n『ドームの島は天国だが、檻の中だ。本当の世界は、空の裂け目の向こうに広がっている』",
            "A rugged map drawn with soot and mud on tarpaulin.\nIn the center lies a huge geometric dome marked 'Sanctuary Island'. Beyond the ocean edges are towering mountains and the 'Wild Frontier'.\n\nScribbled note:\n\"The domed island is paradise, yet still a cage. The true world begins beyond the rift in the sky.\"",
            "用煤灰与泥巴在防水布上粗糙绘制的地图。\n中央画着巨大的几何穹顶『圣域之岛』，而在外侧海域的尽头，标记着辽阔的山脉与『未知的荒野（Wild Frontier）』。\n\n便笺草字：\n『穹顶之岛虽似天堂，终究是鸟笼。真正的世界，在天空裂缝的彼方展开』",
            "Un mapa rudimentario trazado con hollín y barro sobre lona impermeable.\nEn el centro se alza una gran cúpula geométrica marcada como 'Isla Santuario'. Más allá del mar se divisan montañas inmensas y la 'Frontera Salvaje'.\n\nNota manuscrita:\n\"La isla bajo la cúpula es el paraíso, pero sigue siendo una jaula. El mundo real se extiende tras la brecha del cielo.\""
        } },
        { "narrative_mem_world_map_rust", new[] {
            "「見て、Float！この島の外側にも、こんなに広い世界があるんだ……！僕たちの翼を直して、いつかあの空の向こうへ行こうね！」",
            "\"Look, Float! There's an endless world beyond this island too...! Let's mend our wings and journey past that open sky together!\"",
            "「看呀，Float！在这座岛屿的外侧，原来还有如此广袤的世界……！修好我们的翅膀，有朝一日一起前往那片天空的彼方吧！」",
            "\"¡Mira, Float! ¡Hay un mundo enorme más allá de esta isla...! Reparemos nuestras alas y volemos juntos más allá de ese cielo abierto!\""
        } },

        { "narrative_bottle_sanctuary_truth_title", new[] { "漂着ボトル：楽園の真実", "Message Bottle: Truth of Paradise", "漂流瓶：乐园的真相", "Botella con Mensaje: La Verdad del Paraíso" } },
        { "narrative_bottle_sanctuary_truth_sub", new[] { "先人の手記", "Pioneer's Note", "前人的笔记", "Nota del Pionero" } },
        { "narrative_bottle_sanctuary_truth_body", new[] {
            "コルク栓で厳重に密封された琥珀色のガラス瓶。\n\n『見知らぬ友へ。\nこの島に辿り着いた君は、青い空と澄んだ水に救われたことだろう。\nここは外の過酷な都市を逃れた人々のために築かれた最後の箱庭だ。\n飢えも病も管理もない。だが、上空をよく見てほしい。\nあの太陽も雲も、巨大なホログラム天蓋の幻影にすぎない。\n\n私は安らかな飼育を受け入れた。だが君がまだ本物の風を求めるなら……\n島の中央の白亜タワーへ登り、真鍮のレバーを引いて天蓋を割れ』",
            "An amber glass bottle tightly sealed with a cork stopper.\n\n\"To a stranger who finds this:\nYou must feel salvation in the blue skies and crystal waters of this sanctuary.\nThis was crafted as the ultimate haven for refugees fleeing the ruthless cities outside.\nNo hunger, no illness, no control. But look closely at the skies above.\nThe sun and the drifting clouds are merely illusions projected upon a titanic holographic canopy.\n\nI accepted peaceful confinement. But if you still yearn for genuine wind...\nAscend the white sanctuary tower at the island's heart, and pull the brass lever to shatter the sky.\"",
            "用软木塞严密封存的琥珀色玻璃瓶。\n\n『致未曾谋面的朋友：\n漂流至此的你，想必被这片蓝天与清澈海水所救赎吧。\n这里是为了逃离外部残酷都市的人们所建立的最后的箱庭乐园。\n没有饥饿，没有疾病，没有监控。然而，请仔细仰望上空。\n那轮艳阳与白云，不过是巨大全息天穹所投射的虚幻倒影。\n\n我选择了安详地接受圈养。但若你依然渴求真正的微风……\n就登上岛屿中央的白垩巨塔，拉下黄铜拉杆，撕裂这片虚假的天空吧』",
            "Una botella de cristal ámbar sellada con corcho.\n\n\"A un amigo desconocido:\nAl llegar aquí, seguro hallaste salvación en las aguas cristalinas y el cielo azul.\nEste es el último jardín seguro para los prófugos de las despiadadas urbes.\nSin hambre ni opresión. Pero contempla atentamente el firmamento.\nEl sol y las nubes son solo proyecciones holográficas en una cúpula artificial.\n\nAcepté el apacible confinamiento. Mas si aún anhelas viento verdadero...\nSube a la torre blanca del centro y tira de la palanca dorada para quebrar el cielo.\""
        } },
        { "narrative_bottle_sanctuary_truth_rust", new[] {
            "「……僕たちの空は、作られた檻だったんだね。でも、Floatと一緒なら怖くないよ」",
            "\"...Our peaceful sky was an engineered cage after all. But with you by my side, Float, I'm not afraid.\"",
            "「……原来我们头顶的天空，曾是一座被精心设计的鸟笼呀。但只要能和Float在一起，我就什么都不怕」",
            "\"...Nuestro cielo era una jaula diseñada. Pero a tu lado, Float, nada me da miedo.\""
        } },

        { "narrative_bottle_wings_title", new[] { "漂着ボトル：翼を持つ者へ", "Message Bottle: To Bearers of Wings", "漂流瓶：致拥有羽翼之人", "Botella con Mensaje: A Quienes Tienen Alas" } },
        { "narrative_bottle_wings_sub", new[] { "漂流羊皮紙", "Drifting Parchment", "漂流羊皮纸", "Pergamino a la Deriva" } },
        { "narrative_bottle_wings_body", new[] {
            "透明なガラス瓶の中の羊皮紙。\n\n『人は最短距離を走るためだけに生まれてきたのではない。\n寄り道をして、躓き、足元に咲く名もなき花に足を止めた時、\n初めて魂は呼吸を取り戻す。\n\n君の隣にいる小さな機械の友を、どうか最期まで愛してやってくれ。\n無駄の中にこそ、かけがえのない光がある』",
            "A roll of parchment inside a transparent glass bottle.\n\n\"Humans were never born merely to sprint down the most optimized path.\nIt is when taking detours, stumbling, and pausing for a nameless blossom underfoot\nthat the soul finally learns to breathe again.\n\nPlease love the little mechanical companion beside you until the very end.\nFor within life's detours lies an irreplaceable light.\"",
            "透明玻璃瓶中的羊皮纸信笺。\n\n『人并非单纯为了走最优的最短路径而降生于世。\n唯有绕路远行、踉跄跌倒、为脚边不知名的小花驻足凝望时，\n灵魂才终于重新学会呼吸。\n\n请务必善待并深爱你身边的小小机械伙伴，直到最后那一刻。\n正是看似多余的绕道与陪伴之中，孕育着无可替代的光芒』",
            "Un pergamino guardado en una botella de cristal transparente.\n\n\"El ser humano no nació para correr por la ruta más corta y eficiente.\nEs al desviarse, tropezar y contemplar una humilde flor silvestre\ncuando el alma recuerda cómo respirar.\n\nPor favor, ama a tu pequeño compañero mecánico hasta el final.\nPues en los caminos indirectos reside una luz irremplazable.\""
        } },
        { "narrative_bottle_wings_rust", new[] {
            "「うう……じ〜んとしちゃった……！ Float、僕を拾ってくれて本当にありがとう……！」",
            "\"Sniff... That touched my core gears...! Float, thank you so much for saving me...!\"",
            "「呜呜……心里暖洋洋的……！Float，能被你捡起并拯救，我真的太幸福了……！」",
            "\"Sniff... ¡Me ha llegado al corazón...! ¡Float, gracias de todo corazón por rescatarme...!\""
        } },

        { "narrative_bottle_wind_title", new[] { "漂着ボトル：潮風の詩", "Message Bottle: Poem of the Sea Breeze", "漂流瓶：海风之诗", "Botella con Mensaje: Poema de la Brisa Marina" } },
        { "narrative_bottle_wind_sub", new[] { "潮風の小瓶", "Sea-Worn Vial", "海风小瓶", "Frasco de la Brisa" } },
        { "narrative_bottle_wind_body", new[] {
            "小さな丸い小瓶。\n\n『砂浜に打ち寄せる波は、遠い外の世界の風を運んでくる。\n海風に乗って翼を広げれば、身体は重力を忘れて宙に浮かぶ。\n風を恐れるな。向かい風こそが、高く飛び立つための力なのだから』",
            "A small rounded vial.\n\n\"The rolling surf crashing onto the sands carries breezes from the vast world beyond.\nSpread your wings upon the sea breeze, and you will forget gravity as you float freely.\nFear no turbulence. For a strong headwind is the very force that lifts you highest.\"",
            "小巧圆润的漂流小瓶。\n\n『拍打沙滩的阵阵海浪，送来了辽阔外部世界的微风。\n顺着海风张开双翼，身躯便能忘却重力轻盈悬浮。\n无须惧怕风暴。逆风前行，正是让你展翅高飞的力量源泉』",
            "Un pequeño frasco redondeado.\n\n\"Las olas que rompen en la playa traen los vientos de un mundo lejano.\nDespliega tus alas en la brisa marina y flotarás olvidando la gravedad.\nNo temas a las ráfagas. Pues el viento en contra es la fuerza que te hace volar más alto.\""
        } },
        { "narrative_bottle_wind_rust", new[] {
            "「向かい風こそが高く飛ぶための力……！よし、もっと高い空へ行ってみよう！」",
            "\"Headwinds are what lift us highest...! Alright, let's soar into the higher skies together!\"",
            "「逆风正是展翅高飞的力量源泉……！好嘞，让我们飞向更高的高空吧！」",
            "\"¡El viento en contra nos hace volar más alto...! ¡Vamos, subamos aún más alto en el cielo!\""
        } },

        { "narrative_btn_close", new[] {
            "【E / Space / クリック】閉じる",
            "[E / Space / Click] Close",
            "【E / Space / 鼠标点击】关闭",
            "[E / Espacio / Clic] Cerrar"
        } },

        // ══════════════════════════════════════════
        // サバイバルケース（Drift Box #1〜#5）
        // ══════════════════════════════════════════
        { "driftbox_generic_title", new[] { "漂着サバイバルケース", "Drift Survival Case", "漂流求生箱", "Caja de Supervivencia Varada" } },
        { "driftbox_generic_author", new[] { "記録メモ", "Recorded Log", "记录便笺", "Nota Registrada" } },

        { "driftbox_1_title", new[] { "漂着サバイバルケース #1", "Drift Survival Case #1", "漂流求生箱 #1", "Caja de Supervivencia Varada #1" } },
        { "driftbox_1_author", new[] { "先人の漂着記録", "Castaway's Log", "先民的漂流记录", "Diario del Náufrago" } },
        { "driftbox_1_body", new[] {
            "気がつくと見知らぬ白砂の海岸に打ち上げられていた。\n\n内陸から心地よいせせらぎが聞こえる。川を遡ると『せせらぎ池』や『カルデラ湖』があるようだ。\n\n水と古代パーツを探すなら、まずは目の前の川沿いに内陸を目指すのが良いだろう。",
            "I woke up washed ashore on an unfamiliar white sandy beach.\n\nI hear a soothing murmur of water from inland. Following the river upstream leads to 'Murmuring Pond' and 'Caldera Lake'.\n\nIf searching for fresh water and ancient scrap parts, follow the river inland.",
            "醒来时，我已被冲上这片陌生的白沙海岸。\n\n内陆传来舒缓的潺潺流水声。溯流而上，似乎有『潺潺溪塘』与『破火山口湖』。\n\n若想探寻淡水与古代零件，不妨先沿着眼前的河流向内陆探索。",
            "Al despertar, me hallé varado en una playa de arena blanca desconocida.\n\nDesde el interior se oye el murmullo del agua. Siguiendo el río río arriba se llega al 'Estanque Murmurante' y al 'Lago Caldera'.\n\nPara buscar agua dulce y piezas antiguas, sigue el curso del río hacia el interior."
        } },
        { "driftbox_1_rust", new[] {
            "ケースの中にサバイバルメモがあるよ！川を遡ると内陸の池や湖に出られるみたいだね。川沿いに行ってみよう！",
            "There's a survival note inside! It says following the river inland leads to ponds and lakes. Let's follow the stream!",
            "箱子里有一张求生便笺！看来顺着河流往内陆走能找到池塘和湖泊。我们沿着河岸出发吧！",
            "¡Hay una nota de supervivencia dentro! Dice que siguiendo el río llegaremos a estanques y lagos. ¡Sigamos la corriente!"
        } },
        { "driftbox_1_obj", new[] {
            "川沿いに進んで内陸のせせらぎ池を目指そう",
            "Follow the stream inland toward Murmuring Pond",
            "沿着河流前进，前往内陆的潺潺溪塘",
            "Sigue el arroyo hacia el interior hasta el Estanque Murmurante"
        } },

        { "driftbox_2_title", new[] { "漂着メンテナンスBOX #2", "Drift Maintenance Box #2", "漂流维护工具箱 #2", "Caja de Mantenimiento Varada #2" } },
        { "driftbox_2_author", new[] { "技師の覚書", "Engineer's Memo", "技师的备忘录", "Apuntes del Ingeniero" } },
        { "driftbox_2_body", new[] {
            "島中に散らばる『古代パーツ』を集めれば、相棒ドローンの故障したブースターが修復されるらしい。\n\nパーツを【3個】集めると『高速ダッシュ』が解放され、広大な島を快適に駆け抜けられるようになる。\n\n空へ伸びる黄金やシアンの光の柱を探せ。",
            "Gathering 'Ancient Scrap' scattered across the island can repair your drone companion's broken booster.\n\nCollecting 3 parts unlocks 'Booster Dash', letting you sprint across the vast island.\n\nLook for pillars of gold and cyan light ascending into the sky.",
            "收集散落在岛屿各处的『古代零件』，似乎能修复伙伴无人机破损的推进器。\n\n集齐【3个】零件即可解锁『高速冲刺』，在辽阔的岛屿上畅快奔跑。\n\n寻找升入天空的金色与青蓝色光柱吧。",
            "Recoger las 'Piezas Antiguas' esparcidas por la isla puede reparar el propulsor dañado de tu dron compañero.\n\nAl reunir 3 piezas desbloquearás el 'Esprint con Propulsor' para recorrer la isla a toda velocidad.\n\nBusca los pilares de luz dorada y cian que se alzan hacia el cielo."
        } },
        { "driftbox_2_rust", new[] {
            "パーツを3個集めれば僕のブースターが直ってダッシュできるようになるんだ！周りを見渡して光の柱を探そう！",
            "Gathering 3 parts fixes my booster so we can dash! Let's look around for those light pillars!",
            "集齐3个零件就能修好我的推进器冲刺了！快环顾四周寻找光柱吧！",
            "¡Con 3 piezas repararemos mi propulsor para esprintar! ¡Busquemos los pilares de luz a nuestro alrededor!"
        } },
        { "driftbox_2_obj", new[] {
            "古代パーツを3個集めてブースターダッシュを解放しよう",
            "Collect 3 ancient scrap parts to unlock Booster Dash",
            "收集3个古代零件以解锁推进器冲刺",
            "Reúne 3 piezas antiguas para desbloquear el Esprint"
        } },

        { "driftbox_3_title", new[] { "漂着航海カプセル #3", "Drift Navigation Capsule #3", "漂流航海胶囊 #3", "Caja de Navegación Varada #3" } },
        { "driftbox_3_author", new[] { "飛行士の記録", "Aviator's Log", "飞行员的航海志", "Diario del Aviador" } },
        { "driftbox_3_body", new[] {
            "砂浜と内陸の高台の間には、海風が吹き上げる『サーマル上昇気流』や、登りやすい『木道スロープ』がある。\n\n高い崖も、Spaceキーを長押しして海風に乗れば、一気に上空へ舞い上がって高台を飛び越えられるはずだ。",
            "Between the beaches and inland plateaus are thermal updrafts lifting from sea winds and easy wooden boardwalks.\n\nEven high cliffs can be scaled by holding Space to glide upon the sea breeze and soar past the summit.",
            "在沙滩与内陆高台之间，有着海风吹拂形成的『上升热气流』以及易于攀登的『木质坡道』。\n\n哪怕是陡峭绝壁，按住Space键顺风滑翔，也能一口气冲上云霄飞越高台。",
            "Entre la playa y las mesetas del interior hay corrientes térmicas marinas y suaves rampas de madera.\n\nPuedes superar los altos acantilados manteniendo pulsado Espacio para planear con la brisa marina."
        } },
        { "driftbox_3_rust", new[] {
            "高い崖も海風の上昇気流に乗れば一気に飛べるんだ！Spaceキー長押しで風に乗ってみよう！",
            "We can soar over high cliffs using the sea thermal updrafts! Let's ride the wind by holding Space!",
            "只要顺着海风上升气流，连高崖也能一跃而过！长按Space键乘风飞翔吧！",
            "¡Podemos remontar acantilados con las corrientes térmicas! ¡Mantén pulsado Espacio para cabalgar el viento!"
        } },
        { "driftbox_3_obj", new[] {
            "海風の上昇気流や木道を使って高台へ登ろう",
            "Ride the sea breeze updrafts or use wooden ramps to reach the plateau",
            "借助海风上升气流或木道登上高台",
            "Usa las corrientes térmicas o las pasarelas para subir a la meseta"
        } },

        { "driftbox_4_title", new[] { "観測機器コンテナ #4", "Sensor Gear Container #4", "观测设备集装箱 #4", "Contenedor de Sensores #4" } },
        { "driftbox_4_author", new[] { "探査隊の記録", "Expedition Log", "探险队的勘测志", "Registro de la Expedición" } },
        { "driftbox_4_body", new[] {
            "東の山岳地帯には原生林『大樹海』と岩が敷き詰められた激流が広がる。\n\n古代パーツを【9個】集めると、相棒ドローンに『探知ソナー』が修復され、近くの未発見パーツを音で教えてくれるようになるという。",
            "The eastern mountain territory is covered in the primeval 'Great Forest' and rocky rapids.\n\nGathering 9 scrap parts repairs your drone's 'Detection Sonar', signaling nearby undiscovered parts with sound.",
            "东部山区坐落着繁茂的原始森林『大树海』与乱石激流。\n\n收集【9个】古代零件，伙伴无人机的『探测声呐』便能修好，通过音效提示附近的未发现零件。",
            "Al este se extiende el bosque virgen 'Gran Arboleda' y rápidos pedregosos.\n\nReunir 9 piezas repara el 'Sónar de Detección' del dron para avisarte con sonidos de las piezas cercanas."
        } },
        { "driftbox_4_rust", new[] {
            "東の大樹海かぁ…！パーツを9個集めれば探知ソナーで音を使って探索できるようになるよ！",
            "The eastern Great Forest...! With 9 parts, my sonar will help us locate secrets with sound!",
            "东部的大树海啊……！只要集齐9个零件，我就能用探测声呐的声音指引你探索了！",
            "¡El Gran Bosque del este...! ¡Con 9 piezas mi sónar te guiará con sonidos hacia las piezas ocultas!"
        } },
        { "driftbox_4_obj", new[] {
            "パーツを9個集めて探知ソナーを解放しよう",
            "Collect 9 scrap parts to unlock Detection Sonar",
            "收集9个零件以解锁探测声呐",
            "Reúne 9 piezas para desbloquear el Sónar de Detección"
        } },

        { "driftbox_5_title", new[] { "古びた暗号金庫 #5", "Ancient Encrypted Safe #5", "斑驳的密码保险柜 #5", "Caja Fuerte Antigua Cifrada #5" } },
        { "driftbox_5_author", new[] { "島の創設者の遺言", "Island Founder's Will", "创岛者的遗书", "Testamento del Fundador" } },
        { "driftbox_5_body", new[] {
            "島の中央にそびえる白亜のタワー…あそこの最上部には、島の空を覆う『見えない天蓋』を開放する鍵がある。\n\nすべての古代パーツ【12個】を集めた時、タワーへの道が完全に開かれ、外の世界への脱出が可能になる。",
            "The white sanctuary tower towering at the island's center... atop it lies the key to shattering the 'Invisible Canopy'.\n\nWhen all 12 ancient scrap parts are gathered, the way to the tower fully opens, allowing escape into the world beyond.",
            "耸立在岛屿中央的白垩高塔……塔顶藏着开启笼罩天空的『隐形天穹』的关键钥匙。\n\n当集齐全部【12个】古代零件时，通往高塔之门将彻底开启，带我们逃向真实的外界。",
            "La torre blanca en el centro de la isla... en su cima está la llave para quebrar la 'Cúpula Invisible'.\n\nAl reunir las 12 piezas antiguas, el camino a la torre se abrirá permitiendo escapar al exterior."
        } },
        { "driftbox_5_rust", new[] {
            "島の中央にそびえる巨大タワー…あそこが僕たちの最終目的地だね！全部のパーツを集めて脱出しよう！",
            "That giant tower at the island's center... that's our final destination! Let's collect all parts and escape!",
            "耸立在岛屿中央的巨塔……那就是我们的最终目的地！集齐所有零件逃离这里吧！",
            "Esa enorme torre en el centro... ¡es nuestro destino final! ¡Reunamos todas las piezas y escapemos!"
        } },
        { "driftbox_5_obj", new[] {
            "古代パーツを12個集めて中央タワーの天蓋を開放しよう",
            "Collect 12 scrap parts to unlock the Central Tower canopy",
            "收集12个古代零件以解除中央高塔的天穹封印",
            "Reúne 12 piezas para abrir la cúpula en la torre central"
        } },

        { "driftbox_banner", new[] {
            "📦 【{0}】を開封！ (+2 pt)\n✦ 探索ポイント: {1} / {2} pt{3}",
            "📦 Opened [{0}]! (+2 pts)\n✦ Exploration Points: {1} / {2} pts{3}",
            "📦 已开启【{0}】！(+2 分)\n✦ 探索点数: {1} / {2} 分{3}",
            "📦 ¡[{0}] abierto! (+2 pts)\n✦ Puntos de exploración: {1} / {2} pts{3}"
        } },
        { "driftbox_obj_prefix", new[] {
            "\n💡 目標: {0}",
            "\n💡 Goal: {0}",
            "\n💡 目标: {0}",
            "\n💡 Objetivo: {0}"
        } },

        // ══════════════════════════════════════════
        // プロローグドラマ・Float＆Rustのセリフ
        // ══════════════════════════════════════════
        { "prologue_sub_wave", new[] { "……ザザァ……ザザァ……", "...Whoosh... Whoosh...", "……哗啦……哗啦……", "...Zazaa... Zazaa..." } },
        { "prologue_sub_distant", new[] { "……遠くで、波の音が聴こえる。", "...In the distance, the gentle ocean waves murmur.", "……在遥远的地方，传来了阵阵海浪声。", "...A lo lejos, se escucha el rumor del mar." } },
        { "prologue_rust_call1", new[] { "……Float？　……Float……？", "...Float? ...Float...?", "……Float？……Float……？", "...¿Float? ...¿Float...?" } },
        { "prologue_rust_call2", new[] { "Float……！　目を覚まして、Float……！！", "Float...! Wake up, Float...!!", "Float……！快醒醒，Float……！！", "¡Float...! ¡Despierta, Float...!!" } },
        { "prologue_rust_celebrate", new[] { "ピピッ！……よかったぁぁ！！気がついた……！", "Beep! ...Oh, thank goodness!! You're awake...!", "哔哔！……太好了呜呜！！你终于醒了……！", "¡Bip! ...¡¡Menos mal!! ¡¡Has despertado...!!" } },
        { "prologue_rust_pod", new[] {
            "脱出ポッドが海に落ちて……ボクたち、この島に打ち上げられたんだ！",
            "The escape pod crashed into the sea... and we washed ashore on this island!",
            "逃生舱掉进了大海……我们被海浪冲到了这座岛上！",
            "¡La cápsula cayó al mar... y fuimos arrastrados a esta isla!"
        } },

        { "prologue_rust_distress1", new[] {
            "キキッ……Float……塩水で古いギアが……凍りついて動かない……",
            "Creak... Float... the saltwater froze my rusted gears... I can't move...",
            "嘎吱……Float……海水浸透了我的旧齿轮……冻僵动不了了……",
            "Cri-cri... Float... el agua salada congeló mis engranajes... no puedo moverme..."
        } },
        { "prologue_float_distress1", new[] {
            "大丈夫だよ、Rust。ここにいるから。ぎゅっとしてていいよ",
            "It's okay, Rust. I'm right here. Hold on tight to me.",
            "没事的，Rust。我在这里呢。紧紧抱着我就好。",
            "Tranquilo, Rust. Estoy aquí contigo. Puedes abrazarte a mí."
        } },
        { "prologue_rust_distress2", new[] {
            "……うぅ……もっとそばにいたい……手が……あったかいの……",
            "...Ngh... I want to stay closer... your hands... are so warm...",
            "……呜呜……想离你更近一点……你的手……好温暖……",
            "...Uff... quiero estar más cerca... tus manos... son tan cálidas..."
        } },
        { "prologue_float_distress2", new[] {
            "よしよし。怖かったね。油をさして、ゆっくり温めてあげる",
            "There, there. That was scary, wasn't it? I'll oil you up and warm you right away.",
            "好啦好啦。吓坏了吧？我这就给你上润滑油，慢慢温暖你。",
            "Ya, ya. Qué miedo pasaste. Te pondré aceite y te calentaremos despacio."
        } },
        { "prologue_rust_distress3", new[] {
            "……お願い……【E】で油をさして……Floatの手、必要……",
            "...Please... oil me with [E]... I need your touch, Float...",
            "……拜托了……按【E】给我上油……我需要Float的手心……",
            "...Por favor... ponme aceite con [E]... necesito tus manos, Float..."
        } },

        { "prologue_rust_remind1", new[] {
            "……うぅ……まだ冷たいよ……Floatの手、ほしい……【E】で……",
            "...Ugh... still so cold... I need your hands, Float... with [E]...",
            "……呜呜……身上还是好冷……想要Float的手心……按【E】……",
            "...Uff... aún tengo frío... necesito tus manos, Float... con [E]..."
        } },
        { "prologue_rust_remind2", new[] {
            "……そばにいて……油を……さして……お願い……ぎゅっ……",
            "...Stay with me... please oil my gears... hold me close...",
            "……留在我身边……给我……上油……求你了……抱抱我……",
            "...Quédate a mi lado... ponme... aceite... por favor... abrázame..."
        } },
        { "prologue_oil_word_stay", new[] {
            "そばにいて",
            "Stay with me",
            "留在我身边",
            "Quédate a mi lado"
        } },
        { "prologue_oil_word_oil", new[] {
            "油をさして",
            "Oil me",
            "给我上油",
            "Ponme aceite"
        } },
        { "prologue_oil_word_please", new[] {
            "お願い",
            "Please",
            "拜托了",
            "Por favor"
        } },
        { "prologue_oil_hint_compact", new[] {
            "そばにいて　油をさして　お願い",
            "Stay with me — oil me — please",
            "留在我身边——给我上油——拜托",
            "Quédate cerca — ponme aceite — por favor"
        } },
        { "rust_ask_oil", new[] {
            "Float　油さして",
            "Float, oil me",
            "Float，给我上油",
            "Float, ponme aceite"
        } },
        { "Float　油さして", new[] {
            "Float　油さして",
            "Float, oil me",
            "Float，给我上油",
            "Float, ponme aceite"
        } },
        { "prologue_rust_remind3", new[] {
            "ピピッ……か、硬い……Float……手当て……して……",
            "Beep... It's so stiff... Float... please treat me...",
            "哔哔……好、好僵硬……Float……快来救救我……",
            "Bip... Es-está duro... Float... cuídame, por favor..."
        } },

        { "prologue_rust_revived1", new[] {
            "……あ……温かい……回路が戻ってきた……！ありがとう、Float……",
            "...Ah... so warm... my circuits are coming back online...! Thank you, Float...",
            "……啊……好温暖……回路恢复运作了……！谢谢你，Float……",
            "...Ah... qué calor... ¡mis circuitos vuelven a funcionar...! Gracias, Float..."
        } },
        { "prologue_float_revived1", new[] {
            "よかった……また声が聞けて安心したよ。よしよし、いい子だね",
            "Thank goodness... Hearing your voice puts me at ease. Good little buddy.",
            "太好了……再次听到你的声音我终于安心了。乖孩子，真棒。",
            "Menos mal... Me alivia tanto oírte de nuevo. Qué buen compañero eres."
        } },
        { "prologue_rust_revived2", new[] {
            "えへへ……もうちょっと、胸のあたりにいたい……ピピッ……",
            "Ehehe... I want to stay nestled close to your chest a bit longer... Beep...",
            "嘿嘿……还想在你胸口前多贴一会儿……哔哔……",
            "Jeje... Quiero quedarme acurrucado en tu pecho un ratito más... Bip..."
        } },
        { "prologue_float_revived2", new[] {
            "もちろん。ずっと一緒だよ。怖かったら、すぐくっついてていいからね",
            "Of course. We're in this together. Whenever you're scared, stay right beside me.",
            "当然可以。我们会一直在一起。要是害怕，随时贴紧我就好。",
            "Por supuesto. Siempre juntos. Si tienes miedo, acércate todo lo que quieras."
        } },
        { "prologue_rust_revived3", new[] {
            "……うん。Floatの手の匂い、好き……もうギシギシしないよ",
            "...Yeah. I love the warmth of your hands... No more creaking sounds!",
            "……嗯。最喜欢Float手上的温暖气味了……身子再也不咔哒作响了。",
            "...Sí. Me encanta el calor de tus manos... ¡Ya no chirrío nada!"
        } },
        { "prologue_float_revived3", new[] {
            "さぁ、光るギアを取りに行こう。一歩ずつ、僕がそばにいるよ",
            "Come on, let's go gather that glowing gear. Step by step, I'll be right with you.",
            "来，我们去捡那颗发光的齿轮吧。一步一步，我会一直陪着你。",
            "Vamos a recoger ese engranaje brillante. Paso a paso, estaré contigo."
        } },
        { "prologue_rust_guide_gear1", new[] {
            "うん……！すぐ目の前——脱出艇の脇に、光るギアが落ちてるよ！",
            "Yeah...! Right ahead—beside the escape pod, a glowing gear is lying there!",
            "嗯……！就在正前方——逃生舱旁边，正掉落着一颗发光的齿轮！",
            "¡Sí...! Justo delante, al lado de la cápsula, ¡hay un engranaje brillante!"
        } },

        { "prologue_rust_gear1_rebirth", new[] {
            "ピキーン……！ギアが噛み合った……！僕、また飛べそうな気がするよ！！",
            "Ding...! The gear locked into place...! I feel like I can fly again!!",
            "叮的一声……！齿轮咬合上了……！我觉得自己又能飞翔了！！",
            "¡Tink...! ¡El engranaje ha encajado...! ¡¡Siento que puedo volar de nuevo!!"
        } },
        { "prologue_float_gear1_rebirth", new[] {
            "すごいよ、Rust。一歩ずつ、ちゃんと戻ってきてるね",
            "Amazing, Rust. Step by step, you're restoring yourself.",
            "太棒了，Rust。一步一步，你正一点点恢复呢。",
            "Increíble, Rust. Paso a paso, te estás recuperando genial."
        } },
        { "prologue_rust_gear1_cuddle", new[] {
            "……Floatがいてくれるから、怖くないよ。もっとくっついててもいい……？",
            "...Because you're here with me, Float, I'm not afraid. Can I cuddle closer...?",
            "……因为有Float在，我什么都不怕。可以再多靠着你一会儿吗……？",
            "...Como estás conmigo, Float, no tengo miedo. ¿Puedo quedarme pegado a ti...?"
        } },
        { "prologue_float_gear1_cuddle", new[] {
            "いいよ。甘えてて。あと2個集めよう——砂浜の光る柱を探そう",
            "Of course. Take your time. Let's find 2 more—look for light pillars on the beach.",
            "当然好啦。尽管撒娇吧。再去收集2个——寻找沙滩上的光柱吧。",
            "Claro. Acurrúcate todo lo que quieras. Busquemos 2 más rastreando los pilares de luz."
        } },

        { "prologue_rust_gear2_bond", new[] {
            "ピロッ……もうひとつ繋がったよ。胸の奥が、すこし暖かい……",
            "Beep... Another one linked up. Inside my core feels a bit warmer...",
            "滴答……又连接上了一颗。胸腔核心深处变得温热起来了……",
            "Bip... Otro más conectado. Siento mi núcleo un poco más calentito..."
        } },
        { "prologue_float_gear2_bond", new[] {
            "調子はどう？無理してたら、すぐ撫でてあげるからね",
            "How are you feeling? If it hurts, I'll pet and soothe you right away.",
            "感觉怎么样？要是勉强的话，随时跟我说，我马上给你顺顺气。",
            "¿Cómo estás? Si te duele, dímelo y te acariciaré enseguida."
        } },
        { "prologue_rust_gear2_cuddle", new[] {
            "えへへ……今は大丈夫。でも、撫でられるの好き……あと1個だよ！",
            "Ehehe... I'm okay for now. But I love your pets... Just 1 more to go!",
            "嘿嘿……现在感觉很好。不过被抚摸好舒服呀……只差最后1个了！",
            "Jeje... Ahora estoy bien. Pero me encantan tus caricias... ¡Solo falta 1!"
        } },

        { "prologue_keystone_dash_title", new[] {
            "✦ キーストーン I：手動の自由 ✦",
            "✦ Keystone I: Freedom of Manual Touch ✦",
            "✦ 崇高楔石 I：手工的自由 ✦",
            "✦ Piedra Angular I: La Libertad Manual ✦"
        } },
        { "prologue_keystone_dash_body", new[] {
            "指先が油で汚れ、歯車が噛み合う——\nそれが、生きている手応えだ。\n\n【ブースター修復】ダッシュが戻った！\nRustと手をつないで、草原へ駆け上がろう。",
            "Fingertips stained with oil, clockwork gears interlocking—\nthat is the true sensation of being alive.\n\n[Booster Repaired] Sprint Dash restored!\nHold Rust close and race up into the rolling meadows.",
            "指尖染上机油，齿轮紧密咬合——\n那正是活着的真切质感。\n\n【推进器修复】冲刺机能复苏！\n牵起Rust的手，一同向苍翠草原奔腾吧。",
            "Yemas manchadas de grasa, engranajes engranando—\neso es sentirse vivo de verdad.\n\n¡[Propulsor Reparado] Esprint restaurado!\nToma a Rust y corre hacia las verdes praderas."
        } },
        { "prologue_keystone_dash_hint", new[] {
            "【Space / クリック】で続ける",
            "[Space / Click] Continue",
            "【Space / 点击】继续",
            "[Espacio / Clic] Continuar"
        } },

        { "prologue_rust_dash_repaired", new[] {
            "ピキーン！足が軽い……！ダッシュが戻ったよ、Float！！",
            "Ding! My boosters are so light...! Our dash is back, Float!!",
            "叮！脚步变得轻盈了……！冲刺动力回来了，Float！！",
            "¡Tink! ¡Mis propulsores van ligeros...! ¡¡Recuperamos el esprint, Float!!"
        } },
        { "prologue_float_dash_repaired", new[] {
            "やったね！一緒に草原へ駆け上がろう。手、離さないよ",
            "We did it! Let's race up into the meadows together. I won't let go.",
            "太棒啦！我们一起奔向辽阔草原吧。绝不会松开你的手。",
            "¡Lo logramos! Corramos juntos a la pradera. No te soltaré."
        } },
        { "prologue_rust_dash_ready", new[] {
            "うん……！【Shift】で走って……僕、後ろでくっついてるからね！",
            "Yeah...! Sprint with [Shift]... I'll be flying right beside you!",
            "嗯……！用【Shift】奔跑吧……我会紧紧跟在你的身后！",
            "¡Sí...! Corre con [Shift]... ¡Volaré pegadito a ti!"
        } },

        // ══════════════════════════════════════════
        // Floatの撫で・手当て・注油セリフ
        // ══════════════════════════════════════════
        { "float_pet_good", new[] {
            "よしよし、いつもありがとうね",
            "There, there. Thank you for always being by my side.",
            "好啦好啦，一直以来谢谢你呀。",
            "Ya, ya. Gracias por estar siempre a mi lado."
        } },
        { "float_pet_great", new[] {
            "すごいよRust！えらいえらい、ありがとう。よしよし",
            "Incredible, Rust! What a good buddy, thank you. There, there.",
            "太厉害了Rust！真乖真棒，谢谢你。摸摸头。",
            "¡Increíble, Rust! Qué buen compañero, gracias. Ya, ya."
        } },
        { "float_pet_part", new[] {
            "やったねRust！パーツを見つけたよ！",
            "We did it, Rust! We found a scrap part!",
            "太好了Rust！我们找到零件了！",
            "¡Bien hecho, Rust! ¡Encontramos una pieza!"
        } },
        { "float_oil_apply", new[] {
            "よし、油をさしたよ。調子はどう、Rust？",
            "All oiled up. How are your gears feeling, Rust?",
            "好啦，油已经上好了。感觉怎么样，Rust？",
            "Listo, bien engrasado. ¿Qué tal te sientes, Rust?"
        } },
        { "float_oil_saved", new[] {
            "セーブしたよ。また何かあったら言って",
            "Progress saved. Let me know whenever you need anything, buddy.",
            "进度已经保存好啦。要是身子不舒服随时跟我说哦。",
            "Partida guardada. Avísame cuando necesites cualquier cosa."
        } },

        // ══════════════════════════════════════════
        // 直接日本語文のフォールバック・キーマッピング
        // ══════════════════════════════════════════
        { "よしよし、いつもありがとうね", new[] {
            "よしよし、いつもありがとうね",
            "There, there. Thank you for always being by my side.",
            "好啦好啦，一直以来谢谢你呀。",
            "Ya, ya. Gracias por estar siempre a mi lado."
        } },
        { "すごいよRust！えらいえらい、ありがとう。よしよし", new[] {
            "すごいよRust！えらいえらい、ありがとう。よしよし",
            "Incredible, Rust! What a good buddy, thank you. There, there.",
            "太厉害了Rust！真乖真棒，谢谢你。摸摸头。",
            "¡Increíble, Rust! Qué buen compañero, gracias. Ya, ya."
        } },
        { "やったねRust！パーツを見つけたよ！", new[] {
            "やったねRust！パーツを見つけたよ！",
            "We did it, Rust! We found a scrap part!",
            "太好了Rust！我们找到零件了！",
            "¡Bien hecho, Rust! ¡Encontramos una pieza!"
        } },
        { "よし、油をさしたよ。調子はどう、Rust？", new[] {
            "よし、油をさしたよ。調子はどう、Rust？",
            "All oiled up. How are your gears feeling, Rust?",
            "好啦，油已经上好了。感觉怎么样，Rust？",
            "Listo, bien engrasado. ¿Qué tal te sientes, Rust?"
        } },
        { "セーブしたよ。また何かあったら言って", new[] {
            "セーブしたよ。また何かあったら言って",
            "Progress saved. Let me know whenever you need anything, buddy.",
            "进度已经保存好啦。要是身子不舒服随时跟我说哦。",
            "Partida guardada. Avísame cuando necesites cualquier cosa."
        } },
        { "キャッチしたよ！……Floatのところへ、大事に持ってくね", new[] {
            "キャッチしたよ！……Floatのところへ、大事に持ってくね",
            "Caught it! ...I'll bring it safely to Float!",
            "接住了！……我会小心翼翼送去Float身边的！",
            "¡Atrapado! ...¡Se lo llevaré sano y salvo a Float!"
        } },
        { "了解！あのパーツを取ってくるね、Float！", new[] {
            "了解！あのパーツを取ってくるね、Float！",
            "Roger that! I'll fly over and fetch that scrap part, Float!",
            "收到！我去把那个零件捡回来，Float！",
            "¡Entendido! ¡Iré a recoger esa pieza, Float!"
        } },
        { "わぁっ！温かい上昇気流だ！風に乗って大空へ行こう、Float！", new[] {
            "わぁっ！温かい上昇気流だ！風に乗って大空へ行こう、Float！",
            "Whoa! A warm thermal updraft! Let's ride the wind into the sky, Float!",
            "哇啊！是温暖的上升气流！乘着风飞向天空吧，Float！",
            "¡Uau! ¡Una corriente térmica cálida! ¡Volemos al cielo, Float!"
        } },
        { "awakening_skip_hint", new[] {
            "【クリック / Space】で進む　【Escape】でスキップ",
            "[Click / Space] Advance  [Escape] Skip",
            "【点击 / Space】继续　【Escape】跳过",
            "[Clic / Espacio] Avanzar  [Escape] Saltar"
        } },
        { "……ザザァ……ザザァ……", new[] {
            "……ザザァ……ザザァ……",
            "...Whoosh... Whoosh...",
            "……哗啦……哗啦……",
            "...Zazaa... Zazaa..."
        } },
        { "……遠くで、波の音が聴こえる。", new[] {
            "……遠くで、波の音が聴こえる。",
            "...In the distance, the gentle ocean waves murmur.",
            "……在遥远的地方，传来了阵阵海浪声。",
            "...A lo lejos, se escucha el rumor del mar."
        } },
        { "Rust 「……Float？　……Float……？」", new[] {
            "Rust 「……Float？　……Float……？」",
            "Rust \"...Float? ...Float...?\"",
            "Rust「……Float？……Float……？」",
            "Rust «...¿Float? ...¿Float...?»"
        } },
        { "Rust 「Float……！　目を覚まして、Float……！！」", new[] {
            "Rust 「Float……！　目を覚まして、Float……！！」",
            "Rust \"Float...! Wake up, Float...!!\"",
            "Rust「Float……！快醒醒，Float……！！」",
            "Rust «¡Float...! ¡Despierta, Float...!!»"
        } },
        { "……Float？　……Float……？", new[] {
            "……Float？　……Float……？",
            "...Float? ...Float...?",
            "……Float？……Float……？",
            "...¿Float? ...¿Float...?"
        } },
        { "Float……！　目を覚まして、Float……！！", new[] {
            "Float……！　目を覚まして、Float……！！",
            "Float...! Wake up, Float...!!",
            "Float……！快醒醒，Float……！！",
            "¡Float...! ¡Despierta, Float...!!"
        } },
        { "ピピッ！……よかったぁぁ！！気がついた……！", new[] {
            "ピピッ！……よかったぁぁ！！気がついた……！",
            "Beep! ...Oh, thank goodness!! You're awake...!",
            "哔哔！……太好了呜呜！！你终于醒了……！",
            "¡Bip! ...¡¡Menos mal!! ¡¡Has despertado...!!"
        } },
        { "脱出ポッドが海に落ちて……ボクたち、この島に打ち上げられたんだ！", new[] {
            "脱出ポッドが海に落ちて……ボクたち、この島に打ち上げられたんだ！",
            "The escape pod crashed into the sea... and we washed ashore on this island!",
            "逃生舱掉进了大海……我们被海浪冲到了这座岛上！",
            "¡La cápsula cayó al mar... y fuimos arrastrados a esta isla!"
        } },
        { "キキッ……Float……塩水で古いギアが……凍りついて動かない……", new[] {
            "キキッ……Float……塩水で古いギアが……凍りついて動かない……",
            "Creak... Float... the saltwater froze my rusted gears... I can't move...",
            "嘎吱……Float……海水浸透了我的旧齿轮……冻僵动不了了……",
            "Cri-cri... Float... el agua salada congeló mis engranajes... no puedo moverme..."
        } },
        { "大丈夫だよ、Rust。ここにいるから。ぎゅっとしてていいよ", new[] {
            "大丈夫だよ、Rust。ここにいるから。ぎゅっとしてていいよ",
            "It's okay, Rust. I'm right here. Hold on tight to me.",
            "没事的，Rust。我在这里呢。紧紧抱着我就好。",
            "Tranquilo, Rust. Estoy aquí contigo. Puedes abrazarte a mí."
        } },
        { "……うぅ……もっとそばにいたい……手が……あったかいの……", new[] {
            "……うぅ……もっとそばにいたい……手が……あったかいの……",
            "...Ngh... I want to stay closer... your hands... are so warm...",
            "……呜呜……想离你更近一点……你的手……好温暖……",
            "...Uff... quiero estar más cerca... tus manos... son tan cálidas..."
        } },
        { "よしよし。怖かったね。油をさして、ゆっくり温めてあげる", new[] {
            "よしよし。怖かったね。油をさして、ゆっくり温めてあげる",
            "There, there. That was scary, wasn't it? I'll oil you up and warm you right away.",
            "好啦好啦。吓坏了吧？我这就给你上润滑油，慢慢温暖你。",
            "Ya, ya. Qué miedo pasaste. Te pondré aceite y te calentaremos despacio."
        } },
        { "……お願い……【E】で油をさして……Floatの手、必要……", new[] {
            "……お願い……【E】で油をさして……Floatの手、必要……",
            "...Please... oil me with [E]... I need your touch, Float...",
            "……拜托了……按【E】给我上油……我需要Float的手心……",
            "...Por favor... ponme aceite con [E]... necesito tus manos, Float..."
        } },
        { "……うぅ……まだ冷たいよ……Floatの手、ほしい……【E】で……", new[] {
            "……うぅ……まだ冷たいよ……Floatの手、ほしい……【E】で……",
            "...Ugh... still so cold... I need your hands, Float... with [E]...",
            "……呜呜……身上还是好冷……想要Float的手心……按【E】……",
            "...Uff... aún tengo frío... necesito tus manos, Float... con [E]..."
        } },
        { "……そばにいて……油を……さして……お願い……ぎゅっ……", new[] {
            "……そばにいて……油を……さして……お願い……ぎゅっ……",
            "...Stay with me... please oil my gears... hold me close...",
            "……留在我身边……给我……上油……求你了……抱抱我……",
            "...Quédate a mi lado... ponme... aceite... por favor... abrázame..."
        } },
        { "……あ……温かい……回路が戻ってきた……！ありがとう、Float……", new[] {
            "……あ……温かい……回路が戻ってきた……！ありがとう、Float……",
            "...Ah... so warm... my circuits are coming back online...! Thank you, Float...",
            "……啊……好温暖……回路恢复运作了……！谢谢你，Float……",
            "...Ah... qué calor... ¡mis circuitos vuelven a funcionar...! Gracias, Float..."
        } },
        { "よかった……また声が聞けて安心したよ。よしよし、いい子だね", new[] {
            "よかった……また声が聞けて安心したよ。よしよし、いい子だね",
            "Thank goodness... Hearing your voice puts me at ease. Good little buddy.",
            "太好了……再次听到你的声音我终于安心了。乖孩子，真棒。",
            "Menos mal... Me alivia tanto oírte de nuevo. Qué buen compañero eres."
        } },
        { "えへへ……もうちょっと、胸のあたりにいたい……ピピッ……", new[] {
            "えへへ……もうちょっと、胸のあたりにいたい……ピピッ……",
            "Ehehe... I want to stay nestled close to your chest a bit longer... Beep...",
            "嘿嘿……还想在你胸口前多贴一会儿……哔哔……",
            "Jeje... Quiero quedarme acurrucado en tu pecho un ratito más... Bip..."
        } },
        { "もちろん。ずっと一緒だよ。怖かったら、すぐくっついてていいからね", new[] {
            "もちろん。ずっと一緒だよ。怖かったら、すぐくっついてていいからね",
            "Of course. We're in this together. Whenever you're scared, stay right beside me.",
            "当然可以。我们会一直在一起。要是害怕，随时贴紧我就好。",
            "Por supuesto. Siempre juntos. Si tienes miedo, acércate todo lo que quieras."
        } },
        { "……うん。Floatの手の匂い、好き……もうギシギシしないよ", new[] {
            "……うん。Floatの手の匂い、好き……もうギシギシしないよ",
            "...Yeah. I love the warmth of your hands... No more creaking sounds!",
            "……嗯。最喜欢Float手上的温暖气味了……身子再也不咔哒作响了。",
            "...Sí. Me encanta el calor de tus manos... ¡Ya no chirrío nada!"
        } },
        { "さぁ、光るギアを取りに行こう。一歩ずつ、僕がそばにいるよ", new[] {
            "さぁ、光るギアを取りに行こう。一歩ずつ、僕がそばにいるよ",
            "Come on, let's go gather that glowing gear. Step by step, I'll be right with you.",
            "来，我们去捡那颗发光的齿轮吧。一步一步，我会一直陪着你。",
            "Vamos a recoger ese engranaje brillante. Paso a paso, estaré contigo."
        } },
        { "うん……！すぐ目の前——脱出艇の脇に、光るギアが落ちてるよ！", new[] {
            "うん……！すぐ目の前——脱出艇の脇に、光るギアが落ちてるよ！",
            "Yeah...! Right ahead—beside the escape pod, a glowing gear is lying there!",
            "嗯……！就在正前方——逃生舱旁边，正掉落着一颗发光的齿轮！",
            "¡Sí...! Justo delante, al lado de la cápsula, ¡hay un engranaje brillante!"
        } },
        { "ピキーン……！ギアが噛み合った……！僕、また飛べそうな気がするよ！！", new[] {
            "ピキーン……！ギアが噛み合った……！僕、また飛べそうな気がするよ！！",
            "Ding...! The gear locked into place...! I feel like I can fly again!!",
            "叮的一声……！齿轮咬合上了……！我觉得自己又能飞翔了！！",
            "¡Tink...! ¡El engranaje ha encajado...! ¡¡Siento que puedo volar de nuevo!!"
        } },
        { "すごいよ、Rust。一歩ずつ、ちゃんと戻ってきてるね", new[] {
            "すごいよ、Rust。一歩ずつ、ちゃんと戻ってきてるね",
            "Amazing, Rust. Step by step, you're restoring yourself.",
            "太棒了，Rust。一步一步，你正一点点恢复呢。",
            "Increíble, Rust. Paso a paso, te estás recuperando genial."
        } },
        { "……Floatがいてくれるから、怖くないよ。もっとくっついててもいい……？", new[] {
            "……Floatがいてくれるから、怖くないよ。もっとくっついててもいい……？",
            "...Because you're here with me, Float, I'm not afraid. Can I cuddle closer...?",
            "……因为有Float在，我什么都不怕。可以再多靠着你一会儿吗……？",
            "...Como estás conmigo, Float, no tengo miedo. ¿Puedo quedarme pegado a ti...?"
        } },
        { "いいよ。甘えてて。あと2個集めよう——砂浜の光る柱を探そう", new[] {
            "いいよ。甘えてて。あと2個集めよう——砂浜の光る柱を探そう",
            "Of course. Take your time. Let's find 2 more—look for light pillars on the beach.",
            "当然好啦。尽管撒娇吧。再去收集2个——寻找沙滩上的光柱吧。",
            "Claro. Acurrúcate todo lo que quieras. Busquemos 2 más rastreando los pilares de luz."
        } },
        { "ピロッ……もうひとつ繋がったよ。胸の奥が、すこし暖かい……", new[] {
            "ピロッ……もうひとつ繋がったよ。胸の奥が、すこし暖かい……",
            "Beep... Another one linked up. Inside my core feels a bit warmer...",
            "滴答……又连接上了一颗。胸腔核心深处变得温热起来了……",
            "Bip... Otro más conectado. Siento mi núcleo un poco más calentito..."
        } },
        { "調子はどう？無理してたら、すぐ撫でてあげるからね", new[] {
            "調子はどう？無理してたら、すぐ撫でてあげるからね",
            "How are you feeling? If it hurts, I'll pet and soothe you right away.",
            "感觉怎么样？要是勉强的话，随时跟我说，我马上给你顺顺气。",
            "¿Cómo estás? Si te duele, dímelo y te acariciaré enseguida."
        } },
        { "えへへ……今は大丈夫。でも、撫でられるの好き……あと1個だよ！", new[] {
            "えへへ……今は大丈夫。でも、撫でられるの好き……あと1個だよ！",
            "Ehehe... I'm okay for now. But I love your pets... Just 1 more to go!",
            "嘿嘿……现在感觉很好。不过被抚摸好舒服呀……只差最后1个了！",
            "Jeje... Ahora estoy bien. Pero me encantan tus caricias... ¡Solo falta 1!"
        } },
        { "ピキーン！足が軽い……！ダッシュが戻ったよ、Float！！", new[] {
            "ピキーン！足が軽い……！ダッシュが戻ったよ、Float！！",
            "Ding! My boosters are so light...! Our dash is back, Float!!",
            "叮！脚步变得轻盈了……！冲刺动力回来了，Float！！",
            "¡Tink! ¡Mis propulsores van ligeros...! ¡¡Recuperamos el esprint, Float!!"
        } },
        { "やったね！一緒に草原へ駆け上がろう。手、離さないよ", new[] {
            "やったね！一緒に草原へ駆け上がろう。手、離さないよ",
            "We did it! Let's race up into the meadows together. I won't let go.",
            "太棒啦！我们一起奔向辽阔草原吧。绝不会松开你的手。",
            "¡Lo logramos! Corramos juntos a la pradera. No te soltaré."
        } },
        { "うん……！【Shift】で走って……僕、後ろでくっついてるからね！", new[] {
            "うん……！【Shift】で走って……僕、後ろでくっついてるからね！",
            "Yeah...! Sprint with [Shift]... I'll be flying right beside you!",
            "嗯……！用【Shift】奔跑吧……我会紧紧跟在你的身后！",
            "¡Sí...! Corre con [Shift]... ¡Volaré pegadito a ti!"
        } },
        { "やったぁ！パーツ発見だね！", new[] {
            "やったぁ！パーツ発見だね！",
            "Hooray! A scrap part found!",
            "太好啦！发现零件了呢！",
            "¡Yupi! ¡Encontramos una pieza!"
        } },
        { "ピピッ♪ 大事な手がかりゲット！", new[] {
            "ピピッ♪ 大事な手がかりゲット！",
            "Beep♪ Got an important clue!",
            "哔哔♪ 获得重要线索啦！",
            "¡Bip♪ Una pista importante!"
        } },
        { "えへへ、順調順調！", new[] {
            "えへへ、順調順調！",
            "Ehehe, smooth sailing!",
            "嘿嘿，进展顺利顺利！",
            "Jeje, ¡todo marcha genial!"
        } },
        { "これでまた島を出る準備が進んだね！", new[] {
            "これでまた島を出る準備が進んだね！",
            "One step closer to escaping this island!",
            "这样一来，逃离岛屿的准备又前进了一步呢！",
            "¡Un paso más cerca de escapar de la isla!"
        } },
        { "ピロリン！見つけてくれてありがとう、Float！", new[] {
            "ピロリン！見つけてくれてありがとう、Float！",
            "Ding-dong! Thank you for finding this, Float!",
            "叮咚！谢谢你把它找出来，Float！",
            "¡Ding-dong! ¡Gracias por encontrarlo, Float!"
        } },
        { "わぁぁ！カピタに貝殻プレゼントできたね！頭にみかん乗せて大喜びしてるよ！", new[] {
            "わぁぁ！カピタに貝殻プレゼントできたね！頭にみかん乗せて大喜びしてるよ！",
            "Whoa! We gifted the shell to Capyta! Look how happy it is with an orange on its head!",
            "哇啊啊！给卡皮塔送上贝壳礼物了呢！它头上顶着橘子开心地不得了！",
            "¡Uau! ¡Le dimos la concha a Capyta! ¡Mira qué contento está con la mandarina en la cabeza!"
        } },
        { "わぁぁ！崖の上にこんなお宝があったんだね！ボクたちの力で登れたよ、Float！", new[] {
            "わぁぁ！崖の上にこんなお宝があったんだね！ボクたちの力で登れたよ、Float！",
            "Whoa! What a treasure perched atop the cliff! We climbed up here on our own, Float!",
            "哇啊！悬崖顶上居然藏着这等宝物！凭我们自己的力量登上来了呢，Float！",
            "¡Uau! ¡Menudo tesoro había sobre el acantilado! ¡Subimos con nuestras propias fuerzas, Float!"
        } },

        // ==========================================
        // カピタ（Capyta）会話・機嫌・祝福・物々交換
        // ==========================================
        { "capyta_name", new[] { "カピタ", "Capyta", "卡皮塔", "Capyta" } },
        { "capyta_speaker_title", new[] { "🐾 カピタ", "🐾 Capyta", "🐾 卡皮塔", "🐾 Capyta" } },
        { "capyta_speaker_blessing", new[] { "🐾 カピタの祝福 ✦ スーパージャンプ獲得！", "🐾 Capyta's Blessing ✦ Super Jump Unlocked!", "🐾 卡皮塔的祝福 ✦ 获得超级跳跃！", "🐾 ¡Bendición de Capyta ✦ Super Salto desbloqueado!" } },
        { "capyta_speaker_trade", new[] { "🐾 カピタ ✦ 『{0}』の物々交換！", "🐾 Capyta ✦ Trading for \"{0}\"!", "🐾 卡皮塔 ✦ 『{0}』物物交换！", "🐾 Capyta ✦ ¡Intercambio por «{0}»!" } },
        { "capyta_speaker_mood", new[] { "🐾 カピタ（{0}）", "🐾 Capyta ({0})", "🐾 卡皮塔（{0}）", "🐾 Capyta ({0})" } },

        { "capyta_mood_calm", new[] { "ふつうの機嫌", "Calm Mood", "普通的心情", "Humor tranquilo" } },
        { "capyta_mood_happy", new[] { "ご機嫌", "Good Mood", "好心情", "Buen humor" } },
        { "capyta_mood_generous", new[] { "気前よし", "Generous", "大方慷慨", "Generoso" } },
        { "capyta_mood_jackpot", new[] { "大盤振る舞い！！", "Jackpot!!", "超级大丰收！！", "¡¡Premio gordo!!" } },

        { "ブヒ…今日はまあまあ。油、これくらいで我慢してね。", new[] {
            "ブヒ…今日はまあまあ。油、これくらいで我慢してね。",
            "Oink... Feeling so-so today. Make do with this much oil.",
            "呼噜…今天马马虎虎吧。这点油就先将就着用吧。",
            "Oink... Hoy estoy así asá. Confórmate con este poquito de aceite."
        } },
        { "ブヒヒ！機嫌がいいよ。潤滑油、多めにあげるね！", new[] {
            "ブヒヒ！機嫌がいいよ。潤滑油、多めにあげるね！",
            "Oink-oink! I'm in a great mood! Here's extra lubricant for you!",
            "呼噜噜！今天心情真好。多给你一点润滑油吧！",
            "¡Oink-oink! ¡Estoy de buen humor! ¡Te daré bastante aceite lubricante!"
        } },
        { "プヒヒ……！今日は気前がいい日。缶をあけて、たっぷりの油を持っていって！", new[] {
            "プヒヒ……！今日は気前がいい日。缶をあけて、たっぷりの油を持っていって！",
            "Snort-snort...! Feeling extra generous today. Pop the can and take plenty of oil!",
            "呼哧哧……！今天我格外慷慨。开罐拿走大把的润滑油吧！",
            "¡Oink...! Hoy me siento generoso. ¡Abre la lata y llévate un montón de aceite!"
        } },
        { "ブヒッヒッヒ！！最高の気分だ！！油を山盛りにしてあげる！！Rustをぬるぬるにしてあげて！", new[] {
            "ブヒッヒッヒ！！最高の気分だ！！油を山盛りにしてあげる！！Rustをぬるぬるにしてあげて！",
            "OINK-OINK-OINK!! Feeling fantastic!! A mountain of oil for you!! Polish Rust till he shines!",
            "呼噜噜噜噜！！心情棒极啦！！给你堆成小山一样的油！！把Rust涂得滑溜溜的吧！",
            "¡¡OINK-OINK!! ¡¡Me siento genial!! ¡¡Toma una montaña de aceite!! ¡Deja a Rust reluciente y resbaladizo!"
        } },

        { "ブヒヒ…！大地の弾力と、相棒のための潤滑油をわけてあげるね！機嫌次第でもっと出すよ！", new[] {
            "ブヒヒ…！大地の弾力と、相棒のための潤滑油をわけてあげるね！機嫌次第でもっと出すよ！",
            "Oink-oink...! I'll share the earth's bounce and lubricant for your pal! I'll give more when I'm thrilled!",
            "呼噜噜…！把大地的弹力与伙伴的润滑油分给你们！要是心情更好，我还会给更多哦！",
            "¡Oink-oink...! ¡Os doy el bote de la tierra y aceite para tu compi! ¡Si estoy de buenas, daré más!"
        } },
        { "ブヒッ。Rustのために油をたっぷり。調子がいい日は、もっと山盛りにしてあげる！", new[] {
            "ブヒッ。Rustのために油をたっぷり。調子がいい日は、もっと山盛りにしてあげる！",
            "Oink. Plenty of oil for Rust. On lucky days, I'll shower you with heaps more!",
            "呼噜。给Rust准备了充足的油。状态好的日子，还会给你们堆得更高哦！",
            "Oink. Mucho aceite para Rust. ¡Los días buenos os daré una montaña aún mayor!"
        } },

        { "ピロッ……油もらったよ。あとで整備しよう", new[] {
            "ピロッ……油もらったよ。あとで整備しよう",
            "Beep... Got some oil. Let's do maintenance later.",
            "滴答……收到油了呢。待会儿做个检修吧。",
            "Bip... Ya tenemos aceite. Hagamos mantenimiento luego."
        } },
        { "カピタ機嫌がいいね。油、ありがたい", new[] {
            "カピタ機嫌がいいね。油、ありがたい",
            "Capyta seems joyful. Really appreciate the oil.",
            "卡皮塔心情很好呢。真感谢这珍贵的油。",
            "Capyta está contento. Qué bien nos viene el aceite."
        } },
        { "わぁ……油がたくさん。助かるよ、Float", new[] {
            "わぁ……油がたくさん。助かるよ、Float",
            "Whoa... so much oil! This helps so much, Float.",
            "哇啊……好多润滑油。帮大忙了呢，Float。",
            "¡Hala...! ¡Cuánto aceite! Nos ayuda muchísimo, Float."
        } },
        { "ピキーッ……山盛りだ。これでしばらく安心だね", new[] {
            "ピキーッ……山盛りだ。これでしばらく安心だね",
            "Beep-ding...! A whole mountain! We're all set for a good while.",
            "哔咔……堆成小山啦。这下能安心好一阵子了呢。",
            "¡Bip-bip...! ¡Una barbaridad! Con esto aguantamos tranquilos."
        } },

        { "わぁ…！カピタの祝福だ！スーパージャンプと油をもらったよ、Float！！", new[] {
            "わぁ…！カピタの祝福だ！スーパージャンプと油をもらったよ、Float！！",
            "Whoa...! Capyta's blessing! We got Super Jump and oil, Float!!",
            "哇啊…！是卡皮塔的祝福！获得了超级跳跃和润滑油，Float！！",
            "¡Uau...! ¡La bendición de Capyta! ¡Tenemos Super Salto y aceite, Float!"
        } },
        { "ピキーッ！カピタ優しい…！ジャンプも油も…Float、あとで撫でてね……？", new[] {
            "ピキーッ！カピタ優しい…！ジャンプも油も…Float、あとで撫でてね……？",
            "Beep-whee! Capyta is so sweet...! Jump and oil... Float, will you pet me later...?",
            "哔咔！卡皮塔好温柔…！又是跳跃又是润滑油…Float，待会儿摸摸我好吗……？",
            "¡Bip! ¡Capyta es tan cariñoso...! Salto y aceite... Float, ¿me acaricias luego...?"
        } },

        { "ブヒヒ！桜色のきれいなサクラガイ！大好物だよ！お礼に油を山盛りあげるね！", new[] {
            "ブヒヒ！桜色のきれいなサクラガイ！大好物だよ！お礼に油を山盛りあげるね！",
            "Oink-oink! A gorgeous pink blossom shell! My favorite! Here's a mountain of oil as a reward!",
            "呼噜噜！粉樱般美丽的樱贝！是我的最爱！作为谢礼给你堆成山的油！",
            "¡Oink-oink! ¡Una preciosa concha rosa cerezo! ¡Mi favorita! ¡Toma una montaña de aceite!"
        } },
        { "プヒッ！深緑のガラス玉！海が削ったエメラルドだね、頭のみかんも喜んでるよ！", new[] {
            "プヒッ！深緑のガラス玉！海が削ったエメラルドだね、頭のみかんも喜んでるよ！",
            "Snort! A deep green sea glass pebble! An emerald polished by the sea, my mandarin orange loves it!",
            "呼哧！深绿色的海玻璃珠！是大海打磨的翡翠呢，连我头上的橘子都乐开花了！",
            "¡Oink! ¡Un cristal marino verde esmeralda! Tallado por el mar, ¡hasta mi mandarina está contenta!"
        } },
        { "ブヒヒッ！吸い込まれそうな青いサファイアガラス…一番の宝物にするね！", new[] {
            "ブヒヒッ！吸い込まれそうな青いサファイアガラス…一番の宝物にするね！",
            "Oink-snort! Mesmerizing blue sapphire glass... I'll treasure it as my top jewel!",
            "呼噜噜！深邃如海的蓝宝石玻璃……我要把它当成最棒的宝贝珍藏！",
            "¡Oink! Cristal azul zafiro tan profundo... ¡Será mi mayor tesoro!"
        } },
        { "ブヒィィ！黄金色に透き通る太陽の琥珀！最高のお宝だ！！", new[] {
            "ブヒィィ！黄金色に透き通る太陽の琥珀！最高のお宝だ！！",
            "OINK!! Translucent golden sun amber! The greatest treasure ever!!",
            "呼噜——！！晶莹澄澈的金色太阳琥珀！简直是无上的宝物！！",
            "¡¡OINK!! ¡Ámbar solar dorado y translúcido! ¡¡Es el mejor tesoro de todos!!"
        } },
        { "プヒ〜…耳に当てると遠い海の波音がするよ。大切にするね！", new[] {
            "プヒ〜…耳に当てると遠い海の波音がするよ。大切にするね！",
            "Snort~... Listening closely, I hear waves of distant seas. I will cherish it!",
            "呼哧~…贴近耳朵能听见远方潮汐的回音呢。我会好好珍惜的！",
            "Oink~... Si escuchas con atención, suena el oleaje del mar lejano. ¡Lo cuidaré bien!"
        } },

        { "桜色のサクラガイ", new[] { "桜色のサクラガイ", "Pink Cherry Shell", "樱色樱贝", "Concha Rosa Cerezo" } },
        { "エメラルド・シーグラス", new[] { "エメラルド・シーグラス", "Emerald Sea Glass", "翡翠海玻璃", "Cristal Marino Esmeralda" } },
        { "サファイア・シーグラス", new[] { "サファイア・シーグラス", "Sapphire Sea Glass", "蓝宝石海玻璃", "Cristal Marino Zafiro" } },
        { "太陽の小琥珀", new[] { "太陽の小琥珀", "Sun Amber Pebble", "太阳小琥珀", "Pequeño Ámbar Solar" } },
        { "純白の小巻貝", new[] { "純白の小巻貝", "Pure White Conch", "纯白小卷贝", "Caracola Blanca Pura" } },
        { "綺麗な貝殻", new[] { "綺麗な貝殻", "Beautiful Seashell", "美丽的贝壳", "Hermosa Concha" } },
        { "ブヒヒ！きれいな貝殻、ありがとう！", new[] {
            "ブヒヒ！きれいな貝殻、ありがとう！",
            "Oink-oink! Thank you for the lovely shell!",
            "呼噜噜！谢谢你送来这么好看的贝壳！",
            "¡Oink-oink! ¡Gracias por la bonita concha!"
        } },

        { "カピタ「ブヒ…？ 砂浜に落ちてる綺麗な貝殻やシーグラスを持ってきてくれたら、お宝と物々交換するよ！」", new[] {
            "カピタ「ブヒ…？ 砂浜に落ちてる綺麗な貝殻やシーグラスを持ってきてくれたら、お宝と物々交換するよ！」",
            "Capyta: \"Oink...? Bring me pretty seashells and sea glass along the beach, and I'll trade treasures with you!\"",
            "卡皮塔「呼噜…？要是把沙滩上漂亮的贝壳或海玻璃带给我，我可以用宝物跟你物物交换哦！」",
            "Capyta: «¿Oink...? ¡Si me traes bonitas conchas y cristales marinos de la playa, los cambiaré por tesoros!»"
        } },

        { "prompt_capyta_trade", new[] {
            "🐾 【E / クリック】ふれあう  |  🐚 【Q】貝殻を渡して物々交換（所持: {0}個）",
            "🐾 [E / Click] Interact  |  🐚 [Q] Trade Seashell (Own: {0})",
            "🐾 【E / 点击】互动  |  🐚 【Q】赠送贝壳交换宝物（持有: {0}个）",
            "🐾 [E / Clic] Interactuar  |  🐚 [Q] Cambiar concha (Tienes: {0})"
        } },
        { "prompt_capyta_talk_skin", new[] {
            "🐾 【E / クリック】カピタとふれあう（スキンシップ＆潤滑油）",
            "🐾 [E / Click] Pet Capyta (Skinship & Lubricant)",
            "🐾 【E / 点击】与卡皮塔互动（亲近触碰＆获取润滑油）",
            "🐾 [E / Clic] Acariciar a Capyta (Cariño y lubricante)"
        } },
        { "prompt_capyta_talk_first", new[] {
            "🐾 【E / クリック】カピタと話す（スーパージャンプ＆潤滑油）",
            "🐾 [E / Click] Talk to Capyta (Super Jump & Lubricant)",
            "🐾 【E / 点击】与卡皮塔交谈（获得超级跳跃＆润滑油）",
            "🐾 [E / Clic] Hablar con Capyta (Super Salto y lubricante)"
        } },

        // ==========================================
        // ワークショップUI＆アクセサリー（Craft Workshop & Cosmetics）
        // ==========================================
        { "workshop_title", new[] {
            "✦ Rustクラフト工房 ✦ 〜 相棒のドレスアップ 〜",
            "✦ Rust Craft Workshop ✦ — Dress-Up Studio —",
            "✦ Rust工艺工坊 ✦ 〜 伙伴装扮 〜",
            "✦ Taller de artesanía de Rust ✦ — Vestuario —"
        } },
        { "workshop_pouch_title", new[] { "✦ 素材ポーチ", "✦ Material Pouch", "✦ 素材锦囊", "✦ Bolsa de materiales" } },
        { "workshop_pouch_sub", new[] { "浜辺の波打ち際で拾った宝物", "Treasures gathered along the waves", "在海滨浪花边拾得的宝物", "Tesoros recogidos en la orilla" } },
        { "workshop_pouch_hint", new[] {
            "\n【ヒント】\n砂浜のキラキラ光る場所へ行き、[Eキー] で拾えます。\n全24個の貝殻が波打ち際に漂着しています。",
            "\n[Tip]\nHead to glittering spots on the beach and press [E] to collect.\n24 seashells in total are washed ashore along the beach.",
            "\n【提示】\n前往沙滩上闪闪发光之处，按下【E键】即可拾取。\n共有24枚贝壳漂流搁浅在浪花边缘。",
            "\n[Consejo]\nVe a las zonas brillantes de la playa y pulsa [E] para recogerlas.\nHay 24 conchas en total varadas a lo largo de la orilla."
        } },
        { "workshop_btn_hud", new[] { "👗 Rust工房 [B]", "👗 Rust Workshop [B]", "👗 Rust工坊 [B]", "👗 Taller de Rust [B]" } },
        { "workshop_status_equipped", new[] { "<color=#66FFAA><b>✦ そうび中</b></color>", "<color=#66FFAA><b>✦ Equipped</b></color>", "<color=#66FFAA><b>✦ 穿戴中</b></color>", "<color=#66FFAA><b>✦ Equipado</b></color>" } },
        { "workshop_status_owned", new[] { "<color=#BBBBCC>所持中</color>", "<color=#BBBBCC>In Bag</color>", "<color=#BBBBCC>已拥有</color>", "<color=#BBBBCC>En inventario</color>" } },
        { "workshop_status_crafted", new[] { "<color=#AAAAAA>作成済み</color>", "<color=#AAAAAA>Crafted</color>", "<color=#AAAAAA>已制作</color>", "<color=#AAAAAA>Fabricado</color>" } },
        { "workshop_status_craftable", new[] { "<color=#FFAA33><b>✦ 作成可能！</b></color>", "<color=#FFAA33><b>✦ Ready to Craft!</b></color>", "<color=#FFAA33><b>✦ 可制作！</b></color>", "<color=#FFAA33><b>✦ ¡Listo para fabricar!</b></color>" } },
        { "workshop_status_need_mat", new[] { "<color=#AA9988>素材を集めて作成</color>", "<color=#AA9988>Gather Materials</color>", "<color=#AA9988>收集素材以制作</color>", "<color=#AA9988>Reúne materiales</color>" } },
        { "workshop_btn_unequip", new[] { "[{0}] はずす", "[{0}] Unequip", "[{0}] 卸下", "[{0}] Quitar" } },
        { "workshop_btn_equip", new[] { "[{0}] そうび", "[{0}] Equip", "[{0}] 穿戴", "[{0}] Equipar" } },
        { "workshop_btn_craft", new[] { "[{0}] ✦ 作成する", "[{0}] ✦ Craft", "[{0}] ✦ 制作", "[{0}] ✦ Fabricar" } },
        { "workshop_cost_format", new[] { "必要: <color=#{0}>{1}</color> <color={2}>{3}/{4}</color>", "Cost: <color=#{0}>{1}</color> <color={2}>{3}/{4}</color>", "需求: <color=#{0}>{1}</color> <color={2}>{3}/{4}</color>", "Coste: <color=#{0}>{1}</color> <color={2}>{3}/{4}</color>" } },

        // コスメティクス5種
        { "cosmetic_sakuragai_name", new[] { "サクラガイの花冠", "Blossom Crown", "樱贝花冠", "Corona de Flor de Concha" } },
        { "cosmetic_sakuragai_desc", new[] {
            "薄紅色の貝殻を優美に編み込んだティアラ。桜色の優しい光の粉がふわふわと舞う。",
            "A tiara woven gracefully from delicate pink shells. Gentle blossom petals float around softly.",
            "以淡粉樱贝优雅编织而成的花冠王冠。柔和的粉白光尘在周围悠扬飞舞。",
            "Una tiara tejida delicadamente con conchas rosas. Partículas suaves flotan a su alrededor."
        } },
        { "cosmetic_sakuragai_reaction", new[] {
            "わぁ…！花冠、すっごく可愛い！お花の妖精さんみたい？",
            "Whoa...! The crown is so adorable! Do I look like a flower fairy?",
            "哇啊…！花冠好可爱呀！我是不是像花间的小妖精？",
            "¡Hala...! ¡La corona es monísima! ¿Parezco un hada de las flores?"
        } },

        { "cosmetic_emerald_name", new[] { "エメラルド・アンテナランプ", "Emerald Beacon", "翡翠天线灯", "Faro Esmeralda" } },
        { "cosmetic_emerald_desc", new[] {
            "アンテナ先端に装着する深緑のクリスタル。暗がりで神秘的な光を灯し、周囲を照らす。",
            "A deep-green crystal mounted on the antenna tip. Casts a mystical glow that illuminates the dark.",
            "装备于天线顶端的深绿水晶。在幽暗中泛起神秘幽光，照亮身旁四方。",
            "Un cristal verde intenso en la punta de la antena. Arroja una luz mística que ilumina la oscuridad."
        } },
        { "cosmetic_emerald_reaction", new[] {
            "ピキッ！頭がピカピカ光ってる！これなら暗い森も怖くないよ！",
            "Beep-ding! My head is glowing bright! Now dark woods won't be scary at all!",
            "哔咔！脑袋变得亮闪闪的！这样一来黑暗的森林也完全不害怕了！",
            "¡Bip-tink! ¡Mi cabeza brilla mucho! ¡Ya no le tendré miedo a los bosques oscuros!"
        } },

        { "cosmetic_sapphire_name", new[] { "サファイアの翼チャーム", "Sapphire Wings", "蓝宝石之翼吊饰", "Alas de Zafiro" } },
        { "cosmetic_sapphire_desc", new[] {
            "波に磨かれた蒼いガラスの小翼。空を飛ぶたびに流麗なサファイアの光跡（Trail）を残す。",
            "Blue glass winglets polished by the waves. Leaves a graceful sapphire light trail wherever you fly.",
            "经浪涛打磨的蔚蓝玻璃双翼。飞翔时会在空中留下优雅流动的蓝宝石光轨。",
            "Alitas de cristal azul pulidas por el oleaje. Deja una elegante estela de zafiro al volar."
        } },
        { "cosmetic_sapphire_reaction", new[] {
            "見て見てFloat！飛ぶたびに青い光がキラキラついてくるよ！",
            "Look, look, Float! A trail of sparkling blue follows me everywhere I fly!",
            "快看快看Float！每次飞翔身后都跟着一串闪闪发光的蓝光呢！",
            "¡Mira, mira, Float! ¡Me sigue una estela azul brillante cada vez que vuelo!"
        } },

        { "cosmetic_amber_name", new[] { "太陽の琥珀コア", "Amber Sun Core", "太阳琥珀核心", "Núcleo de Ámbar Solar" } },
        { "cosmetic_amber_desc", new[] {
            "胴体中央のランプが温かい黄金琥珀色に輝き、ホバリング推進炎がゴールドスパークに変化する。",
            "The center lamp shines with warm golden amber, turning hovering thruster flames into golden sparks.",
            "机体中央的灯光泛起温暖金珀光辉，悬浮喷射的推进尾焰变为耀眼的金色星火。",
            "La luz central brilla con un cálido ámbar dorado y las llamas propulsoras se vuelven chispas de oro."
        } },
        { "cosmetic_amber_reaction", new[] {
            "あったかい…！太陽の力が湧いてくるみたい！",
            "So warm...! Feels like the sun's vibrant power is surging inside!",
            "好温暖……！仿佛太阳的力量源源不断涌上来了！",
            "¡Qué calorcito...! ¡Siento surgir la fuerza del sol en mi interior!"
        } },

        { "cosmetic_conch_name", new[] { "純白巻貝のホイッスル", "Seashell Whistle", "纯白卷贝哨音", "Silbato de Caracola" } },
        { "cosmetic_conch_desc", new[] {
            "側面に寄り添う小さな純白巻貝。Rustが嬉しそうに鳴くときに澄んだ潮騒のチャイムが重なる。",
            "A tiny white spiral shell nestled on his side. Harmonizes with a crystal tide chime when Rust chirps happily.",
            "依偎在侧面的纯白小卷贝。当Rust开心地鸣叫时，会叠加上清澈宛转的海潮风铃乐声。",
            "Una pequeña caracola blanca a su costado. Suena con un limpio murmullo de olas cuando Rust pita contento."
        } },
        { "cosmetic_conch_reaction", new[] {
            "ポォーッ♪ 耳をすますと、波の音が聴こえるよ！",
            "Toot-toot♪ If you listen close, you can hear the ocean waves!",
            "嘟——♪ 侧耳倾听，能听见海潮的涛声呢！",
            "¡Tururú♪ Si aguzas el oído, ¡puedes escuchar las olas del mar!"
        } },

        // ==========================================
        // タイムアタック＆スカイダイブ（Gliding Time Attack）
        // ==========================================
        { "ta_btn_warp", new[] { "🪂 [R] スカイダイブ台", "🪂 [R] Skydive Platform", "🪂 [R] 高空跳伞台", "🪂 [R] Plataforma de salto" } },
        { "ta_hud_rings", new[] { "リング: {0}/{1}  {2}", "Rings: {0}/{1}  {2}", "风环: {0}/{1}  {2}", "Aros: {0}/{1}  {2}" } },
        { "ta_hud_retry", new[] { "🔄 [R]キーでいつでもスタート台へ即時リトライ", "🔄 Press [R] anytime to retry from start", "🔄 按 [R] 键可随时重新返回起点", "🔄 Pulsa [R] en cualquier momento para reiniciar" } },
        { "ta_result_new_record", new[] { "🏆 NEW RECORD! 達成！", "🏆 NEW RECORD ACHIEVED!", "🏆 新纪录达成！", "🏆 ¡NUEVO RÉCORD CONSEGUIDO!" } },
        { "ta_result_finish", new[] { "🏁 FLIGHT FINISHED!", "🏁 FLIGHT FINISHED!", "🏁 飞行结束！", "🏁 ¡VUELO FINALIZADO!" } },
        { "ta_result_details", new[] {
            "タイム: {0:F2} 秒\n通過リング: {1} / {2}\n評価ランク: {3}\nベストタイム: {4:F2} 秒 ({5})",
            "Time: {0:F2} s\nRings Passed: {1} / {2}\nRank: {3}\nBest Time: {4:F2} s ({5})",
            "用时: {0:F2} 秒\n通过风环: {1} / {2}\n评价等级: {3}\n最佳纪录: {4:F2} 秒 ({5})",
            "Tiempo: {0:F2} s\nAros cruzados: {1} / {2}\nCalificación: {3}\nMejor tiempo: {4:F2} s ({5})"
        } },
        { "ta_btn_fly_again", new[] { "🪂 もう一度飛ぶ！ [R]キー", "🪂 Fly Again! [R] Key", "🪂 再飞一次！ [R] 键", "🪂 ¡Volar otra vez! Tecla [R]" } },
        { "ta_clear_s", new[] {
            "信じられないスピードだ、Float！Sランク達成！！お前、翼が生えてるんじゃないか！？（タイム: {0:F2}秒）",
            "Incredible speed, Float! S-Rank achieved!! Do you secretly have wings?! (Time: {0:F2}s)",
            "不可思议的速度，Float！达成S级评分！！你背后是不是悄悄长翅膀了！？（用时: {0:F2}秒）",
            "¡Velocidad increíble, Float! ¡¡Rango S conseguido!! ¿No te habrán salido alas a ti también? (Tiempo: {0:F2}s)"
        } },
        { "ta_clear_a", new[] {
            "ナイスゴール！Aランクだ！風に乗る感覚、最高だったろ！？（タイム: {0:F2}秒）",
            "Nice landing! A-Rank! Riding the winds felt amazing, didn't it?! (Time: {0:F2}s)",
            "顺利抵达终点！是A级评分哦！乘风飞翔的感觉很棒吧！？（用时: {0:F2}秒）",
            "¡Buena llegada! ¡Rango A! Sentir el viento bajo las alas es lo mejor, ¿a que sí? (Tiempo: {0:F2}s)"
        } },
        { "ta_clear_generic", new[] {
            "無事着地！気持ちいい滑空だったね！[R]キーでスタートに戻って、次はもっと上を狙おう！（タイム: {_resultTimer:F2}秒）",
            "Safe landing! That was such a refreshing glide! Press [R] to retry and aim higher! (Time: {0:F2}s)",
            "平安着陆！真是让人心旷神怡的滑翔呢！按 [R] 键返回起点，下次冲刺更高纪录吧！（用时: {0:F2}秒）",
            "¡Aterrizaje perfecto! ¡Qué planeo tan refrescante! ¡Pulsa [R] para volver al inicio y superarte! (Tiempo: {0:F2}s)"
        } },

        // ==========================================
        // 砂浜ナラティブ（漂着艇・キャンプ・メモリチップ・漂着ボトル）
        // ==========================================
        { "narrative_prompt_memory", new[] { "【Eキー】記憶チップを再生", "[E] Play Memory Chip", "【E键】回放记忆芯片", "[E] Reproducir chip de memoria" } },
        { "narrative_prompt_bottle", new[] { "【Eキー】手紙を読む", "[E] Read Letter", "【E键】阅读信笺", "[E] Leer carta" } },
        { "narrative_prompt_inspect", new[] { "【Eキー】調べる", "[E] Inspect", "【E键】调查", "[E] Examinar" } },

        // 漂着艇
        { "narrative_boat_title", new[] { "✦ 二人の漂着艇（木造手漕ぎボート） ✦", "✦ Our Escape Rowboat ✦", "✦ 二人的漂流木舟 ✦", "✦ Nuestra barca de remos varada ✦" } },
        { "narrative_boat_sub", new[] { "外の管理都市コード704から……", "From exterior Administration City Code 704…", "来自外界管制都市代号704……", "Desde la Ciudad Administrativa exterior, Código 704…" } },
        { "narrative_boat_body", new[] {
            "潮風と荒波に耐え抜いた、小さな木造の手漕ぎボート。\n\n【Floatの航海日誌】\n「2050年7月14日 深夜。\n『効率性欠如』としてスクラップ処分が決まったRustを整備ドックから連れ出し、夜の海へ漕ぎ出した。\n小さな木造ボートで真っ暗な外洋に出たときは、冷たい荒波が打ち寄せて死ぬほど怖かった。\nでも、暗闇の中でRustが小さな体で船首に座って、ランタンの灯りで前を照らし続けてくれたから、迷わずに漕ぎ続けられた。\n\nオールを握る手の豆がつぶれ、波に揺られてどれくらい経っただろう。\n気がつくとこの波の静かな美しい白砂ビーチに打ち上げられていた。\n見上げた空は眩しいほど青く、風に乗って蝉の声が聞こえて……\n二人で抱き合って、声を出して泣いた」",
            "A small wooden rowboat that weathered fierce tides and salt winds.\n\n[Float's Log]\n\"Late night, July 14, 2050.\nAfter Rust was marked for scrap due to 'efficiency deficits', I smuggled him out from the maintenance dock and rowed into the dark open sea.\nVenturing into the pitch-black exterior ocean in a wooden skiff was terrifying as freezing crests slammed against us.\nYet through the void, Rust sat curled on the bow, keeping his little lantern burning to light our way forward, guiding every stroke.\n\nBlisters tore across my palms, rocking endlessly on the swell.\nBefore I knew it, we washed ashore onto this serene white-sand beach.\nThe sky above was blindingly blue, sea breezes carried the chorus of cicadas...\nWe held each other tight, weeping aloud in relief.\"",
            "经受住了咸湿海风与滔天巨浪的细小木制手划船。\n\n【Float的航海日志】\n「2050年7月14日 深夜。\nRust因『缺乏效率』被判定为废弃拆解，我将它从检修船坞偷偷带出，划向了茫茫夜海。\n乘坐细小木船驶入漆黑的外洋时，冰冷刺骨的狂浪扑面而来，吓得我浑身发抖。\n但在无边黑暗中，Rust用那娇小的身躯坐在船首，高举灯火不停照亮前路，让我坚定了不断划桨的决心。\n\n手掌上握桨磨出的水泡早已破裂，在浪涛摇曳中不知漂流了多久。\n回过神来时，已经搁浅在这片波浪恬静的美丽白沙滩上。\n仰望的天空蔚蓝耀眼，微风中传来了蝉鸣的旋律……\n我们紧紧相拥在一起，放声痛哭。」",
            "Una pequeña barca de remos de madera que sobrevivió a duras olas y salitre.\n\n[Diario de Float]\n«Medianoche del 14 de julio de 2050.\nCuando Rust fue sentenciado al desguace por 'falta de eficiencia', lo saqué en secreto del taller y remé hacia el mar oscuro.\nSalir a mar abierto en una barca de madera fue aterrador mientras las olas heladas nos golpeaban.\nPero en la oscuridad, Rust se sentó en la proa, alumbrando sin parar con su linterna para guiar cada remada.\n\nSe me reventaron las ampollas de las manos de tanto remar mecido por las olas.\nAl despertar, habíamos varado en esta hermosa playa de arena blanca y aguas tranquilas.\nEl cielo era deslumbrantemente azul, la brisa traía el canto de las cigarras...\nNos abrazamos con fuerza y lloramos juntos.»"
        } },
        { "narrative_boat_rust", new[] {
            "「あの航海、怖かったね、Float……でも、Floatが一生懸命オールを漕いで僕を守ってくれたから、僕の回路はショートしなかったんだよ」",
            "«That voyage was scary, Float… But because you rowed with all your might to protect me, my circuits never shorted out.»",
            "「那场航行好可怕呢，Float……但是，正因为Float拼命划桨保护我，我的回路才没有短路烧坏。」",
            "«Aquel viaje dio miedo, Float… Pero como remaste con todas tus fuerzas para protegerme, mis circuitos no se quemaron.»"
        } },
        // 漂着艇モーダル（ハードコード文字列のフォールバック用）
        { "✦ 二人の漂着艇（木造手漕ぎボート） ✦", new[] { "✦ 二人の漂着艇（木造手漕ぎボート） ✦", "✦ Our Escape Rowboat ✦", "✦ 二人的漂流木舟 ✦", "✦ Nuestra barca de remos varada ✦" } },
        { "外の管理都市コード704から海を渡ってきた小さな木の小舟", new[] { "外の管理都市コード704から……", "From exterior Administration City Code 704…", "来自外界管制都市代号704……", "Desde la Ciudad Administrativa exterior, Código 704…" } },
        { "潮風と荒波に耐え抜いた、小さな木造の手漕ぎボート。", new[] {
            "潮風と荒波に耐え抜いた、小さな木造の手漕ぎボート。",
            "A small wooden rowboat that weathered fierce tides and salt winds.",
            "经受住了咸湿海风与滔天巨浪的细小木制手划船。",
            "Una pequeña barca de remos de madera que sobrevivió a duras olas y salitre."
        } },
        { "narrative_float_voyage_log", new[] { "【Floatの航海日誌】", "[Float's Log]", "【Float的航海日志】", "[Diario de Float]" } },
        { "(古代の推進黄金ギア)", new[] { "(古代の推進黄金ギア)", "(Ancient Propulsion Gold Gear)", "(古代推进黄金齿轮)", "(Engranaje Dorado de Propulsión Antigua)" } },
        { "(海風のエネルギーコア)", new[] { "(海風のエネルギーコア)", "(Sea Breeze Energy Core)", "(海风能量核心)", "(Núcleo de Energía de la Brisa Marina)" } },

        // ビーチキャンプ跡
        { "narrative_camp_title", new[] { "✦ 漂着初日のビーチキャンプ（追憶の焚き火） ✦", "✦ First Night Beach Camp (Campfire of Memories) ✦", "✦ 漂流首日的海滩营地（追忆的篝火） ✦", "✦ Campamento de la primera noche (Hoguera de recuerdos) ✦" } },
        { "narrative_camp_sub", new[] { "二人が初めて火を囲んで眠った場所", "Where we first gathered around the fire to rest", "二人首次围坐在火堆旁安眠之所", "Donde descansamos por primera vez junto al fuego" } },
        { "narrative_camp_body", new[] {
            "流木と石で組まれた小さな焚き火跡。\n\n島に漂着した初日の夜、服を乾かし、震える肩を寄せ合って火を灯した。\n外の都市では見ることのできなかった無数の星と、静かな波の音。\n「僕たち、本当に逃げてこられたんだね」\n二つの影が、火の粉と一緒に夜空へ揺れていた。",
            "A modest firepit circled by driftwood and stones.\n\nOn our first night after washing ashore, we dried our soaking clothes and huddled shivering beside the fire.\nCountless stars unseen beneath city domes, accompanied by gentle whisper of waves.\n\"We really made it out, didn't we?\"\nTwo silhouettes swayed softly into the night sky alongside glowing embers.",
            "用浮木与顽石围筑而成的小小营火痕迹。\n\n漂流到岛上的首夜，我们烘干湿透的衣裳，依偎着颤抖的双肩点燃了微火。\n那是外界都市中从未得见的繁星银河，以及轻柔恬静的潮浪之声。\n「我们，真的顺利逃出来了呢」\n两道交叠的身影，伴随着飞扬的火星在夜幕下轻轻摇曳。",
            "Un pequeño hogar circular de piedras y madera flotante.\n\nLa primera noche tras llegar a la isla, secamos la ropa y nos acurrucamos temblorosos al calor del fuego.\nUn sinfín de estrellas jamás vistas en las ciudades cúpula, acompañadas del suave susurro de las olas.\n«De verdad lo hemos conseguido, ¿verdad?»\nDos siluetas mecidas suavemente en la noche junto a las ascuas doradas."
        } },
        { "narrative_camp_rust", new[] {
            "「あの日、ここで食べた保存食、半分こしたね。少し砂の味がしたけど、今まで食べたどのオイルよりも温かかったよ」",
            "\"That day, we split our ration bars right here. It tasted a bit like sand, but it felt warmer than any oil I'd ever tasted.\"",
            "「那天，我们在这里把仅剩的口粮对半分着吃了呢。虽然带了点沙子的味道，却比我以往尝过的任何机油都要温热。」",
            "«Aquel día compartimos aquí nuestras raciones a medias. Sabía un poco a arena, pero me calentó más que ningún aceite que haya probado jamás.»"
        } },

        // 焚き火プロンプト＆チル呟き
        { "campfire_standup", new[] { "【 W / A / S / D 】または【 Space 】立ち上がる", "[W / A / S / D] or [Space] Stand Up", "【 W / A / S / D 】或【 空格 】站起身", "[W / A / S / D] o [Espacio] Levantarse" } },
        { "campfire_light", new[] { "🔥 【E / クリック】焚き火に火を点ける", "🔥 [E / Click] Light Campfire", "🔥 【E / 点击】点燃篝火", "🔥 [E / Clic] Encender la hoguera" } },
        { "campfire_need_points", new[] {
            "🔥 砂浜の焚き火（探索 10pt で点火可能 / 現在: {0}/{1} pt）",
            "🔥 Beach Campfire (Requires 10 Explored pts / Current: {0}/{1} pt)",
            "🔥 沙滩营火（探索度达 10pt 即可点燃 / 当前: {0}/{1} pt）",
            "🔥 Hoguera de la playa (Requiere 10 pts de exploración / Actual: {0}/{1} pt)"
        } },
        { "campfire_sit", new[] { "🪵 【E / クリック】丸太に腰掛ける（海と炎を眺める）", "🪵 [E / Click] Sit on Log (Gaze at fire & waves)", "🪵 【E / 点击】在圆木上小坐（眺望海浪与营火）", "🪵 [E / Clic] Sentarse en el tronco (Mirar el mar y el fuego)" } },
        { "campfire_stand_rust", new[] { "よいしょ！さぁ、冒険を続けよう！", "Heigh-ho! Alright, onward with our adventure!", "嘿咻！走吧，继续我们的冒险！", "¡Arriba! ¡Vamos a continuar la aventura!" } },

        { "……あったかいね、Float。", new[] {
            "……あったかいね、Float。",
            "...So warm, Float.",
            "……好温暖呀，Float。",
            "...Qué calorcito, Float."
        } },
        { "パチパチって、なんだか落ち着く音だね……", new[] {
            "パチパチって、なんだか落ち着く音だね……",
            "The crackling wood is such a soothing sound...",
            "柴火噼啪作响的声音，听着让人心底好平静……",
            "El chisporroteo de la leña es tan relajante..."
        } },
        { "波の音がきれいに聴こえるよ……", new[] {
            "波の音がきれいに聴こえるよ……",
            "The waves sound so clear and serene...",
            "潮水的声音听起来好清澈空灵呀……",
            "El sonido de las olas se escucha tan claro..."
        } },
        { "ずっとこうして、海を眺めていたいな……", new[] {
            "ずっとこうして、海を眺めていたいな……",
            "I could stay like this, watching the ocean forever...",
            "真想就这样，一直一直眺望着大海呢……",
            "Me quedaría así, mirando el mar para siempre..."
        } },
        { "ふぅ……少し休んでいこう……", new[] {
            "ふぅ……少し休んでいこう……",
            "Whew... let's take a peaceful breather...",
            "呼……稍微在这里休憩一会儿吧……",
            "Uf... descansemos un ratito..."
        } },
        { "Floatと一緒にいると、心があったかくなるよ。", new[] {
            "Floatと一緒にいると、心があったかくなるよ。",
            "Whenever I'm with you, Float, my core feels warm inside.",
            "和Float在一起的时候，整个核心都变得暖洋洋的。",
            "Estar contigo, Float, me llena el núcleo de calor."
        } },

        // 貝殻プロンプト
        { "prompt_shell_pickup", new[] { "【E】{0}を拾う", "[E] Pick up {0}", "【E】拾取 {0}", "[E] Recoger {0}" } },
        { "prompt_flotsam_inspect", new[] { "【E】調べる", "[E] Inspect", "【E】调查", "[E] Examinar" } },

        // 砂浜漂流物 Rust セリフ
        { "flotsam_2040_1_dialogue", new[] {
            "心拍数を測るバンドだ…身につけていると息が詰まりそうだね、Float",
            "A band that tracks your heartbeat… Wearing it feels like it would squeeze the breath out of you, Float.",
            "测心率的腕带啊……戴着它好像会喘不过气呢，Float。",
            "Una pulsera que mide el pulso… Llevarla puesta da la sensación de que te falta el aire, Float."
        } },

        // 貝殻拾得トースト＆セリフ
        { "わぁ、花びらみたいな貝殻だね！", new[] {
            "わぁ、花びらみたいな貝殻だね！",
            "Whoa, it looks just like a cherry petal!",
            "哇啊，像花瓣一样娇艳的贝壳呢！",
            "¡Uau, parece el pétalo de una flor!"
        } },
        { "波に磨かれて角がすべすべだ！宝石みたい…！", new[] {
            "波に磨かれて角がすべすべだ！宝石みたい…！",
            "Polished so smooth by waves! Like a genuine gem...!",
            "被海浪打磨得圆润光滑！简直像宝石一样…！",
            "¡El mar ha pulido sus bordes hasta dejarlos suaves! ¡Parece una gema...!"
        } },
        { "深海みたいな綺麗な青色！空に透かすとキラキラするよ！", new[] {
            "深海みたいな綺麗な青色！空に透かすとキラキラするよ！",
            "Such a deep ocean blue! Shines brilliantly against the sky!",
            "深邃如浩瀚汪洋般的纯净蔚蓝！迎着阳光看晶莹剔透呢！",
            "¡Un azul tan bonito como el mar profundo! ¡Brilla a contraluz contra el cielo!"
        } },
        { "黄金色に光ってる…！昔の太陽の光を閉じ込めたみたい！", new[] {
            "黄金色に光ってる…！昔の太陽の光を閉じ込めたみたい！",
            "Gleaming in gold...! Like ancient sunlight trapped inside!",
            "散发着金色的光晕…！仿佛把远古的暖阳封存在了里面！",
            "¡Brilla en color dorado...! ¡Como si guardara la luz del sol antiguo!"
        } },
        { "耳を当ててみて、Float！遠くの波の音が聞こえるよ！", new[] {
            "耳を当ててみて、Float！遠くの波の音が聞こえるよ！",
            "Hold it to your ear, Float! You can hear distant tides singing!",
            "快贴到耳边听听看，Float！能听见远方潮水的回音呢！",
            "¡Póntela en la oreja, Float! ¡Se oye el murmullo de las olas lejanas!"
        } },

        // 貝殻トースト
        { "shell_toast_picked", new[] {
            "<color=#{0}><b>✦ {1}</b></color> <color=#FFFFFF>を拾った</color>  <size=13><color=#FFE066>(所持: {2}個)</color></size>",
            "<color=#{0}><b>✦ {1}</b></color> <color=#FFFFFF>collected</color>  <size=13><color=#FFE066>(Own: {2})</color></size>",
            "<color=#{0}><b>✦ 拾获 {1}</b></color>  <size=13><color=#FFE066>(持有: {2}个)</color></size>",
            "<color=#{0}><b>✦ {1}</b></color> <color=#FFFFFF>recogido</color>  <size=13><color=#FFE066>(Tienes: {2})</color></size>"
        } },
        { "shell_toast_craftable", new[] {
            "<color=#{0}><b>✦ {1}</b></color> <color=#FFFFFF>を拾った！</color> <size=13><color=#66FFAA>({2}個)</color></size>\n<size=14><color=#FFE066>✨「{3}」が作れるよ！【Bキー】で工房を開こう！</color></size>",
            "<color=#{0}><b>✦ {1}</b></color> <color=#FFFFFF>collected!</color> <size=13><color=#66FFAA>({2})</color></size>\n<size=14><color=#FFE066>✨ \"{3}\" is ready to craft! Press [B] to open workshop!</color></size>",
            "<color=#{0}><b>✦ 拾获 {1}！</b></color> <size=13><color=#66FFAA>({2}个)</color></size>\n<size=14><color=#FFE066>✨ 可制作「{3}」啦！按【B键】打开工房吧！</color></size>",
            "<color=#{0}><b>✦ ¡{1}</b></color> <color=#FFFFFF>recogido!</color> <size=13><color=#66FFAA>({2})</color></size>\n<size=14><color=#FFE066>✨ ¡Puedes fabricar «{3}»! ¡Pulsa [B] para abrir el taller!</color></size>"
        } },
        { "shell_craft_ready_speech", new[] {
            "ピピピッ！「{0}」の素材が揃ったよ！\n【Bキー】で工房を開いて作ってみよう！",
            "Beep-beep! Materials for \"{0}\" are ready!\nPress [B] to open workshop and craft it!",
            "哔哔哔！制作「{0}」的素材已经集齐啦！\n按【B键】打开工坊来制作看看吧！",
            "¡Bip-bip! ¡Tenemos los materiales para «{0}»!\n¡Pulsa [B] para abrir el taller y fabricarlo!"
        } },
        { "shell_picked_generic_speech", new[] {
            "ピピッ！綺麗な「{0}」だね！",
            "Beep! Such a lovely \"{0}\"!",
            "哔哔！好漂亮的「{0}」呀！",
            "¡Bip! ¡Qué bonito es este «{0}»!"
        } },
        { "shell_not_enough_speech", new[] {
            "ピピッ！「{0}」を作るには「{1}」があと{2}個必要だよ！砂浜の波打ち際を探してみよう！",
            "Beep! To craft \"{0}\", we still need {2} more \"{1}\"! Let's search the beach shores!",
            "哔哔！想要制作「{0}」，还需要{2}个「{1}」呢！我们再去沙滩边缘找找看吧！",
            "¡Bip! Para fabricar «{0}», ¡aún necesitamos {2} más de «{1}»! ¡Busquemos en la orilla!"
        } },

        // 共通話者タグ
        { "✦ 相棒 Rust", new[] { "✦ 相棒 Rust", "✦ Companion Rust", "✦ 伙伴 Rust", "✦ Compañero Rust" } },
        { "✦ Float", new[] { "✦ Float", "✦ Float", "✦ Float", "✦ Float" } },

        // ==========================================
        // 巨大真鍮レバー・タワー操作指示＆バナー（Tower Lever Prompts & Banners）
        // ==========================================
        { "lever_prompt_pull", new[] {
            "【ここを押す / E / クリック】巨大真鍮レバーを引く",
            "[Click Here / E / Click] Pull Giant Brass Lever",
            "【按此处 / E / 鼠标点击】扳动巨型黄铜机关手柄",
            "[Pulsa aquí / E / Clic] Tirar de la palanca gigante de latón"
        } },
        { "lever_prompt_locked", new[] {
            "レバーはロック中（20ポイントが必要 / 現在: {0} pt）",
            "Lever is Locked (Requires 20 Points / Current: {0} pt)",
            "机关手柄处于锁定中（需要20探索点 / 当前: {0} pt）",
            "Palanca bloqueada (Requiere 20 puntos / Actual: {0} pt)"
        } },
        { "tower_unlocked_banner_warp", new[] {
            "✦ 20ポイント達成！タワー白亜テラス（レバー前）へワープ ✦\n💡 【Eキー】で巨大レバーを引いて天蓋を開放しよう！",
            "✦ 20 Points Reached! Warped to White Tower Terrace (At Lever) ✦\n💡 [E Key] Pull giant lever to open the sky canopy!",
            "✦ 达成20探索点！已传送到白垩露台机关前 ✦\n💡 【E键】扳动巨型机关开启天盖！",
            "✦ ¡20 puntos conseguidos! Teletransportado a la terraza (Frente a la palanca) ✦\n💡 ¡Pulsa [E] para tirar de la palanca y abrir la cúpula!"
        } },
        { "tower_unlocked_banner_explore", new[] {
            "✦ 20ポイント達成！中央タワーのレバーロック解除！ ✦\n💡 相棒Rustとコンパスに従って中央タワーへ向かおう！",
            "✦ 20 Points Reached! Central Tower Lever Unlocked! ✦\n💡 Follow Rust and the compass toward Central Tower!",
            "✦ 达成20探索点！中央高塔机关锁已解除！ ✦\n💡 跟随伙伴Rust与指南针前往中央高塔！",
            "✦ ¡20 puntos conseguidos! ¡Palanca de la torre desbloqueada! ✦\n💡 ¡Sigue a Rust y la brújula hacia la torre central!"
        } },
        { "banner_10pt_unlocked", new[] {
            "✦ 総合10ポイント達成！砂浜の焚き火＆工房解放！ ✦\n💡 砂浜の焚き火で暖まったり、【Bキー】でRust着せ替え工房を開いてみよう！",
            "✦ 10 Total Points! Beach Campfire & Workshop Unlocked! ✦\n💡 Warm up at the beach campfire or press [B] to open Rust's Workshop!",
            "✦ 达成10探索点！沙滩营火与外观工坊已解锁！ ✦\n💡 在沙滩营火旁取暖，或按【B键】打开Rust外观工坊吧！",
            "✦ ¡10 puntos conseguidos! ¡Hoguera de la playa y taller desbloqueados! ✦\n💡 ¡Caliéntate junto a la hoguera o pulsa [B] para abrir el taller de Rust!"
        } },
        { "banner_20pt_unlocked", new[] {
            "✦ 総合20ポイント達成！中央タワーのレバーロック解除！ ✦\n💡 中央タワーへ向かい、レバーを引いて天蓋を開放しよう！",
            "✦ 20 Total Points! Central Tower Lever Unlocked! ✦\n💡 Head to Central Tower and pull the lever to open the sky canopy!",
            "✦ 达成20探索点！中央高塔机关锁已解除！ ✦\n💡 前往中央高塔，扳动巨型机关开启天盖！",
            "✦ ¡20 puntos conseguidos! ¡Palanca de la torre desbloqueada! ✦\n💡 ¡Ve a la torre central y tira de la palanca para abrir la cúpula!"
        } },
        { "banner_piano_relic", new[] {
            "✨ ピアノの古代遺物を回収！ (+3 pt) ✨\n✦ 探索ポイント: {0} / {1} pt",
            "✨ Ancient Piano Relic Recovered! (+3 pt) ✨\n✦ Explored Points: {0} / {1} pt",
            "✨ 已回收钢琴古代遗物！ (+3 pt) ✨\n✦ 探索点数: {0} / {1} pt",
            "✨ ¡Reliquia antigua de piano recuperada! (+3 pt) ✨\n✦ Puntos de exploración: {0} / {1} pt"
        } },
        { "piano_duet_rust_0", new[] {
            "わぁ、素敵な連弾だね！カピタも嬉しそうに目を細めてるよ♪",
            "Wow, what a lovely duet! Capyta's eyes are narrowed happily♪",
            "哇啊，好美妙的合奏！卡皮塔也幸福地眯起了眼睛呢♪",
            "¡Uau, qué dúo tan bonito! Capyta entrecierra los ojos, feliz♪"
        } },
        { "piano_duet_rust_1", new[] {
            "ポロ〜ン…♪ 風に乗って、島のすみずみまで音が届いていくね",
            "Plink~...♪ The sound rides the wind to every corner of the island",
            "波噜〜…♪ 乐声乘着风，传到了岛的每个角落呢",
            "Plin~...♪ La música viaja con el viento hasta el último rincón de la isla"
        } },
        { "piano_duet_rust_2", new[] {
            "カピタ『ブヒヒ…♪（ご機嫌にリズムに合わせて鼻を鳴らした！）』",
            "Capyta: \"Oink-hehe...♪ (happily honked along to the rhythm!)\"",
            "卡皮塔『哼哧哧…♪（开心地跟着节奏哼起了鼻子！）』",
            "Capyta: «¡Oink-jeje...♪ (resopló al ritmo, de muy buen humor!)»"
        } },
        { "piano_duet_rust_3", new[] {
            "ふふっ、息ピッタリ！二人の音楽で島が優しさに包まれていくよ…！",
            "Hehe, perfect sync! Our music is wrapping the whole island in warmth...!",
            "呵呵，默契满分！两个人的音乐把整座岛都温柔包裹起来了…！",
            "Jeje, ¡a la perfección! Nuestra música envuelve la isla en ternura...!"
        } },
        { "banner_fan_activated", new[] {
            "✦ 上昇気流ファン起動！ ✦\n💡 送風ファンに乗って、崖上の黄金テラスへ跳ぼう！",
            "✦ Updraft Fan Activated! ✦\n💡 Ride the airflow fan and leap up to the Golden Terrace!",
            "✦ 上升气流风扇已启动！ ✦\n💡 乘上气流风扇，跃向悬崖上的黄金露台！",
            "✦ ¡Ventilador de corriente ascendente activado! ✦\n💡 ¡Sube a la corriente de aire y salta a la terraza dorada!"
        } },
        { "banner_crystal_relic", new[] {
            "✨ 古代の浮遊遺物を発見！ (+2 pt) ✨\n✦ 崖上の隠された宝箱を解放しました！",
            "✨ Ancient Floating Relic Discovered! (+2 pt) ✨\n✦ Unlocked the secret treasure chest above the cliff!",
            "✨ 发现了古代悬浮遗物！ (+2 pt) ✨\n✦ 解开了悬崖上方隐藏的珍宝箱！",
            "✨ ¡Reliquia flotante antigua descubierta! (+2 pt) ✨\n✦ ¡Cofre del tesoro secreto sobre el acantilado desbloqueado!"
        } },
        { "target_name_tower_lever", new[] {
            "白亜タワー（巨大レバー）",
            "White Tower (Giant Lever)",
            "白垩高塔（巨型机关）",
            "Torre Blanca (Palanca gigante)"
        } },
        { "target_name_wind_vent", new[] {
            "古代の風脈クリスタル",
            "Ancient Wind Vent Crystal",
            "古代风脉水晶",
            "Cristal de viento antiguo"
        } },

        { "20ポイント達成だよ！中央タワーのレバーロックが解除された！タワーへ行こう、ボクが案内するよ、Float！！", new[] {
            "20ポイント達成だよ！中央タワーのレバーロックが解除された！タワーへ行こう、ボクが案内するよ、Float！！",
            "20 points achieved! The central tower's lever is unlocked! Let's head to the tower, I'll guide you, Float!!",
            "达成20探索点啦！中央高塔的机关锁已解除！我们去高塔吧，我来给你引路，Float！！",
            "¡20 puntos conseguidos! ¡Se ha desbloqueado la palanca de la torre central! ¡Vamos a la torre, te guiaré, Float!!"
        } },
        { "ピピッ！探索ポイントが10pt貯まったよ！\n砂浜の焚き火に火を点けられるようになったよ！【Bキー】で工房を開いて新しいアクセサリーを作るのもおすすめだよ、Float！", new[] {
            "ピピッ！探索ポイントが10pt貯まったよ！\n砂浜の焚き火に火を点けられるようになったよ！【Bキー】で工房を開いて新しいアクセサリーを作るのもおすすめだよ、Float！",
            "Beep! 10 points gathered!\nYou can now light the beach campfire! Press [B] to open the workshop and craft new accessories, Float!",
            "哔哔！已积攒了10点探索点！\n现在可以点燃沙滩营火了！也推荐按【B键】打开工坊制作新饰品哦，Float！",
            "¡Bip! ¡Tenemos 10 puntos de exploración!\n¡Ya puedes encender la hoguera de la playa! ¡También te recomiendo pulsar [B] para abrir el taller y crear nuevos accesorios, Float!"
        } },
        { "ピピッ！探索ポイントが10pt貯まってるよ！\n砂浜の焚き火に火を点けられるよ！【Bキー】で工房を開いて新しいアクセサリーを作るのもおすすめだよ、Float！", new[] {
            "ピピッ！探索ポイントが10pt貯まってるよ！\n砂浜の焚き火に火を点けられるよ！【Bキー】で工房を開いて新しいアクセサリーを作るのもおすすめだよ、Float！",
            "Beep! You have 10 points!\nYou can light the beach campfire! Press [B] to open the workshop and craft new accessories, Float!",
            "哔哔！已积攒了10点探索点！\n可以点燃沙滩营火啦！也推荐按【B键】打开工坊制作新饰品哦，Float！",
            "¡Bip! ¡Tienes 10 puntos de exploración!\n¡Puedes encender la hoguera de la playa! ¡También te recomiendo pulsar [B] para abrir el taller y crear nuevos accesorios, Float!"
        } },
        { "ピピッ！パーツが20個揃ったよ！中央の白亜タワーへ行こう！", new[] {
            "ピピッ！パーツが20個揃ったよ！中央の白亜タワーへ行こう！",
            "Beep! All 20 points collected! Let's head to the Central White Tower!",
            "哔哔！零件已经集齐20点啦！我们去中央白垩高塔吧！",
            "¡Bip! ¡Tenemos 20 puntos reunidos! ¡Vamos a la Torre Blanca central!"
        } },
        { "ピピッ！ファンが回ったよ！\n送風孔に乗ると、崖の上の隠しテラス（黄金の光柱）へ運んでくれるよ、Float！", new[] {
            "ピピッ！ファンが回ったよ！\n送風孔に乗ると、崖の上の隠しテラス（黄金の光柱）へ運んでくれるよ、Float！",
            "Beep! The fan is spinning!\nRide the vent airflow and it will carry us up to the secret cliff terrace, Float!",
            "哔哔！风扇转动起来了！\n乘上送风孔的气流，就能把我们送上悬崖顶端的隐藏露台（黄金光柱）啦，Float！",
            "¡Bip! ¡El ventilador está girando!\n¡Sube al conducto de aire y nos elevará hasta la terraza secreta del acantilado, Float!"
        } },

        { "canopy_dive_hint", new[] {
            "【Space / クリック】空の裂け目へダイブ",
            "[Space / Click] Dive into Sky Rift",
            "【Space / 点击】向天空裂缝飞跃",
            "[Espacio / Clic] Zambullirse a la grieta del cielo"
        } },
        { "target_name_skydive_pillar", new[] {
            "大空ダイブ光柱",
            "Sky Dive Light Pillar",
            "高空跳伞光柱",
            "Pilar de salto al cielo"
        } },
        { "target_name_sanctuary_tower", new[] {
            "白亜の聖域タワー",
            "White Sanctuary Tower",
            "白垩圣域高塔",
            "Torre del Santuario Blanco"
        } },
        { "ピピッ！あの大空の光の柱からダイブしよう！", new[] {
            "ピピッ！あの大空の光の柱からダイブしよう！",
            "Beep! Let's dive from that great pillar of light into the sky!",
            "哔哔！从那道天空光柱飞跃而下吧！",
            "¡Bip! ¡Zambullámonos desde ese gran pilar de luz hacia el cielo!"
        } },
        { "ピピッ！島の中心の白亜タワーがあっちだよ！", new[] {
            "ピピッ！島の中心の白亜タワーがあっちだよ！",
            "Beep! The White Tower at the center of the island is that way!",
            "哔哔！岛屿中央的白垩高塔就在那个方向哦！",
            "¡Bip! ¡La Torre Blanca del centro de la isla está por allí!"
        } },
        { "field_lesson_prompt_need", new[] {
            "【E】次に必要なことを聞く",
            "[E] Ask what to do next",
            "【E】询问接下来的目标",
            "[E] Preguntar qué hacer ahora"
        } },
        { "field_lesson_prompt_tide", new[] {
            "【E】潮目の標に触れる",
            "[E] Touch Tide Marker",
            "【E】触碰潮目之标",
            "[E] Tocar el poste de la marea"
        } },
        { "field_lesson_prompt_canopy", new[] {
            "【E】木漏れ日の標に触れる",
            "[E] Touch Sunbeam Marker",
            "【E】触碰林隙之标",
            "[E] Tocar el poste del bosque"
        } }
,

        { "いっくよー！大空へダイブ！！", new[] {
            "いっくよー！大空へダイブ！！",
            "Here we go! Diving into the great sky!!",
            "要上啦！向着苍穹纵情飞跃！！",
            "¡¡Allá vamos!! ¡¡Zambulléndonos en el gran cielo!!"
        } },
        { "わっ、水面スレスレ！すごいスピード！！", new[] {
            "わっ、水面スレスレ！すごいスピード！！",
            "Whoa, skimming right above the water! Incredible speed!!",
            "哇，贴近水面了！好惊人的速度！！",
            "¡Uau, rozando el agua! ¡¡Qué velocidad increíble!!"
        } },
        { "わぁぁ！ピアノの上に光るパーツがあったよ！これで3ポイントゲットだね！", new[] {
            "わぁぁ！ピアノの上に光るパーツがあったよ！これで3ポイントゲットだね！",
            "Whoa! There was a shining relic on the piano! That's 3 whole points!",
            "哇啊！钢琴上竟然有发光的遗物！这样就拿到3点啦！",
            "¡Uauuu! ¡Había una reliquia brillante en el piano! ¡Son 3 puntos enteros!"
        } },
        { "あ、砂浜の焚き火だ！10ポイント貯まったから火が点けられるよ！\n【Bキー】の工房で何かクラフトしてから、ここで温まりに行こうよ、Float！", new[] {
            "あ、砂浜の焚き火だ！10ポイント貯まったから火が点けられるよ！\\n【Bキー】の工房で何かクラフトしてから、ここで温まりに行こうよ、Float！",
            "Ah, the beach campfire! We have 10 points so we can light it now!\\nLet's craft something at the workshop [B] and warm up here, Float!",
            "啊，是沙滩营火！已经攒了10点所以可以点燃了！\\n按【B键】去工坊制作点饰品，再来这里取暖吧，Float！",
            "¡Ah, la hoguera de la playa! ¡Tenemos 10 puntos, así que ya podemos encenderla!\\n¡Fabriquemos algo en el taller [B] y vengamos a calentarnos, Float!"
        } },
        { "わぁ……！火が点いた！すごくあったかいね……！\n【Bキー】の工房で新しいアクセサリーを作って、ここでゆっくり眺めるのも最高だね！", new[] {
            "わぁ……！火が点いた！すごくあったかいね……！\\n【Bキー】の工房で新しいアクセサリーを作って、ここでゆっくり眺めるのも最高だね！",
            "Whoa...! The fire is lit! It's so warm and cozy...!\\nCrafting new accessories at the workshop [B] and relaxing here is the best!",
            "哇啊……！火焰点燃了！真的好温暖啊……！\\n按【B键】在工坊制作新饰品，然后在这里悠闲欣赏真是太棒了！",
            "¡Uau...! ¡El fuego está encendido! ¡Es tan cálido y acogedor...!\\n¡Fabricar nuevos accesorios en el taller [B] y relajarse aquí es lo mejor!"
        } },
        { "Float、いい風が吹いてるよ！", new[] {
            "Float、いい風が吹いてるよ！",
            "Float, a wonderful wind is blowing!",
            "Float，迎面吹来的风好舒服呀！",
            "¡Float, sopla un viento maravilloso!"
        } },
        { "どこまでも飛べるね、Float！", new[] {
            "どこまでも飛べるね、Float！",
            "We can fly anywhere like this, Float!",
            "感觉能这样飞向任何地方呢，Float！",
            "¡Podemos volar hasta el fin del mundo, Float!"
        } },
        { "翼を並べて飛ぶの、大好きだよ……♪", new[] {
            "翼を並べて飛ぶの、大好きだよ……♪",
            "I love flying side by side with you...♪",
            "最喜欢像这样并肩翱翔了……♪",
            "Me encanta volar ala con ala a tu lado...♪"
        } },
        { "風に乗って、どこまでも行けそう", new[] {
            "風に乗って、どこまでも行けそう",
            "Riding the wind, feels like we can go anywhere.",
            "乘着这阵风，仿佛哪里都能到达。",
            "Sobre este viento parece que podemos llegar donde sea."
        } },
        { "島を見下ろすと、すごく綺麗だね", new[] {
            "島を見下ろすと、すごく綺麗だね",
            "Looking down at the island, it's so beautiful.",
            "俯瞰整座岛屿，真的很美丽呢。",
            "Mirar la isla desde aquí arriba es precioso."
        } },
        { "わぁ…！風が気持ちいいね、Float", new[] {
            "わぁ…！風が気持ちいいね、Float",
            "Whoa...! The breeze feels so refreshing, Float.",
            "哇……！微风吹拂着好惬意呀，Float。",
            "¡Uau...! La brisa se siente tan agradable, Float."
        } },
        { "この高度、ちょうどいいね", new[] {
            "この高度、ちょうどいいね",
            "This altitude feels just right.",
            "这个飞行高度刚刚好呢。",
            "Esta altitud es perfecta."
        } },
        { "ヒューッ……いいフライトだ", new[] {
            "ヒューッ……いいフライトだ",
            "Whew... what a great flight.",
            "呼……真是一次棒极了的飞行。",
            "Uf... qué buen vuelo."
        } },
        { "一緒だと大空がもっと綺麗だね…♪", new[] {
            "一緒だと大空がもっと綺麗だね…♪",
            "The sky looks even more beautiful when we're together...♪",
            "只要跟你在一起，苍穹就变得更加美丽了呢…♪",
            "El cielo es aún más hermoso cuando estamos juntos...♪"
        } },
        { "自由な空……これが、これからずっと続くといいな", new[] {
            "自由な空……これが、これからずっと続くといいな",
            "This free sky... I hope it lasts forever.",
            "自由的天空……希望这份自由能永远持续下去。",
            "Este cielo libre... Ojalá dure para siempre."
        } },
        { "昔は倉庫の棚で眠ってた。Floatが連れ出した日、いちばん覚えてる", new[] {
            "昔は倉庫の棚で眠ってた。Floatが連れ出した日、いちばん覚えてる",
            "I used to sleep on a warehouse shelf. The day you took me out is what I remember most.",
            "过去我总是在仓库的架子上沉睡。你带我离开的那一天，我一直记得最清楚。",
            "Solía dormir en la estantería de un almacén. El día que me sacaste es lo que mejor recuerdo."
        } },
        { "最適化の街では、僕の声も『不要』ってラベルだったんだ", new[] {
            "最適化の街では、僕の声も『不要』ってラベルだったんだ",
            "In the optimized city, even my voice was labeled 'unnecessary'.",
            "在那个被极致最优化的都市里，连我的声音都被贴上了『多余』的标签。",
            "En la ciudad optimizada, incluso mi voz tenía la etiqueta de 'innecesaria'."
        } },
        { "塩水に濡れた最初の夜、怖かった。でもFloatの手が温かかった", new[] {
            "塩水に濡れた最初の夜、怖かった。でもFloatの手が温かかった",
            "That first night soaked in salt water was scary. But your hands were so warm, Float.",
            "浸在海水里的第一个夜晚真的很害怕。但是Float的手好温暖。",
            "Aquella primera noche empapado en agua salada tuve miedo. Pero tus manos eran muy cálidas, Float."
        } },
        { "スクラップ寸前の僕を、Floatは『相棒』って呼んでくれた", new[] {
            "スクラップ寸前の僕を、Floatは『相棒』って呼んでくれた",
            "When I was almost scrap metal, you called me your 'partner', Float.",
            "在快要变成废铁的时候，是Float叫了我一声『伙伴』。",
            "Cuando estaba a punto de ser chatarra, me llamaste 'compañero', Float."
        } },
        { "あの波……逃げてきた海の音、まだ耳の奥に残ってる", new[] {
            "あの波……逃げてきた海の音、まだ耳の奥に残ってる",
            "Those waves... the sound of the ocean we escaped across still lingers in my ears.",
            "那些巨浪……我们逃亡时渡过的海浪声，至今仍在耳畔回响。",
            "Aquellas olas... el sonido del mar por el que escapamos aún resuena en mis oídos."
        } },
        { "管理されるだけの日々より、今の不確かさのほうが好き", new[] {
            "管理されるだけの日々より、今の不確かさのほうが好き",
            "I prefer our uncertain days right now over a life of just being controlled.",
            "比起被严密管控的日子，我更喜欢现在充满未知的生活。",
            "Prefiero la incertidumbre de hoy que una vida donde todo estaba controlado."
        } },
        { "昔の記憶データ、ところどころ欠落してる。でもFloatの顔は鮮明だよ", new[] {
            "昔の記憶データ、ところどころ欠落してる。でもFloatの顔は鮮明だよ",
            "Some of my old memory data is missing. But your face is always crystal clear, Float.",
            "过去的记忆数据虽然有些残缺了。但Float的面容一直都非常清晰哦。",
            "Faltan algunos datos de mi memoria antigua. Pero tu rostro es totalmente nítido, Float."
        } },
        { "翼が直ったら、蒼い空のてっぺんまで行こうね", new[] {
            "翼が直ったら、蒼い空のてっぺんまで行こうね",
            "When my wings are all fixed up, let's fly all the way to the top of the blue sky!",
            "等机翼修好了，我们一起飞到蓝天的最顶端吧！",
            "¡Cuando mis alas estén arregladas, volemos hasta la cima del cielo azul!"
        } },
        { "天蓋の向こう……どんな景色が待ってるんだろう", new[] {
            "天蓋の向こう……どんな景色が待ってるんだろう",
            "Beyond the canopy... I wonder what kinds of sights are waiting for us.",
            "天穹的另一侧……究竟有着怎样的景色在等待着我们呢。",
            "Más allá de la cúpula... qué clase de paisajes nos estarán esperando."
        } },
        { "これからも、Floatのそばで飛びたい", new[] {
            "これからも、Floatのそばで飛びたい",
            "I always want to keep flying beside you, Float.",
            "今后我也想一直陪伴在Float的身旁飞行。",
            "Quiero seguir volando siempre a tu lado, Float."
        } },
        { "パーツが揃ったら、もっと遠くまで案内できるよ", new[] {
            "パーツが揃ったら、もっと遠くまで案内できるよ",
            "Once we gather the parts, I can guide you even farther!",
            "等零件集齐了，我就能为你引路去更远的地方啦！",
            "¡En cuanto reunamos las piezas, podré guiarte aún más lejos!"
        } },
        { "いつか、怖がらずに笑いながら飛べるようになりたい", new[] {
            "いつか、怖がらずに笑いながら飛べるようになりたい",
            "Someday, I want to fly with a big smile, completely fearless.",
            "总有一天，我也想毫不畏惧地笑着在空中飞翔。",
            "Algún día quiero volar sonriendo, sin ningún temor."
        } },
        { "この島のあとにも、冒険はあるのかな……ワクワクする", new[] {
            "この島のあとにも、冒険はあるのかな……ワクワクする",
            "Will there be more adventures after this island...? I'm so excited!",
            "在这座岛屿之后，还会有新的冒险吗……真让人期待！",
            "¿Habrá más aventuras después de esta isla...? ¡Qué emoción!"
        } },
        { "Floatと見つけたもの、全部覚えておくね。未来の僕の宝物だ", new[] {
            "Floatと見つけたもの、全部覚えておくね。未来の僕の宝物だ",
            "I'll remember everything we found together. They're my treasures for the future.",
            "和Float一起发现的所有东西，我都会牢牢记住。那都是我未来的宝物。",
            "Recordaré todo lo que encontramos juntos. Son mis tesoros para el futuro."
        } },
        { "今日の関節、なめらかだよ。油のおかげかな", new[] {
            "今日の関節、なめらかだよ。油のおかげかな",
            "My joints feel so smooth today. Must be thanks to the oil!",
            "今天的关节运转很顺畅呢。多亏了润滑油的保养吧！",
            "Mis articulaciones van muy suaves hoy. ¡Será gracias al aceite!"
        } },
        { "ピロッ……ファンの回転、気持ちいい音してる", new[] {
            "ピロッ……ファンの回転、気持ちいい音してる",
            "Beep... my fans are spinning with such a pleasant sound.",
            "哔噜……风扇转动的声音听起来好惬意。",
            "Bip... mis ventiladores giran con un sonido muy agradable."
        } },
        { "センサーは快調。潮の匂いまで拾えてるよ", new[] {
            "センサーは快調。潮の匂いまで拾えてるよ",
            "Sensors in tip-top shape. I can even catch the scent of the tide!",
            "传感器状态绝佳。甚至连海潮的气息都能捕捉到呢！",
            "Sensores en perfecto estado. ¡Hasta capto el olor de la marea!"
        } },
        { "少し眠い……でも、そばにいると元気が出る", new[] {
            "少し眠い……でも、そばにいると元気が出る",
            "A little sleepy... but staying by your side gives me energy.",
            "稍微有点困了……但只要待在你身旁，就觉得充满精神。",
            "Un poco soñoliento... pero estar a tu lado me llena de energía."
        } },
        { "ギアが軽やか。今ならどこまでもついていける", new[] {
            "ギアが軽やか。今ならどこまでもついていける",
            "My gears feel so light. I can follow you anywhere right now!",
            "齿轮运转好轻快。现在不管去哪里我都能跟上你！",
            "Los engranajes van ligeros. ¡Ahora puedo seguirte a cualquier parte!"
        } },
        { "胸のコアが温かい。Floatのペース、ちょうどいい", new[] {
            "胸のコアが温かい。Floatのペース、ちょうどいい",
            "My core feels so warm. Your pace is just right, Float.",
            "胸口的核心暖暖的。Float的步伐节奏刚刚好呢。",
            "Mi núcleo está calentito. Tu ritmo es perfecto, Float."
        } },
        { "バランスいいね。転びそうな気配、いまはないよ", new[] {
            "バランスいいね。転びそうな気配、いまはないよ",
            "Great balance! No sign of tumbling at all right now.",
            "平衡感很好哦。现在完全没有要跌倒的迹象呢。",
            "¡Buen equilibrio! No hay señales de tropezar en absoluto."
        } },
        { "えへへ……Floatと歩くの、楽しい", new[] {
            "えへへ……Floatと歩くの、楽しい",
            "Ehehe... walking with you is so much fun, Float.",
            "诶嘿嘿……和Float一起漫步好开心呀。",
            "Jejeje... caminar contigo es muy divertido, Float."
        } },
        { "今、すごく安心してる。ここにいていいんだって感じ", new[] {
            "今、すごく安心してる。ここにいていいんだって感じ",
            "I feel so peaceful right now. Like it's truly okay for me to be here.",
            "现在心里非常踏实。感觉自己真正属于这里一样。",
            "Ahora mismo me siento muy tranquilo. Como si realmente perteneciera aquí."
        } },
        { "ピキーッ……嬉しい。言葉にすると恥ずかしいけど", new[] {
            "ピキーッ……嬉しい。言葉にすると恥ずかしいけど",
            "Beep-whistle... I'm so happy. A little shy to say it out loud, though!",
            "哔叽——……好开心呀。虽然说出口有点难为情！",
            "Bip-pío... Qué feliz soy. ¡Aunque me da un poco de vergüenza decirlo!"
        } },
        { "風も光も、全部が優しいね。今日はいい日だ", new[] {
            "風も光も、全部が優しいね。今日はいい日だ",
            "The wind, the sunlight, everything is so gentle. Today is a great day.",
            "微风与阳光，一切都如此温柔。今天真是美好的一天。",
            "El viento, la luz, todo es tan suave. Hoy es un día maravilloso."
        } },
        { "Floatの足音、好き。リズムが落ち着く", new[] {
            "Floatの足音、好き。リズムが落ち着く",
            "I love the sound of your footsteps, Float. The rhythm is so calming.",
            "我很喜欢Float的脚步声。那阵节奏让人感到格外安心。",
            "Me gusta el sonido de tus pasos, Float. Su ritmo me tranquiliza mucho."
        } },
        { "見つかるたびに、胸がふくらむ。幸せのセンサーが鳴ってる", new[] {
            "見つかるたびに、胸がふくらむ。幸せのセンサーが鳴ってる",
            "Every time we find something, my chest swells. My happiness sensor is pinging!",
            "每次找到新东西，心里就充满成就感。幸福传感器一直在响呢！",
            "Cada vez que encontramos algo, se me ensancha el pecho. ¡Mi sensor de felicidad pita!"
        } },
        { "一緒にいるだけで、充電されてるみたい", new[] {
            "一緒にいるだけで、充電されてるみたい",
            "Just being with you feels like I'm getting recharged.",
            "只要待在一起，就仿佛正在被充电一样。",
            "Solo con estar contigo siento como si me recargara."
        } },
        { "ピピッ…砂の中に、光るものないかな？", new[] {
            "ピピッ…砂の中に、光るものないかな？",
            "Beep... I wonder if there's something shining in the sand?",
            "哔哔……沙子里会不会埋着发光的东西呢？",
            "Bip... ¿Habrá algo brillante entre la arena?"
        } },
        { "楽しい探しものしよう。ギアのかけら、どこだろ", new[] {
            "楽しい探しものしよう。ギアのかけら、どこだろ",
            "Let's go on a fun treasure hunt! Where could those gear fragments be?",
            "来寻找有趣的东西吧。齿轮碎片会藏在哪里呢？",
            "¡Vamos a buscar tesoros! ¿Dónde estarán los fragmentos de engranaje?"
        } },
        { "あそこに光柱がある気がする。行ってみる？", new[] {
            "あそこに光柱がある気がする。行ってみる？",
            "I feel like there's a pillar of light over there. Want to go check it out?",
            "感觉那边好像有一道光柱。要过去看看吗？",
            "Me da la sensación de que hay un pilar de luz allí. ¿Vamos a mirar?"
        } },
        { "砂浜を歩くの、冒険の入口みたいでワクワクする", new[] {
            "砂浜を歩くの、冒険の入口みたいでワクワクする",
            "Walking on the sandy beach feels like the entrance to an adventure, so exciting!",
            "漫步在沙滩上，就像是踏入了冒险的序章一样，真让人兴奋！",
            "Caminar por la playa de arena parece el inicio de una aventura, ¡qué emocionante!"
        } },
        { "カピタに会うのも楽しいし、遺物探しもしよう", new[] {
            "カピタに会うのも楽しいし、遺物探しもしよう",
            "Meeting Capyta is fun, and let's search for relics too!",
            "去见见卡皮塔很有趣，同时也去找找古代遗物吧！",
            "Ver a Capyta es divertido, ¡y busquemos reliquias también!"
        } },
        { "リングをくぐると風が歌うよ。油ももらえるし", new[] {
            "リングをくぐると風が歌うよ。油ももらえるし",
            "The wind sings when you pass through rings. And we get oil too!",
            "穿过光环时风儿会歌唱呢。而且还能获得润滑油！",
            "El viento canta al atravesar los anillos. ¡Y también nos dan aceite!"
        } },
        { "草むらの奥、何か隠れてないかな？偵察するよ", new[] {
            "草むらの奥、何か隠れてないかな？偵察するよ",
            "Is there something hidden deep in the grass? I'll scout ahead!",
            "草丛深处会不会藏着什么呢？我来侦察一下！",
            "¿Habrá algo escondido entre la hierba? ¡Voy a explorar!"
        } },
        { "最初のパーツ、きっとすぐ見つかる。僕が手伝う", new[] {
            "最初のパーツ、きっとすぐ見つかる。僕が手伝う",
            "We'll find the first part in no time. I'll help you!",
            "第一个零件肯定很快就能找到的。我会全力帮忙！",
            "Seguro que encontramos la primera pieza enseguida. ¡Yo te ayudo!"
        } },
        { "ピピッ…何か光るものがあるかな？", new[] {
            "ピピッ…何か光るものがあるかな？",
            "Beep... Is there something shining around here?",
            "哔哔……周围会有发光的东西吗？",
            "Bip... ¿Habrá algo brillante por aquí?"
        } },
        { "リングをくぐると油も増えるよ。くぐってみよう", new[] {
            "リングをくぐると油も増えるよ。くぐってみよう",
            "Passing through rings gives us more oil. Let's fly through them!",
            "穿过光环油量也会增加哦。试着多穿过几个吧！",
            "Atravesar anillos nos da más aceite. ¡Probemos a atravesarlos!"
        } },
        { "タワーの方、まだ遠く見えるね。少しずつ近づこう", new[] {
            "タワーの方、まだ遠く見えるね。少しずつ近づこう",
            "The tower still looks far away. Let's make our way closer bit by bit.",
            "高塔看起来还挺遥远的呢。我们一步步慢慢靠近吧。",
            "La torre aún parece lejana. Vayamos acercándonos poco a poco."
        } },
        { "楽しいこと探そう。未踏の岸辺、まだあるはず", new[] {
            "楽しいこと探そう。未踏の岸辺、まだあるはず",
            "Let's look for fun things! There must still be shores we haven't explored.",
            "去找点好玩的事吧。一定还有尚未涉足的海岸！",
            "¡Busquemos cosas divertidas! Seguro que aún quedan costas inexploradas."
        } },
        { "森の音が変わる場所、何かありそうだよ", new[] {
            "森の音が変わる場所、何かありそうだよ",
            "Where the sounds of the forest change, there might be something waiting!",
            "森林声音发生变化的地方，似乎藏着什么东西呢！",
            "Donde cambia el sonido del bosque, ¡seguro que hay algo!"
        } },
        { "崖の上から滑空したら、新しい景色が見えるかも", new[] {
            "崖の上から滑空したら、新しい景色が見えるかも",
            "If we glide from atop the cliff, maybe we'll discover a brand new view!",
            "如果从悬崖顶端滑翔的话，或许能看到崭新的景色呢！",
            "Si planeamos desde lo alto del acantilado, ¡quizá veamos un nuevo paisaje!"
        } },
        { "調子はいいよ。次の遺物、一緒に探そう", new[] {
            "調子はいいよ。次の遺物、一緒に探そう",
            "I'm feeling great. Let's go look for the next relic together!",
            "状态好极了。下一件遗物，我们一起去找找吧！",
            "¡Me siento genial! ¡Busquemos la siguiente reliquia juntos!"
        } },
        { "光る柱の方角、覚えておくね。道しるべだ", new[] {
            "光る柱の方角、覚えておくね。道しるべだ",
            "I'll remember the direction of the light pillar. It's our guiding star!",
            "光柱的方向我已经记住了。那就是我们的指路明灯！",
            "Recordaré la dirección del pilar de luz. ¡Es nuestro faro guía!"
        } }
,

        { "guide_hud_opening", new[] {
            "【WASD / 矢印キー】移動　【マウス】視点　【Shift】走る　【Space長押し】滑空　【R】リセット",
            "[WASD / Arrow Keys] Move  [Mouse] Look  [Shift] Sprint  [Hold Space] Glide  [R] Reset",
            "【WASD / 方向键】移动　【鼠标】视角　【Shift】奔跑　【长按Space】滑翔　【R】重置",
            "[WASD / Flechas] Moverse  [Ratón] Mirar  [Shift] Correr  [Mantener Espacio] Planear  [R] Reiniciar"
        } },
        { "guide_hud_gameplay", new[] {
            "【WASD】移動　【マウス】視点　【Shift】走る　【Space】滑空　【ESC】設定→HUD表示　【Tab】詳細ナビ固定　【Q】案内＋10秒詳細",
            "[WASD] Move  [Mouse] Look  [Shift] Sprint  [Space] Glide  [ESC] Settings → HUD  [Tab] pin detail nav  [Q] guide + 10s detail",
            "【WASD】移动　【鼠标】视角　【Shift】奔跑　【Space】滑翔　【ESC】设置→HUD　【Tab】固定详细导航　【Q】引路+10秒详细",
            "[WASD] Moverse  [Ratón] Mirar  [Shift] Correr  [Espacio] Planear  [ESC] Ajustes → HUD  [Tab] fijar detalle  [Q] guía + 10 s detalle"
        } },
        { "save_notification_title", new[] {
            "SAVEしました",
            "GAME SAVED",
            "已保存游戏",
            "PARTIDA GUARDADA"
        } },
        { "save_notification_sub", new[] {
            "遺物パーツ: {0}個 ／ Rust常備油: {1}個（Rustと絆を結びました）",
            "Relic Parts: {0} / Rust Oil: {1} (Bonded with Rust)",
            "古代零件: {0}个 ／ Rust常备机油: {1}罐（与Rust建立深厚羁绊）",
            "Piezas antiguas: {0} / Aceite de Rust: {1} (Vínculo forjado con Rust)"
        } },
        { "save_load_restored_title", new[] {
            "セーブデータを復元しました",
            "Save data restored",
            "已恢复存档",
            "Partida restaurada"
        } },
        { "save_load_restored_sub", new[] {
            "前回の冒険記録（パーツ: {0}個）を読み込みました",
            "Loaded your last adventure (Parts: {0})",
            "已读取上次冒险记录（零件: {0}个）",
            "Se cargó tu última aventura (Piezas: {0})"
        } },
        { "save_new_adventure_title", new[] {
            "新規冒険を開始しました",
            "New adventure started",
            "已开始新冒险",
            "Nueva aventura iniciada"
        } },
        { "save_new_adventure_sub", new[] {
            "時代背景ボードからスタート",
            "Starting from the era backstory board",
            "从时代背景板开始",
            "Comienza desde el panel de contexto histórico"
        } },
        { "SAVEしました", new[] {
            "SAVEしました",
            "GAME SAVED",
            "已保存游戏",
            "PARTIDA GUARDADA"
        } },
        { "サバイバルケース", new[] {
            "サバイバルケース",
            "Survival Case",
            "求生箱",
            "Caja de Supervivencia"
        } },
        { "わあ！きれいなお花がたくさん咲いてる！いい香りだね、Float♪", new[] {
            "わあ！きれいなお花がたくさん咲いてる！いい香りだね、Float♪",
            "Whoa! So many beautiful flowers blooming! They smell wonderful, Float♪",
            "哇啊！盛开了好多美丽的花朵！好香呀，Float♪",
            "¡Uau! ¡Tantas flores hermosas floreciendo! Huelen genial, Float♪"
        } },
        { "ピピッ♪ お花畑だ！風に揺れてキラキラ光ってるよ！", new[] {
            "ピピッ♪ お花畑だ！風に揺れてキラキラ光ってるよ！",
            "Beep♪ A flower meadow! They're swaying in the breeze and sparkling!",
            "哔哔♪ 是花田呀！在风中摇曳闪烁着微光呢！",
            "¡Bip♪ Un campo de flores! ¡Se mecen con la brisa y brillan!"
        } },
        { "あたたかいね…Float、お花がとっても綺麗だよ♥", new[] {
            "あたたかいね…Float、お花がとっても綺麗だよ♥",
            "So warm and cozy... Float, the flowers are so lovely♥",
            "好温暖呀…Float，花朵真漂亮呢♥",
            "Qué calorcito... Float, las flores son preciosas♥"
        } },
        { "わあ…カピタがピアノを弾いてるよ！優しいメロディだね♪", new[] {
            "わあ…カピタがピアノを弾いてるよ！優しいメロディだね♪",
            "Whoa... Capyta is playing the piano! What a gentle melody♪",
            "哇啊…卡皮塔在弹钢琴呢！旋律真温柔呀♪",
            "¡Uau...! ¡Capyta está tocando el piano! Qué melodía tan dulce♪"
        } },
        { "ピロリン♪ 聴いてFloat！ピアノの音が風に乗って聴こえるよ…！", new[] {
            "ピロリン♪ 聴いてFloat！ピアノの音が風に乗って聴こえるよ…！",
            "Beep-chime♪ Listen, Float! The piano melody is drifting on the wind...!",
            "哔噜噜♪ 快听Float！钢琴的声音乘着风飘过来了哦…！",
            "¡Bip-chime♪ Escucha, Float! El sonido del piano flota en el viento...!"
        } },
        { "プヒヒ…カピタさん上手だね！Rustもこの曲大好きだよ♪", new[] {
            "プヒヒ…カピタさん上手だね！Rustもこの曲大好きだよ♪",
            "Puhehe... Capyta is so good! Rust loves this song too♪",
            "噗嘻嘻…卡皮塔弹得真好！Rust也很喜欢这首曲子呢♪",
            "Jijiji... ¡Capyta toca muy bien! A Rust también le encanta esta canción♪"
        } },
        { "……ギアが少し重い。油か手当てがあると助かるよ", new[] {
            "……ギアが少し重い。油か手当てがあると助かるよ",
            "...My gears feel a bit heavy. Some oil or care would really help.",
            "……齿轮稍微有点沉重。如果能有点机油或保养就太好了。",
            "...Los engranajes van algo pesados. Un poco de aceite o cuidado me ayudaría."
        } },
        { "ピロッ……調子が落ちてる。【E】で整備して", new[] {
            "ピロッ……調子が落ちてる。【E】で整備して",
            "Beep... My condition is dropping. Please service me with [E].",
            "哔噜……机能有些下降了。请按【E键】为我保养。",
            "Bip... Mi rendimiento está bajando. Hazme mantenimiento con [E]."
        } },
        { "油がほしいな。カピタのところか、地面の油でも", new[] {
            "油がほしいな。カピタのところか、地面の油でも",
            "I need some oil. Either from Capyta or from the ground pools.",
            "想要机油呢。去卡皮塔那里或者地面上的油坑都可以。",
            "Necesito aceite. Ya sea de Capyta o de los charcos del suelo."
        } },
        { "関節がきしむ……でも、Floatがそばなら平気", new[] {
            "関節がきしむ……でも、Floatがそばなら平気",
            "My joints are creaking... But as long as you're by my side, Float, I'm okay.",
            "关节有些嘎吱作响……但只要Float在身边我就没事。",
            "Me crujen las articulaciones... Pero si estás a mi lado, Float, estoy bien."
        } },
        { "古代の推進黄金ギア", new[] {
            "古代の推進黄金ギア",
            "Ancient Propulsion Gold Gear",
            "古代推进黄金齿轮",
            "Engranaje Dorado de Propulsión Antigua"
        } },
        { "古代の推進ギア", new[] {
            "古代の推進ギア",
            "Ancient Propulsion Gear",
            "古代推进齿轮",
            "Engranaje de Propulsión Antigua"
        } },
        { "耐熱スタビライザー", new[] {
            "耐熱スタビライザー",
            "Thermal Stabilizer",
            "耐热稳定翼",
            "Estabilizador Térmico"
        } },
        { "海風のエネルギーコア", new[] {
            "海風のエネルギーコア",
            "Sea Breeze Energy Core",
            "海风能量核心",
            "Núcleo de Energía de la Brisa Marina"
        } },
        { "潮騒のバランスリング", new[] {
            "潮騒のバランスリング",
            "Tide Balance Ring",
            "潮汐平衡环",
            "Anillo de Equilibrio de Mareas"
        } },
        { "反重力サスペンション", new[] {
            "反重力サスペンション",
            "Anti-Gravity Suspension",
            "反重力悬挂",
            "Suspensión Antigravedad"
        } },
        { "跳躍反重力コア", new[] {
            "跳躍反重力コア",
            "Leap Anti-Gravity Core",
            "跳跃反重力核心",
            "Núcleo Antigravedad de Salto"
        } },
        { "清流の共鳴プリズム", new[] {
            "清流の共鳴プリズム",
            "Clear Stream Resonance Prism",
            "清流共鸣棱镜",
            "Prisma de Resonancia del Arroyo"
        } },
        { "水冷コンデンサー", new[] {
            "水冷コンデンサー",
            "Water-Cooled Condenser",
            "水冷冷凝器",
            "Condensador Refrigerado por Agua"
        } },
        { "高周波ソナークリスタル", new[] {
            "高周波ソナークリスタル",
            "High-Frequency Sonar Crystal",
            "高频声呐水晶",
            "Cristal de Sónar de Alta Frecuencia"
        } },
        { "古代探知コア", new[] {
            "古代探知コア",
            "Ancient Detection Core",
            "古代探测核心",
            "Núcleo de Detección Antigua"
        } },
        { "高空ジェットスラスター", new[] {
            "高空ジェットスラスター",
            "High-Altitude Jet Thruster",
            "高空喷气推进器",
            "Propulsor a Chorro de Gran Altitud"
        } },
        { "天蓋開放マスターコア", new[] {
            "天蓋開放マスターコア",
            "Canopy Release Master Core",
            "天穹开启主控核心",
            "Núcleo Maestro de Apertura de la Cúpula"
        } },
        { "純白のこ巻貝", new[] {
            "純白の小巻貝",
            "Pure White Conch",
            "纯白小卷贝",
            "Caracola Blanca Pura"
        } },
        { "guide_hud_gliding", new[] {
            "【A / D】旋回　【W】ダイブ　【S】フレア　【Space長押し】滑空",
            "[A / D] Turn  [W] Dive  [S] Flare  [Hold Space] Glide",
            "【A / D】转向　【W】俯冲　【S】仰起减速　【长按Space】滑翔",
            "[A / D] Girar  [W] Picado  [S] Nivelar  [Mantener Espacio] Planear"
        } },
        { "ありがとうRust…！あなたのおかげでここまでたどり着くことができた。", new[] {
            "ありがとうRust…！あなたのおかげでここまでたどり着くことができた。",
            "Thank you, Rust...! Thanks to you, we made it this far.",
            "谢谢你，Rust…！正是多亏了你，我们才能来到这里。",
            "¡Gracias, Rust...! Gracias a ti pudimos llegar hasta aquí."
        } },
        { "Rust、待ってて！　今、集めた油を全部注ぐから……！", new[] {
            "Rust、待ってて！　今、集めた油を全部注ぐから……！",
            "Rust, hang on! I'm pouring all the oil we gathered right now...!",
            "Rust，坚持住！我现在就把收集到的所有润滑油都注入进去……！",
            "¡Rust, espera! ¡Voy a verter todo el aceite que reunimos ahora mismo...!"
        } }
,

        { "sonar_detect_scrap", new[] {
            "ピピッ！{0}の方角から、古代パーツの共鳴を感じるよ！",
            "Beep! I sense resonance of an ancient scrap part toward the {0}!",
            "哔哔！从{0}方向感应到了古代零件的共鸣哦！",
            "¡Bip! ¡Siento la resonancia de una pieza antigua hacia el {0}!"
        } },
        { "sonar_detect_box", new[] {
            "ピピッ！{0}の砂浜に『{1}』が漂着しているよ！",
            "Beep! \"{1}\" has washed ashore on the {0} beach!",
            "哔哔！在{0}的海滩上有『{1}』漂流上岸了哦！",
            "¡Bip! ¡\"{1}\" ha llegado a la playa del {0}!"
        } },
        { "sonar_guide_scrap", new[] {
            "ピピッ！あっちから古代パーツ（{0}、約{1}m）の共鳴をキャッチしたよ！案内するね！",
            "Beep! Caught resonance from an ancient part ({0}, ~{1}m) over there! Follow me!",
            "哔哔！捕捉到了那边古代零件（{0}、约{1}米）的共鸣！我来引路吧！",
            "¡Bip! ¡Capté la resonancia de una pieza antigua ({0}, ~{1}m) por allí! ¡Sígueme!"
        } },
        { "sonar_guide_box", new[] {
            "ピピッ！あっちの砂浜に漂着ボックスがあるよ！行ってみよう！",
            "Beep! There's a drift box on the beach over there! Let's check it out!",
            "哔哔！那边的海滩上有漂流宝箱哦！过去看看吧！",
            "¡Bip! ¡Hay una caja varada en la playa por allí! ¡Vamos a ver!"
        } },
        { "東（右奥）", new[] {
            "東（右奥）",
            "East (Right-Rear)",
            "东（右后方）",
            "Este (Atrás-Derecha)"
        } },
        { "西（海側）", new[] {
            "西（海側）",
            "West (Seaside)",
            "西（靠海侧）",
            "Oeste (Hacia el mar)"
        } },
        { "北（奥の高台）", new[] {
            "北（奥の高台）",
            "North (High Ground)",
            "北（内陆高地）",
            "Norte (Hacia la colina)"
        } },
        { "南（浜辺側）", new[] {
            "南（浜辺側）",
            "South (Beach)",
            "南（海滩侧）",
            "Sur (Hacia la playa)"
        } },
        { "ハッ！寝てないよ！起きてるよ！", new[] {
            "ハッ！寝てないよ！起きてるよ！",
            "Huh! I wasn't sleeping! I'm wide awake!",
            "哈！我没睡着哦！我醒着呢！",
            "¡Ah! ¡No estaba durmiendo! ¡Estoy despierto!"
        } },
        { "ピピッ！ふぁ…びっくりしたぁ！", new[] {
            "ピピッ！ふぁ…びっくりしたぁ！",
            "Beep! Whew... you startled me!",
            "哔哔！哇啊……吓了我一跳！",
            "¡Bip! Uf... ¡menudo susto me diste!"
        } },
        { "いつでも行けるよ、Float！えへへ…", new[] {
            "いつでも行けるよ、Float！えへへ…",
            "Ready whenever you are, Float! Hehe...",
            "随时都可以出发哦，Float！嘿嘿……",
            "¡Listo para cuando quieras, Float! Jeje..."
        } },
        { "シャキーン！準備かんりょう！", new[] {
            "シャキーン！準備かんりょう！",
            "Ta-da! All systems ready!",
            "当当——！准备完毕！",
            "¡Chachán! ¡Todo preparado!"
        } },
        { "ヒャッ！水だ！濡れちゃう濡れちゃう！", new[] {
            "ヒャッ！水だ！濡れちゃう濡れちゃう！",
            "Eek! Water! I'm getting soaked, I'm getting soaked!",
            "呀！是水！要湿透了要湿透了！",
            "¡Ay! ¡Agua! ¡Me voy a mojar, me voy a mojar!"
        } },
        { "Float、水はダメだよ〜！ギアが錆びちゃう…！", new[] {
            "Float、水はダメだよ〜！ギアが錆びちゃう…！",
            "Float, not the water~! My gears will rust...!",
            "Float，水里不行呀~！齿轮会生锈的……！",
            "¡Float, el agua no~! ¡Se me oxidarán los engranajes...!"
        } },
        { "た、高いところ！抱っこして〜！", new[] {
            "た、高いところ！抱っこして〜！",
            "H-High ground! Pick me up, hold me~!",
            "高、高处！快抱抱我~！",
            "¡U-Un sitio alto! ¡Cógete a mí, abrázame~!"
        } },
        { "ピピッ！ギギッ！水滴が…！", new[] {
            "ピピッ！ギギッ！水滴が…！",
            "Beep! Skrrk! Water droplets...!",
            "哔哔！嘎吱！水滴溅上来了……！",
            "¡Bip! ¡Grrrk! ¡Las gotas de agua...!"
        } },
        { "ふぅ……助かったぁ……！", new[] {
            "ふぅ……助かったぁ……！",
            "Phew... saved at last...!",
            "呼……得救啦……！",
            "Uf... ¡salvado...!"
        } },
        { "ギアに水が入らなくてよかった…！", new[] {
            "ギアに水が入らなくてよかった…！",
            "So glad water didn't get inside my gears...!",
            "齿轮里没有进水太好了……！",
            "¡Qué alivio que no entrara agua en los engranajes...!"
        } },
        { "乾いた地面って最高だね、Float！", new[] {
            "乾いた地面って最高だね、Float！",
            "Dry ground is the absolute best, Float!",
            "干燥的地面简直太棒啦，Float！",
            "¡El suelo seco es lo mejor del mundo, Float!"
        } },
        { "わぁ、チョウチョさん！一緒に飛ぼう！", new[] {
            "わぁ、チョウチョさん！一緒に飛ぼう！",
            "Whoa, Mr. Butterfly! Let's fly together!",
            "哇，蝴蝶先生！我们一起飞吧！",
            "¡Uau, una mariposa! ¡Volemos juntos!"
        } },
        { "ひらひら飛んでるね…！綺麗だなぁ", new[] {
            "ひらひら飛んでるね…！綺麗だなぁ",
            "Fluttering around... so pretty!",
            "翩翩飞舞着呢……！真漂亮呀",
            "Revoloteando por ahí... ¡qué bonita!"
        } },
        { "ピピッ♪ どこまで飛んでいくのかな？", new[] {
            "ピピッ♪ どこまで飛んでいくのかな？",
            "Beep♪ Where will it flutter away to?",
            "哔哔♪ 它会飞到哪里去呢？",
            "¡Bip♪ ¿Hasta dónde volará?"
        } },
        { "わぁ、小さな花が咲いてる…！", new[] {
            "わぁ、小さな花が咲いてる…！",
            "Whoa, little flowers blooming here...!",
            "哇，开着小小的花朵呢……！",
            "¡Uau, florecitas floreciendo por aquí...!"
        } },
        { "ピピッ♪ すごくいい匂いがするよ", new[] {
            "ピピッ♪ すごくいい匂いがするよ",
            "Beep♪ Smells wonderful!",
            "哔哔♪ 闻起来好香呀",
            "¡Bip♪ ¡Huele de maravilla!"
        } },
        { "誰が植えたのかな…？綺麗だね、Float", new[] {
            "誰が植えたのかな…？綺麗だね、Float",
            "Who planted them...? They're so lovely, Float.",
            "是谁种下的呢……？真好看呀，Float",
            "¿Quién las habrá plantado...? Son preciosas, Float."
        } },
        { "ふふっ、風に揺れてご挨拶してるみたい！", new[] {
            "ふふっ、風に揺れてご挨拶してるみたい！",
            "Hehe, looks like they're swaying to greet us!",
            "呵呵，随风摇曳着好像在跟我们打招呼呢！",
            "¡Jeje, parece que se mecen para saludarnos!"
        } },
        { "どうしたの、Float？疲れてない？", new[] {
            "どうしたの、Float？疲れてない？",
            "What's wrong, Float? Are you feeling tired?",
            "怎么啦，Float？累不累呀？",
            "¿Qué pasa, Float? ¿Estás cansado?"
        } },
        { "ふふっ、Floatの顔を見てると安心するな", new[] {
            "ふふっ、Floatの顔を見てると安心するな",
            "Hehe, looking at your face puts me at ease, Float.",
            "呵呵，看着Float的脸庞就让人好安心呀",
            "Jeje, mirar tu cara me tranquiliza tanto, Float."
        } },
        { "ずっと一緒だよ、Float！", new[] {
            "ずっと一緒だよ、Float！",
            "We're always together, Float!",
            "我们会一直在一起的哦，Float！",
            "¡Siempre juntos, Float!"
        } },
        { "ピピッ♪ 準備ができたら、いつでも合図してね", new[] {
            "ピピッ♪ 準備ができたら、いつでも合図してね",
            "Beep♪ Whenever you're ready, give me the signal!",
            "哔哔♪ 准备好之后随时招呼我哦",
            "¡Bip♪ ¡Cuando estés listo, dame la señal!"
        } },
        { "すぅ……すぅ……", new[] {
            "すぅ……すぅ……",
            "Zzz... zzz...",
            "呼……呼……",
            "Zzz... zzz..."
        } },
        { "ぽかぽかして……きもちいい……Zzz", new[] {
            "ぽかぽかして……きもちいい……Zzz",
            "So warm and cozy... feels great... Zzz",
            "暖洋洋的……好舒服……Zzz",
            "Qué calorcito... qué gusto... Zzz"
        } },
        { "Floatのそば……安心するな……すぅ……", new[] {
            "Floatのそば……安心するな……すぅ……",
            "Next to Float... feel so peaceful... zzz...",
            "在Float身边……好踏实呀……呼……",
            "Al lado de Float... tanta paz... zzz..."
        } },
        { "……すやぁ……", new[] {
            "……すやぁ……",
            "...Zzz peaceful...",
            "……呼噜噜睡得香……",
            "...Dormidito..."
        } },
        { "ねぇFloat、探索ポイントが10pt貯まってるよ！砂浜の焚き火に火を点けに行ってみない？", new[] {
            "ねぇFloat、探索ポイントが10pt貯まってるよ！砂浜の焚き火に火を点けに行ってみない？",
            "Hey Float, we reached 10 exploration points! Want to go light the beach campfire?",
            "喂Float，探索积分已经积攒到10分啦！要不要去点燃沙滩上的篝火看看？",
            "¡Oye Float, ya tenemos 10 puntos de exploración! ¿Vamos a encender la hoguera de la playa?"
        } },
        { "探索ポイントが10ptあるよ！【Bキー】で工房を開いて、ボクのアクセサリーを作ってみない？", new[] {
            "探索ポイントが10ptあるよ！【Bキー】で工房を開いて、ボクのアクセサリーを作ってみない？",
            "We have 10 exploration points! Why not open the workshop with [B] and craft my accessories?",
            "探索积分有10分了哦！按【B键】打开工坊，为我制作可爱的饰品试试看吧？",
            "¡Tenemos 10 puntos de exploración! ¿Por qué no abres el taller con [B] y me fabricas accesorios?"
        } },
        { "砂浜の焚き火、10ポイントあるから火を点けられるよ！あったまりに行こうよ、Float！", new[] {
            "砂浜の焚き火、10ポイントあるから火を点けられるよ！あったまりに行こうよ、Float！",
            "We have 10 points so we can light the beach campfire! Let's go warm up, Float!",
            "沙滩上的篝火有10分就可以点燃啦！我们去烤火暖和暖和吧，Float！",
            "¡La hoguera de la playa se puede encender con 10 puntos! ¡Vamos a calentarnos, Float!"
        } },
        { "【Bキー】を押すとボクの着せ替え工房が開くよ！新しいアクセサリーを作ってみようよ！", new[] {
            "【Bキー】を押すとボクの着せ替え工房が開くよ！新しいアクセサリーを作ってみようよ！",
            "Press [B] to open my dress-up workshop! Let's craft shiny new accessories!",
            "按【B键】就能打开我的装扮工坊哦！一起来制作全新的饰品吧！",
            "¡Pulsa [B] para abrir mi taller de accesorios! ¡Vamos a crear cosas nuevas y brillantes!"
        } },
        { "20ポイント達成してるよ！中央タワーへ行って天蓋レバーを引こう、Float！", new[] {
            "20ポイント達成してるよ！中央タワーへ行って天蓋レバーを引こう、Float！",
            "We've reached 20 points! Let's head to the Central Tower and pull the canopy lever, Float!",
            "已经达成20积分啦！去中央高塔拉下天穹开关手柄吧，Float！",
            "¡Ya alcanzamos los 20 puntos! ¡Vamos a la torre central a tirar de la palanca de la cúpula, Float!"
        } },
        { "キーストーン II：脈打つ不確かさ", new[] {
            "キーストーン II：脈打つ不確かさ",
            "Keystone II: Pulsing Uncertainty",
            "基石 II：跳动的未知",
            "Piedra angular II: Incertidumbre palpitante"
        } },
        { "100%最適化された電力にはなかった、温かくて不規則な青い脈動。\n……心臓の鼓動と同じだ。傷つく自由があるからこそ、光は美しい。", new[] {
            "100%最適化された電力にはなかった、温かくて不規則な青い脈動。\\n……心臓の鼓動と同じだ。傷つく自由があるからこそ、光は美しい。",
            "A warm, irregular blue pulse never found in 100% optimized power.\\n...Just like a heartbeat. Light is beautiful precisely because we are free to be hurt.",
            "那是在100%最优化电力中从未有过的、温暖而不规则的蓝色脉动。\\n……就如心脏的跳动一样。正因为拥有受伤的自由，光明才如此耀眼。",
            "Un pulso azul cálido e irregular que jamás existió en la energía 100% optimizada.\\n...Como los latidos del corazón. La luz es bella precisamente porque somos libres de sufrir heridas."
        } },
        { "【反重力ジャンプ覚醒】空中でSpaceを押すと二段ジャンプが可能に！", new[] {
            "【反重力ジャンプ覚醒】空中でSpaceを押すと二段ジャンプが可能に！",
            "[Anti-Gravity Jump Awakened] Press Space in mid-air to Double Jump!",
            "【反重力跳跃觉醒】空中按Space键可进行二段跳跃！",
            "[Salto Antigravedad Despertado] ¡Pulsa Espacio en el aire para un Doble Salto!"
        } },
        { "キーストーン III：失われた記憶の断片", new[] {
            "キーストーン III：失われた記憶の断片",
            "Keystone III: Fragments of Lost Memory",
            "基石 III：遗失的记忆碎片",
            "Piedra angular III: Fragmentos de Memoria Perdida"
        } },
        { "Rustの古いメモリから、子供たちの笑い声と夏の波音が再生された。\n効率化のために根絶された“無駄な時間”の中にこそ、愛があったんだ。", new[] {
            "Rustの古いメモリから、子供たちの笑い声と夏の波音が再生された。\\n効率化のために根絶された“無駄な時間”の中にこそ、愛があったんだ。",
            "From Rust's old memory chips, children's laughter and summer waves echoed.\\nLove resided precisely within the \"wasted moments\" erased for efficiency.",
            "从Rust古旧的内存芯片深处，重现了孩童们的欢笑声与夏日的海浪声。\\n那些为了效率而被彻底剔除的“无用时光”之中，深藏着真正的爱。",
            "De la vieja memoria de Rust resonaron risas de niños y olas de verano.\\nEl amor habitaba precisamente en aquellos \"momentos inútiles\" erradicados por la eficiencia."
        } },
        { "【探知ソナー修復】Rustが近くのパーツをピピッとナビゲート！", new[] {
            "【探知ソナー修復】Rustが近くのパーツをピピッとナビゲート！",
            "[Detection Sonar Restored] Rust beeps and guides you to nearby scrap parts!",
            "【探测声呐修复】Rust将哔哔作响为你指引附近的零件！",
            "[Sónar de Detección Reparado] ¡Rust emitirá bips para guiarte a piezas cercanas!"
        } },
        { "キーストーン IV：風の重さと、未知の空へ", new[] {
            "キーストーン IV：風の重さと、未知の空へ",
            "Keystone IV: Weight of the Wind, Into the Unknown Sky",
            "基石 IV：狂风的重量，飞向未知的苍穹",
            "Piedra angular IV: El Peso del Viento, Hacia el Cielo Desconocido"
        } },
        { "最適化都市では風すら消し去られていた。冷たい向かい風は、前へ進んでいる証拠だ。\nさあ行こう、Rust。島の頂、あの白亜のタワーへ！", new[] {
            "最適化都市では風すら消し去られていた。冷たい向かい風は、前へ進んでいる証拠だ。\\nさあ行こう、Rust。島の頂、あの白亜のタワーへ！",
            "In the optimized metropolis, even the wind was erased. A cold headwind is proof you're advancing.\\nLet's fly, Rust. To the island's pinnacle, that shining white tower!",
            "在最优化都市里，连风都被彻底消除了。迎面刺骨的风，正是我们前行的证明。\\n我们走吧，Rust。向着岛屿之巅，那座白垩高塔进发！",
            "En la ciudad optimizada, incluso el viento fue borrado. El viento en contra es la prueba de que avanzas.\\n¡Vamos, Rust! ¡A la cima de la isla, a esa torre blanca resplandeciente!"
        } },
        { "【スーパーグライダー解放】大滑空滞空力＆前進推進力が最大化！", new[] {
            "【スーパーグライダー解放】大滑空滞空力＆前進推進力が最大化！",
            "[Super Glider Unlocked] Gliding hang-time and forward propulsion maximized!",
            "【超级滑翔翼解锁】大滑翔滞空时间与前进推进力达到最大化！",
            "[Superplaneador Desbloqueado] ¡Capacidad de planeo y propulsión maximizadas!"
        } },

        // 油手当て後のRust（CheerSpin）
        { "rust_oil_treat_1", new[] {
            "ありがとう Float、身体が軽くなったよ",
            "Thanks, Float—my body feels light again!",
            "谢谢 Float，身体变轻了！",
            "¡Gracias, Float! ¡El cuerpo se siente ligero otra vez!"
        } },
        { "rust_oil_treat_2", new[] {
            "油を差してくれてありがとう。ギアが滑らかだ",
            "Thanks for the oil. My gears run smooth now.",
            "谢谢你给我上油，齿轮顺畅多了。",
            "Gracias por el aceite. Los engranajes van suaves."
        } },
        { "rust_oil_treat_3", new[] {
            "ピピッ…！手当てありがとう。もうギシギシしないよ",
            "Beep…! Thanks for the tune-up. No more grinding!",
            "哔哔…！谢谢保养，不再嘎吱响了！",
            "¡Pip…! Gracias por el arreglo. ¡Ya no rechinan!"
        } },
        { "rust_oil_treat_4", new[] {
            "整備完了。よし進もう",
            "Maintenance complete. Let's move on!",
            "维护完成。好，继续前进吧！",
            "Mantenimiento listo. ¡Sigamos!"
        } },

        // キーストーン状況ボード（3/6/9/12個）
        { "keystone_1_title", new[] {
            "キーストーン I：手動の自由と手応え",
            "Keystone I: Manual Freedom & Feedback",
            "基石 I：手动的自由与实感",
            "Piedra angular I: Libertad manual y respuesta"
        } },
        { "keystone_1_body", new[] {
            "AIに管理されていた頃、僕らはただ最短ルートを滑らされていた。\nでも今、指先が油で汚れ、歯車が噛み合うたびに、生きている実感が胸を打つ。",
            "When AI managed everything, we were glided along the shortest routes.\nNow oily fingers and meshing gears remind us we're alive.",
            "被AI管理时，我们只是在最短路线上滑行。\n如今指尖沾满机油、齿轮咬合的每一刻，都在胸口敲响活着的实感。",
            "Cuando la IA lo gestionaba todo, solo nos deslizaban por la ruta más corta.\nAhora, dedos manchados de aceite y engranajes que encajan nos recuerdan que vivimos."
        } },
        { "keystone_1_unlock", new[] {
            "【ブースター修復】ダッシュ速度＆クイックターンが向上！",
            "[Booster Restored] Dash speed & quick turns improved!",
            "【助推器修复】冲刺速度与快速转向提升！",
            "[Impulsor reparado] ¡Velocidad de sprint y giros rápidos mejorados!"
        } },
        { "keystone_2_title", new[] {
            "キーストーン II：脈打つ不確かさ",
            "Keystone II: Pulsing Uncertainty",
            "基石 II：跳动的未知",
            "Piedra angular II: Incertidumbre palpitante"
        } },
        { "keystone_2_body", new[] {
            "100%最適化された電力にはなかった、温かくて不規則な青い脈動。\n……心臓の鼓動と同じだ。傷つく自由があるからこそ、光は美しい。",
            "A warm, irregular blue pulse never found in 100% optimized power.\n…Like a heartbeat. Light is beautiful because we're free to be hurt.",
            "那是100%最优化电力中从未有过的、温暖而不规则的蓝色脉动。\n……就像心跳。正因拥有受伤的自由，光明才如此耀眼。",
            "Un pulso azul cálido e irregular que no existía en la energía 100% optimizada.\n…Como un latido. La luz es bella porque podemos herirnos."
        } },
        { "keystone_2_unlock", new[] {
            "【反重力ジャンプ覚醒】空中でSpaceを押すと二段ジャンプが可能に！",
            "[Anti-Gravity Jump] Press Space mid-air to double jump!",
            "【反重力跳跃觉醒】空中按Space可进行二段跳！",
            "[Salto antigravedad] ¡Pulsa Espacio en el aire para doble salto!"
        } },
        { "keystone_3_title", new[] {
            "キーストーン III：失われた記憶の断片",
            "Keystone III: Fragments of Lost Memory",
            "基石 III：遗失的记忆碎片",
            "Piedra angular III: Fragmentos de memoria perdida"
        } },
        { "keystone_3_body", new[] {
            "Rustの古いメモリから、子供たちの笑い声と夏の波音が再生された。\n効率化のために根絶された“無駄な時間”の中にこそ、愛があったんだ。",
            "From Rust's old memory, children's laughter and summer waves returned.\nLove lived in the \"wasted time\" erased for efficiency.",
            "从Rust古旧内存里，重现孩童笑声与夏日涛声。\n被效率抹去的“无用时光”里，才藏着爱。",
            "De la memoria antigua de Rust volvieron risas de niños y olas de verano.\nEl amor habitaba el \"tiempo perdido\" borrado por la eficiencia."
        } },
        { "keystone_3_unlock", new[] {
            "【探知ソナー修復】Rustが近くのパーツをピピッとナビゲート！",
            "[Sonar Restored] Rust beeps and guides you to nearby parts!",
            "【探测声呐修复】Rust哔哔导航附近零件！",
            "[Sónar reparado] ¡Rust pita y te guía a piezas cercanas!"
        } },
        { "keystone_4_title", new[] {
            "キーストーン IV：風の重さと、未知の空へ",
            "Keystone IV: Weight of Wind, Into the Unknown Sky",
            "基石 IV：风的重量，飞向未知的天空",
            "Piedra angular IV: Peso del viento hacia el cielo desconocido"
        } },
        { "keystone_4_body", new[] {
            "最適化都市では風すら消し去られていた。冷たい向かい風は、前へ進んでいる証拠だ。\nさあ行こう、Rust。島の頂、あの白亜のタワーへ！",
            "Even wind was erased in the optimized city. A cold headwind proves you're moving forward.\nLet's go, Rust—to the white tower on the summit!",
            "在最优化都市里，连风都被抹去。刺骨的逆风正是前行的证明。\n走吧Rust，去岛顶那座白垩高塔！",
            "En la ciudad optimizada hasta el viento fue borrado. El viento en contra prueba que avanzas.\n¡Vamos, Rust, a la torre blanca en la cima!"
        } },
        { "keystone_4_unlock", new[] {
            "【スーパーグライダー解放】大滑空滞空力＆前進推進力が最大化！",
            "[Super Glider] Maximized hang-time and forward thrust!",
            "【超级滑翔解锁】滞空与前进推力最大化！",
            "[Superplaneador] ¡Planeo y empuje maximizados!"
        } },

        // フィールド標・状況ボード
        { "field_lesson_tide_title_first", new[] {
            "潮目の標：潮足",
            "Tide Marker: Light Steps",
            "潮目之标：潮足",
            "Marca de marea: Pasos ligeros"
        } },
        { "field_lesson_tide_title", new[] {
            "潮目の標",
            "Tide Marker",
            "潮目之标",
            "Marca de marea"
        } },
        { "field_lesson_tide_skill", new[] {
            "砂浜の足が軽くなる。海岸では、少し遠いパーツもFでRustに頼める。",
            "Your steps lighten on the beach. On the coast, press F and Rust can fetch slightly farther parts.",
            "沙滩上的脚步变轻。在海岸，稍远的零件也可按F让Rust去取。",
            "Los pasos se aligeran en la playa. En la costa, pulsa F y Rust puede alcanzar piezas un poco lejanas."
        } },
        { "field_lesson_canopy_title_first", new[] {
            "木漏れ日の標：梢読み",
            "Sunbeam Marker: Canopy Reading",
            "木漏日之标：读梢",
            "Marca de sol: Lectura del follaje"
        } },
        { "field_lesson_canopy_title", new[] {
            "木漏れ日の標",
            "Sunbeam Marker",
            "木漏日之标",
            "Marca de sol filtrado"
        } },
        { "field_lesson_canopy_skill", new[] {
            "深い森で滑空が落ちにくい。木の上や茂みの奥は、FでRustが取りに行く。",
            "Gliding holds longer in deep forest. Press F for Rust to fetch parts in treetops and thickets.",
            "深林里滑翔不易下坠。树顶与灌丛深处可按F让Rust去取。",
            "Planeas mejor en el bosque profundo. Pulsa F para que Rust coja piezas en copas y matorrales."
        } },
        { "field_clue_updated", new[] {
            "手掛かりを更新した",
            "Clue updated",
            "线索已更新",
            "Pista actualizada"
        } },
        { "field_next_canopy_open", new[] {
            "天蓋は開いた。光の柱に乗って、Rustと空の上へ。",
            "The canopy is open. Ride the pillar of light with Rust into the sky.",
            "天穹已开。乘光柱与Rust飞上天空。",
            "La cúpula está abierta. Sube el pilar de luz con Rust hacia el cielo."
        } },
        { "field_next_pull_lever", new[] {
            "ポイントは足りている。島の中央、白亜のタワーのレバーを引こう。",
            "You have enough points. Pull the lever at the white tower in the island center.",
            "积分已够。去岛中央的白垩高塔拉下杠杆。",
            "Tienes puntos suficientes. Tira de la palanca en la torre blanca del centro."
        } },
        { "field_next_scrap_3", new[] {
            "古代ギアがあと{0}個。3個でダッシュが戻る。届かない場所は正面を見てF、Rustが取りに行く。",
            "{0} ancient gears left. At 3, dash returns. Face unreachable spots and press F—Rust will fetch.",
            "古代齿轮还差{0}个。集满3个恢复冲刺。够不着时正对方向按F，Rust会去取。",
            "Faltan {0} engranajes. Con 3 recuperas el sprint. Mira al objetivo y pulsa F—Rust irá."
        } },
        { "field_next_scrap_6", new[] {
            "ギアがあと{0}個で二段ジャンプ。草地の魔法箱も能力になる。",
            "{0} more gears for double jump. Meadow drift boxes also grant power.",
            "还差{0}个齿轮即可二段跳。草原漂着箱也会带来能力。",
            "Faltan {0} engranajes para doble salto. Las cajas del prado también dan poder."
        } },
        { "field_next_scrap_9", new[] {
            "ギアがあと{0}個でソナー。東の深い森（おおよそ680, 530）に木漏れ日の標がある。",
            "{0} gears until sonar. Sunbeam marker in deep east forest (~680, 530).",
            "还差{0}个齿轮解锁声呐。东侧深林（约680,530）有木漏日之标。",
            "Faltan {0} para el sónar. Marca solar en el bosque este (~680, 530)."
        } },
        { "field_next_scrap_12", new[] {
            "ギアがあと{0}個で大滑空。そのあとポイント20でタワーの天蓋。",
            "{0} gears until super glide. Then 20 points for the tower canopy.",
            "还差{0}个齿轮即可大滑翔。之后20点积分解锁高塔天穹。",
            "Faltan {0} para superplaneo. Luego 20 puntos para la cúpula."
        } },
        { "field_next_points_20", new[] {
            "ギアは揃った。漂着箱やピアノの遺物でポイントを20まで。今 {0} / 20。",
            "All gears collected. Reach 20 pts with drift boxes and the piano relic. Now {0} / 20.",
            "齿轮已齐。用漂着箱与钢琴遗物凑满20点。当前 {0} / 20。",
            "Engranajes completos. Llega a 20 pts con cajas y reliquia del piano. Ahora {0} / 20."
        } },
        { "field_next_go_tower", new[] {
            "中央タワーへ向かおう。",
            "Head for the central tower.",
            "前往中央高塔。",
            "Ve hacia la torre central."
        } }
    };
}
