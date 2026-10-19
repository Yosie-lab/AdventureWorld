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

    static float _lastSwitchTime = -1f;

    public static void NextLanguage()
    {
        if (Time.unscaledTime - _lastSwitchTime < 0.15f) return;
        _lastSwitchTime = Time.unscaledTime;
        int next = ((int)CurrentLanguage + 1) % 4;
        CurrentLanguage = (GameLanguage)next;
    }

    public static void PrevLanguage()
    {
        if (Time.unscaledTime - _lastSwitchTime < 0.15f) return;
        _lastSwitchTime = Time.unscaledTime;
        int prev = ((int)CurrentLanguage + 3) % 4;
        CurrentLanguage = (GameLanguage)prev;
    }

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
    public static string GetFormat(string key, params object[] args)
    {
        string pattern = Get(key);
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
        { "prompt_workbench", new[] { "<b>[E / クリック] Rustの着せ替え工房</b>\n<size=16><color=#DDEEFF>地上で [B] キーでも開ける</color></size>", "<b>[E / Click] Rust's Customization Workshop</b>\n<size=16><color=#DDEEFF>Press [B] on ground to open anytime</color></size>", "<b>[E / 点击] Rust的外观工坊</b>\n<size=16><color=#DDEEFF>在地面按 [B] 键也可随时开启</color></size>", "<b>[E / Clic] Taller de Rust</b>\n<size=16><color=#DDEEFF>Pulsa [B] en el suelo para abrir</color></size>" } },
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
            "<b>[Q] / [中クリック]</b>  <b>相棒Floatに道案内を頼む（最寄り目標へ先行フライト）</b>\n" +
            "<b>[F]</b>  遠隔パーツ回収 / 視線偵察指示\n" +
            "<b>[B]</b>  Rustの着せ替え工房を開く\n" +
            "<b>[Tab]</b>  クエスト目標 ⇄ ミニマルHUD切替\n" +
            "<b>[F5] / [Cmd+S]</b>  クイックセーブ\n" +
            "<b>[ESC] / [Pad B]</b>  ポーズ ＆ 設定メニュー開閉\n\n" +
            "✦ コンパスHUDと相棒Floatが最寄りの風脈・パーツ・タワーを常時案内します。",

            "<b>[ Interactions & System ]</b>\n\n" +
            "<b>[E] / [Left Click]</b>  Inspect / Lubricate / Pet\n" +
            "<b>[Q] / [Middle Click]</b>  <b>Ask Float to Guide (Scout to nearest goal)</b>\n" +
            "<b>[F]</b>  Remote Scrap Retrieve / Scout Command\n" +
            "<b>[B]</b>  Open Rust Customization Workshop\n" +
            "<b>[Tab]</b>  Toggle Quest Objectives / Minimal HUD\n" +
            "<b>[F5] / [Cmd+S]</b>  Quick Save\n" +
            "<b>[ESC] / [Pad B]</b>  Pause & Settings Menu\n\n" +
            "✦ Compass HUD & Float always guide you to nearby currents and scrap.",

            "<b>[ 互动与系统功能 ]</b>\n\n" +
            "<b>[E] / [左键点击]</b>  调查 / 注油 / 抚摸\n" +
            "<b>[Q] / [中键点击]</b>  <b>请求Float引路 (飞向最近目标)</b>\n" +
            "<b>[F]</b>  远程回收零件 / 视线侦察指令\n" +
            "<b>[B]</b>  打开Rust外观工坊\n" +
            "<b>[Tab]</b>  切换任务目标 / 极简HUD\n" +
            "<b>[F5] / [Cmd+S]</b>  快速存档\n" +
            "<b>[ESC] / [手柄 B]</b>  暂停与设置菜单\n\n" +
            "✦ 罗盘HUD与Float将时刻为您指引风脉、零件与圣域之塔。",

            "<b>[ Interacción y Sistema ]</b>\n\n" +
            "<b>[E] / [Clic Izquierdo]</b>  Examinar / Lubricar / Acariciar\n" +
            "<b>[Q] / [Clic Central]</b>  <b>Pedir guía a Float (Vuela al objetivo cercano)</b>\n" +
            "<b>[F]</b>  Recuperar pieza a distancia / Orden de exploración\n" +
            "<b>[B]</b>  Abrir taller de personalización de Rust\n" +
            "<b>[Tab]</b>  Alternar HUD de misiones / Modo mínimo\n" +
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
            "✦ 限界高度：凍結危機 ✦",
            "✦ Altitude Limit: Freezing Crisis ✦",
            "✦ 极限高度：冻结危机 ✦",
            "✦ Límite de Altitud: Crisis de Congelación ✦"
        } },
        { "climax_oil_prompt", new[] {
            "極寒の気流でRustのギアが凍りつく……！\n集めた常備油を心臓部へ注ぎ込め！\n【 E / Space / クリック長押し 】",
            "Rust's gears are freezing in the icy gale...!\nPour your collected lubricant into the core!\n[ Hold E / Space / Click ]",
            "极寒的气流让Rust的齿轮快要冻结了……！\n把收集到的润滑油注入核心！\n【长按 E / Space / 鼠标点击】",
            "¡Los engranajes de Rust se congelan en el vendaval gélido...!\n¡Vierte el lubricante recolectado en su núcleo!\n[ Mantén presionado E / Espacio / Clic ]"
        } },
        { "climax_oil_hold", new[] {
            "【長押しで注油】Rustを温める",
            "[Hold to Lubricate] Warm Rust Up",
            "【长按注入润滑油】为Rust加温",
            "[Mantén para Lubricar] Calentar a Rust"
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
        } }
    };
}
