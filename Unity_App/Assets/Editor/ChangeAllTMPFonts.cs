using UnityEngine;
using UnityEditor;
using TMPro;

public class ChangeAllTMPFonts : EditorWindow
{
    private TMP_FontAsset newFont;

    [MenuItem("Tools/Change All TMP Fonts")]
    public static void ShowWindow()
    {
        GetWindow<ChangeAllTMPFonts>("Change TMP Fonts");
    }

    private void OnGUI()
    {
        GUILayout.Label("현재 씬의 모든 TMP 폰트 변경", EditorStyles.boldLabel);

        newFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "New Font",
            newFont,
            typeof(TMP_FontAsset),
            false
        );

        if (GUILayout.Button("모든 TMP 폰트 변경"))
        {
            ChangeFonts();
        }
    }

    private void ChangeFonts()
    {
        if (newFont == null)
        {
            Debug.LogWarning("Font Asset을 선택하세요.");
            return;
        }

        TMP_Text[] texts = FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        Undo.RecordObjects(texts, "Change All TMP Fonts");

        int count = 0;

        foreach (TMP_Text text in texts)
        {
            text.font = newFont;
            EditorUtility.SetDirty(text);
            count++;
        }

        Debug.Log($"{count}개의 TMP Text 폰트를 변경했습니다.");
    }
}