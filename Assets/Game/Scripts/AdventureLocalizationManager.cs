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

    public static void NextLanguage()
    {
        int next = ((int)CurrentLanguage + 1) % 4;
        CurrentLanguage = (GameLanguage)next;
    }

    public static void PrevLanguage()
    {
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
        } }
    };
}
