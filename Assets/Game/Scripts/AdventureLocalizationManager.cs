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
                OnLanguageChanged?.Invoke();
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

        // --- ディスプレイ・画質設定 ---
        { "opt_display_mode", new[] { "画面表示モード", "Display Mode", "显示模式", "Modo de Pantalla" } },
        { "opt_fullscreen", new[] { "フルスクリーン", "Fullscreen", "全屏显示", "Pantalla Completa" } },
        { "opt_windowed", new[] { "ウィンドウ", "Windowed", "窗口模式", "Modo Ventana" } },
        { "opt_quality", new[] { "グラフィック描画品質", "Graphics Quality", "画面渲染品质", "Calidad Gráfica" } },
        { "opt_quality_fast", new[] { "軽量 (Fast)", "Fast", "流畅 (Fast)", "Rápido" } },
        { "opt_quality_high", new[] { "高画質 (High)", "High", "高画质 (High)", "Alto" } },
        { "opt_quality_ultra", new[] { "最高画質 (Ultra)", "Ultra", "极致 (Ultra)", "Ultra" } },
        { "opt_vsync", new[] { "垂直同期 (V-Sync)", "V-Sync", "垂直同步 (V-Sync)", "Sincronización Vertical (V-Sync)" } },
        { "opt_resolution", new[] { "解像度プリセット", "Resolution", "分辨率预设", "Resolución" } },

        // --- HUD・コンパス ---
        { "hud_parts_goal", new[] { "✦ 目標: パーツ回収 ({0}/{1}) [計 {2}pt]", "✦ Goal: Collect Scrap Parts ({0}/{1}) [{2} pts]", "✦ 目标: 收集零件 ({0}/{1}) [总计 {2}分]", "✦ Objetivo: Recuperar piezas ({0}/{1}) [{2} pts]" } },
        { "hud_nearest_part", new[] { "最寄りパーツ: {0} 約{1}m", "Nearest Scrap: {0} ~{1}m", "最近的零件: {0} 约{1}米", "Pieza más cercana: {0} ~{1}m" } },
        { "hud_oil", new[] { "潤滑油: {0}", "Lubricant: {0}", "润滑油: {0}", "Lubricante: {0}" } },
        { "hud_interact_pet", new[] { "[E / クリック] 撫でる / 注油", "[E / Click] Pet / Oil", "[E / 点击] 抚摸 / 注油", "[E / Clic] Acariciar / Lubricar" } },

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
        } }
    };
}
