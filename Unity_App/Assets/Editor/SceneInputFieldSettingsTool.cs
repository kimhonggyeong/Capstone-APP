using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 현재 열려 있는 씬의 모든 Input Field에 모바일용 공통 설정을 적용합니다.
/// </summary>
public static class SceneInputFieldSettingsTool
{
    private static readonly Color32 SelectionColor =
        new Color32(0, 0, 0, 50);

    [MenuItem("Tools/UI/현재 씬 Input Field 모바일 설정 적용")]
    private static void ApplySettingsToCurrentScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            EditorUtility.DisplayDialog(
                "Input Field 설정",
                "현재 열린 씬을 찾을 수 없습니다.",
                "확인"
            );
            return;
        }

        int tmpCount = ApplyTmpInputFields(activeScene);
        int legacyCount = ApplyLegacyInputFields(activeScene);

        if (tmpCount > 0 || legacyCount > 0)
            EditorSceneManager.MarkSceneDirty(activeScene);

        EditorUtility.DisplayDialog(
            "Input Field 설정 완료",
            $"씬: {activeScene.name}\n" +
            $"TMP Input Field: {tmpCount}개\n" +
            $"기본 UI Input Field: {legacyCount}개\n\n" +
            "적용값\n" +
            "- On Focus - Select All: False (TMP)\n" +
            "- Hide Mobile Input: True\n" +
            "- Selection Color: RGBA(0, 0, 0, 50)",
            "확인"
        );
    }

    private static int ApplyTmpInputFields(Scene activeScene)
    {
        TMP_InputField[] inputFields =
            Object.FindObjectsByType<TMP_InputField>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        int changedCount = 0;

        foreach (TMP_InputField inputField in inputFields)
        {
            if (inputField == null || inputField.gameObject.scene != activeScene)
                continue;

            Undo.RecordObject(inputField, "Apply TMP Input Field Mobile Settings");
            inputField.onFocusSelectAll = false;
            inputField.shouldHideMobileInput = true;
            inputField.selectionColor = SelectionColor;
            EditorUtility.SetDirty(inputField);
            changedCount++;
        }

        return changedCount;
    }

    private static int ApplyLegacyInputFields(Scene activeScene)
    {
        InputField[] inputFields =
            Object.FindObjectsByType<InputField>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        int changedCount = 0;

        foreach (InputField inputField in inputFields)
        {
            if (inputField == null || inputField.gameObject.scene != activeScene)
                continue;

            Undo.RecordObject(inputField, "Apply Input Field Mobile Settings");
            inputField.shouldHideMobileInput = true;
            inputField.selectionColor = SelectionColor;
            EditorUtility.SetDirty(inputField);
            changedCount++;
        }

        return changedCount;
    }
}
