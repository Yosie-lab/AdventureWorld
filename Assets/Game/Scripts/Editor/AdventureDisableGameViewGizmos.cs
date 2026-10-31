#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play中に Game ビューの Gizmos トグル／AnnotationWindow（Gizmosパネル）が
/// 勝手に開いて画面を覆うのを抑止する。
/// </summary>
[InitializeOnLoad]
static class AdventureDisableGameViewGizmos
{
    static double _nextForceAt;
    static bool _updateHooked;

    static AdventureDisableGameViewGizmos()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        // 既にPlay中でドメインリロードした場合にも効かせる
        if (EditorApplication.isPlaying)
            BeginPlayGuard();
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            BeginPlayGuard();
        else if (state == PlayModeStateChange.ExitingPlayMode)
            EndPlayGuard();
    }

    static void BeginPlayGuard()
    {
        EditorApplication.delayCall += ForceDisableOnce;
        if (!_updateHooked)
        {
            EditorApplication.update += OnEditorUpdate;
            _updateHooked = true;
        }
        _nextForceAt = 0;
    }

    static void EndPlayGuard()
    {
        if (_updateHooked)
        {
            EditorApplication.update -= OnEditorUpdate;
            _updateHooked = false;
        }
        CloseAnnotationWindows();
    }

    static void OnEditorUpdate()
    {
        if (!EditorApplication.isPlaying)
        {
            EndPlayGuard();
            return;
        }

        // 毎フレームは重いので 0.25秒間隔。パネルが開いたら即閉じる
        if (EditorApplication.timeSinceStartup < _nextForceAt && !HasOpenAnnotationWindow())
            return;
        _nextForceAt = EditorApplication.timeSinceStartup + 0.25;
        ForceDisableOnce();
    }

    static void ForceDisableOnce()
    {
        try
        {
            DisableGameViewGizmosToggle();
            CloseAnnotationWindows();
        }
        catch
        {
            // Editor内部API差異は無視
        }
    }

    static void DisableGameViewGizmosToggle()
    {
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameViewType == null) return;

        var windows = Resources.FindObjectsOfTypeAll(gameViewType);
        for (int i = 0; i < windows.Length; i++)
        {
            var window = windows[i] as EditorWindow;
            if (window == null) continue;

            // internal bool drawGizmos { set; }
            var drawProp = gameViewType.GetProperty("drawGizmos", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (drawProp != null && drawProp.CanWrite)
                drawProp.SetValue(window, false, null);

            // [SerializeField] bool m_Gizmos
            var so = new SerializedObject(window);
            var prop = so.FindProperty("m_Gizmos");
            if (prop != null && prop.propertyType == SerializedPropertyType.Boolean && prop.boolValue)
            {
                prop.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var field = gameViewType.GetField("m_Gizmos", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null && field.FieldType == typeof(bool))
                field.SetValue(window, false);

            // PlayModeView.showGizmos（基底）
            var showField = gameViewType.BaseType?.GetField("m_ShowGizmos", BindingFlags.Instance | BindingFlags.NonPublic)
                            ?? gameViewType.GetField("showGizmos", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            // showGizmos はプロパティの場合もある
            var showProp = gameViewType.GetProperty("showGizmos", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                           ?? gameViewType.BaseType?.GetProperty("showGizmos", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (showProp != null && showProp.CanWrite && showProp.PropertyType == typeof(bool))
                showProp.SetValue(window, false, null);

            window.Repaint();
        }
    }

    static bool HasOpenAnnotationWindow()
    {
        var annotationType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnnotationWindow");
        if (annotationType == null) return false;
        var windows = Resources.FindObjectsOfTypeAll(annotationType);
        for (int i = 0; i < windows.Length; i++)
        {
            var w = windows[i] as EditorWindow;
            if (w != null) return true;
        }
        return false;
    }

    static void CloseAnnotationWindows()
    {
        var annotationType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnnotationWindow");
        if (annotationType == null) return;

        var windows = Resources.FindObjectsOfTypeAll(annotationType);
        for (int i = 0; i < windows.Length; i++)
        {
            var w = windows[i] as EditorWindow;
            if (w == null) continue;
            try
            {
                w.Close();
            }
            catch
            {
                // ignore
            }
        }
    }
}
#endif
