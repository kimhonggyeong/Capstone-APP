using System.Collections;
using UnityEngine;

public class Reaction_PanelManager : MonoBehaviour
{
    public GameObject simulatorRoot;
    public ReactionPanelSlider inputPanelSlider;
    public ReactionPanelSlider resultPanelSlider;
    public GameObject loadingPanel;
    public float rootDisableDelay = .6f;
    Coroutine closeRoutine;
    bool analysisInProgress;

    void Awake()
    {
        if (inputPanelSlider != null) inputPanelSlider.SetCloseRootAction(DisableRootFromSlider);
        if (resultPanelSlider != null) resultPanelSlider.SetCloseRootAction(DisableRootFromSlider);
    }
    void Start() { HideEverythingImmediate(); }
    public void OpenSimulator()
    {
        StopClose();
        if (simulatorRoot != null) simulatorRoot.SetActive(true);
        if (resultPanelSlider != null) resultPanelSlider.HideForSwitchImmediate();
        if (inputPanelSlider != null) inputPanelSlider.OpenPanel();
        if (analysisInProgress) ShowLoadingOnly();
    }
    public void CloseSimulator()
    {
        if (inputPanelSlider != null && inputPanelSlider.gameObject.activeInHierarchy) inputPanelSlider.ClosePanel();
        if (resultPanelSlider != null && resultPanelSlider.gameObject.activeInHierarchy) resultPanelSlider.ClosePanel();
        StopClose(); closeRoutine = StartCoroutine(DisableRootLater());
    }
    public void ShowInput()
    {
        StopClose(); if (simulatorRoot != null) simulatorRoot.SetActive(true);
        if (resultPanelSlider != null) resultPanelSlider.CloseForSwitch();
        if (inputPanelSlider != null) inputPanelSlider.OpenPanel();
        if (analysisInProgress) ShowLoadingOnly();
    }
    public void ShowResult()
    {
        StopClose(); if (simulatorRoot != null) simulatorRoot.SetActive(true);
        if (inputPanelSlider != null) inputPanelSlider.CloseForSwitch();
        if (resultPanelSlider != null) resultPanelSlider.OpenPanel();
    }
    public void ShowLoadingOnly() { if (loadingPanel != null) loadingPanel.SetActive(true); }
    public void HideLoading() { if (loadingPanel != null) loadingPanel.SetActive(analysisInProgress); }
    public void SetAnalysisLoading(bool value)
    {
        analysisInProgress = value;
        if (value) ShowLoadingOnly();
        else HideLoading();
    }
    // The result screen does not return directly to the input screen.
    // Closing it ends the simulator; the next OpenSimulator starts from InputPanel.
    public void OnClickBackToInput() { CloseSimulator(); }
    public void ConfirmResult() { CloseSimulator(); }
    IEnumerator DisableRootLater() { yield return new WaitForSecondsRealtime(rootDisableDelay); HideEverythingImmediate(); }
    void HideEverythingImmediate()
    {
        if (inputPanelSlider != null) inputPanelSlider.HideForSwitchImmediate();
        if (resultPanelSlider != null) resultPanelSlider.HideForSwitchImmediate();
        HideLoading();
        SetPanelRectActive(inputPanelSlider, false);
        SetPanelRectActive(resultPanelSlider, false);
        if (simulatorRoot != null) simulatorRoot.SetActive(false);
    }
    void DisableRootFromSlider()
    {
        HideLoading();
        SetPanelRectActive(inputPanelSlider, false);
        SetPanelRectActive(resultPanelSlider, false);
        if (simulatorRoot != null) simulatorRoot.SetActive(false);
    }
    void SetPanelRectActive(ReactionPanelSlider slider, bool value)
    {
        if (slider != null && slider.panelRect != null)
            slider.panelRect.gameObject.SetActive(value);
    }
    void StopClose() { if (closeRoutine != null) StopCoroutine(closeRoutine); closeRoutine = null; }
    // Legacy compatibility
    public void ShowList() { ShowInput(); }
    public IEnumerator ShowListAfterLoading() { yield return null; ShowInput(); }
    public IEnumerator ShowInputAfterLoading() { yield return null; ShowInput(); }
    public IEnumerator ShowResultAfterLoading() { yield return null; ShowResult(); }
    public void OnClickBackToList() { CloseSimulator(); }
}
