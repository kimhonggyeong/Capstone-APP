from pathlib import Path
import hashlib
import json

work = Path(__file__).resolve().parent
target = Path('C:/Users/hk010/Ant Teacher_V2/Assets/Scripts/MyPage/MyPageController.cs')
original = target.read_bytes()
text = original.decode('utf-8-sig').replace('\r\n', '\n')

def replace_once(old, new):
    global text
    if text.count(old) != 1:
        raise RuntimeError(f'Insertion point changed: {old[:80]!r}')
    text = text.replace(old, new, 1)

replace_once(
    '    [Header("상태")]\n',
    '    [Header("퀴즈 학습 진행도")]\n'
    '    [SerializeField] private TMP_Text quizProgressText;\n'
    '    [SerializeField] private Slider quizProgressSlider;\n\n'
    '    [Header("상태")]\n',
)
replace_once(
    '    private readonly List<GameObject> spawnedRows = new List<GameObject>();\n',
    '    private readonly List<GameObject> spawnedRows = new List<GameObject>();\n'
    '    private FinanceQuizProgressService quizProgressService;\n'
    '    private float nextQuizProgressRefresh;\n',
)
replace_once(
    '    private void OnEnable()\n    {\n        ApplyUsername();\n',
    '    private void OnEnable()\n    {\n'
    '        ApplyUsername();\n'
    '        quizProgressService = FinanceQuizProgressService.Instance;\n'
    '        quizProgressService.Changed += ApplyQuizProgress;\n',
)
replace_once(
    '    private void OnDisable()\n    {\n',
    '    private void OnDisable()\n    {\n'
    '        if (quizProgressService != null)\n'
    '            quizProgressService.Changed -= ApplyQuizProgress;\n\n',
)
replace_once(
    '    public void RefreshAll()\n    {\n',
    '    public void RefreshAll()\n    {\n'
    '        RefreshQuizProgress();\n',
)
replace_once(
    '    private void ClearHoldingRows()\n',
    '    private void Update()\n'
    '    {\n'
    '        if (quizProgressService != null && Time.unscaledTime >= nextQuizProgressRefresh)\n'
    '            RefreshQuizProgress();\n'
    '    }\n\n'
    '    public void RefreshQuizProgress()\n'
    '    {\n'
    '        if (quizProgressService == null) return;\n'
    '        nextQuizProgressRefresh = Time.unscaledTime + 15f;\n'
    '        quizProgressService.RefreshProgress();\n'
    '        ApplyQuizProgress();\n'
    '    }\n\n'
    '    private void ApplyQuizProgress()\n'
    '    {\n'
    '        float progress = quizProgressService != null ? quizProgressService.ProgressPercent : 0f;\n'
    '        SetText(quizProgressText, progress.ToString("F0", CultureInfo.InvariantCulture) + "%");\n'
    '        if (quizProgressSlider != null)\n'
    '        {\n'
    '            quizProgressSlider.minValue = 0f;\n'
    '            quizProgressSlider.maxValue = 100f;\n'
    '            quizProgressSlider.wholeNumbers = false;\n'
    '            quizProgressSlider.interactable = false;\n'
    '            quizProgressSlider.SetValueWithoutNotify(progress);\n'
    '        }\n'
    '    }\n\n'
    '    private void ClearHoldingRows()\n',
)
stage = work / 'unity/MyPageController.cs'
stage.write_text(text, encoding='utf-8', newline='\r\n')
backup = work / 'backup/MyPageController.cs'
if backup.exists():
    raise RuntimeError('A previous MyPage backup already exists; review before preparing again.')
backup.write_bytes(original)
manifest = {
    'source': str(stage), 'destination': str(target),
    'expected_sha256': hashlib.sha256(original).hexdigest(),
}
(work / 'mypage-install.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'prepared':str(stage),'original_sha256':manifest['expected_sha256']},ensure_ascii=False))
