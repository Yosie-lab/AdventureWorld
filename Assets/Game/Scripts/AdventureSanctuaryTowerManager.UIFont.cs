using UnityEngine;
using UnityEngine.UI;

/// <summary>タワー系 uGUI の OS フォント解決（各 partial から共有）</summary>
public partial class AdventureSanctuaryTowerManager
{
    static Font _cachedUiFont;

    static Font ResolveUiFont()
    {
        // 壊れた OS フォント（Hiragino Sans）を掴んだまま残さない
        if (_cachedUiFont != null)
        {
            try
            {
                CharacterInfo info;
                _cachedUiFont.RequestCharactersInTexture("あ", 28, FontStyle.Normal);
                if (!_cachedUiFont.GetCharacterInfo('あ', out info, 28, FontStyle.Normal))
                    _cachedUiFont = null;
            }
            catch { _cachedUiFont = null; }
        }
        if (_cachedUiFont != null) return _cachedUiFont;
        _cachedUiFont = CreateJapaneseFont(28);
        return _cachedUiFont;
    }

    /// <summary>
    /// EditorのGUI.skin.fontは使わない（Play中に「Gizmos」等のEditor文字が化けるため）。
    /// Unity 6 では "Hiragino Sans" が face 読み込みに失敗するため除外する。
    /// </summary>
    static Font CreateJapaneseFont(int size)
    {
        string[] candidates =
        {
            "PingFang SC",
            "Hiragino Sans GB",
            "Heiti SC",
            "HiraginoSans-W3",
            "HiraginoSans-W6",
            "Hiragino Kaku Gothic ProN",
            "Hiragino Kaku Gothic ProN W3",
            "YuGothic",
            "Yu Gothic",
            "YuGothic-Medium",
            "Apple SD Gothic Neo",
            "Microsoft YaHei",
            "SimHei",
            "Arial Unicode MS",
            "Helvetica Neue",
            "Arial"
        };

        try
        {
            Font multiFont = Font.CreateDynamicFontFromOSFont(candidates, size);
            if (multiFont != null) return multiFont;
        }
        catch { }

        for (int i = 0; i < candidates.Length; i++)
        {
            try
            {
                Font font = Font.CreateDynamicFontFromOSFont(candidates[i], size);
                if (font == null) continue;
                font.RequestCharactersInTexture("あA你", size, FontStyle.Normal);
                CharacterInfo info;
                if (font.GetCharacterInfo('あ', out info, size, FontStyle.Normal) ||
                    font.GetCharacterInfo('A', out info, size, FontStyle.Normal))
                    return font;
            }
            catch { }
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
               ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    static void PrepareFontForText(Font font, string text, int fontSize, FontStyle style = FontStyle.Normal)
    {
        if (font == null || string.IsNullOrEmpty(text)) return;
        font.RequestCharactersInTexture(text, fontSize, style);
        font.RequestCharactersInTexture(text, fontSize, FontStyle.Normal);
        font.RequestCharactersInTexture(text, fontSize, FontStyle.Bold);
    }
}
