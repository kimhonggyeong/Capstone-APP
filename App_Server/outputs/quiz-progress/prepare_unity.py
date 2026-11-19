from pathlib import Path
import hashlib
import json
import uuid

work = Path(__file__).resolve().parent
project = Path('C:/Users/hk010/Ant Teacher_V2')
target = project / 'Assets/Scripts/Term/Quiz'
original_path = target / 'FinanceQuizManager.cs'
original = original_path.read_bytes()
text = original.decode('utf-8-sig').replace('\r\n', '\n')

def replace_once(old, new):
    global text
    if text.count(old) != 1:
        raise RuntimeError(f'Expected a unique insertion point: {old[:90]!r}')
    text = text.replace(old, new, 1)

replace_once(
    '    private int bestStreak;\n',
    '    private int bestStreak;\n'
    '    private bool answerChecked;\n'
    '    private bool sessionCompletionRecorded;\n'
    '    private string sessionUsername;\n',
)
replace_once(
    '        allQuizzes.AddRange(loadedData);\n',
    '        allQuizzes.AddRange(loadedData);\n'
    '        FinanceQuizProgressService.Instance.ConfigureTotalQuizCount(allQuizzes.Count);\n',
)
replace_once(
    '    private void StartQuiz(string category, string type)\n    {\n',
    '    private void StartQuiz(string category, string type)\n    {\n'
    '        answerChecked = false;\n'
    '        sessionCompletionRecorded = false;\n'
    '        AuthManager auth = AuthManager.Instance;\n'
    '        sessionUsername = auth != null && auth.IsLoggedIn() ? auth.Username : "";\n'
    '        FinanceQuizProgressService.Instance.RefreshProgress();\n',
)
replace_once(
    '        FinanceQuizData quiz = currentQuizzes[currentIndex];\n\n        selectedIndex = -1;',
    '        FinanceQuizData quiz = currentQuizzes[currentIndex];\n\n'
    '        answerChecked = false;\n        selectedIndex = -1;',
)
replace_once(
    '    public void CheckAnswer()\n    {\n',
    '    public void CheckAnswer()\n    {\n'
    '        if (answerChecked || currentQuizzes == null || currentIndex < 0\n'
    '            || currentIndex >= currentQuizzes.Count) return;\n\n',
)
replace_once(
    '        bool isCorrect = selectedIndex == quiz.answerIndex;\n        UpdateQuizResultStats(isCorrect);',
    '        bool isCorrect = selectedIndex == quiz.answerIndex;\n'
    '        answerChecked = true;\n'
    '        UpdateQuizResultStats(isCorrect);\n'
    '        FinanceQuizProgressService.Instance.RecordAnswer(quiz.id, isCorrect, sessionUsername);',
)
replace_once(
    '    public void NextQuestion()\n    {\n        currentIndex++;',
    '    public void NextQuestion()\n    {\n'
    '        if (!answerChecked || currentQuizzes == null || currentIndex >= currentQuizzes.Count) return;\n'
    '        currentIndex++;',
)
replace_once(
    '        SetText(resultBestStreakText, bestStreak.ToString());\n',
    '        SetText(resultBestStreakText, bestStreak.ToString());\n\n'
    '        if (!sessionCompletionRecorded && totalCount > 0 && currentIndex >= totalCount)\n'
    '        {\n'
    '            sessionCompletionRecorded = true;\n'
    '            FinanceQuizProgressService.Instance.RecordSessionCompleted(sessionUsername);\n'
    '        }\n',
)
replace_once(
    '    public void ResetQuizState()\n    {\n',
    '    public void ResetQuizState()\n    {\n'
    '        answerChecked = false;\n'
    '        sessionCompletionRecorded = false;\n'
    '        sessionUsername = null;\n',
)

stage = work / 'unity'
(work / 'backup').mkdir(exist_ok=True)
(work / 'backup/FinanceQuizManager.cs').write_bytes(original)
(stage / 'FinanceQuizManager.cs').write_text(text, encoding='utf-8', newline='\r\n')
entries = []
for source in sorted(stage.iterdir()):
    if source.suffix not in ('.cs', '.md'):
        continue
    destination = target / source.name
    if source.name != 'FinanceQuizManager.cs' and destination.exists():
        raise RuntimeError(f'New script already exists: {destination}')
    entries.append({
        'source': str(source), 'destination': str(destination),
        'expected_sha256': hashlib.sha256(original).hexdigest() if destination == original_path else None,
    })
    if not destination.with_suffix(destination.suffix + '.meta').exists():
        meta = source.with_suffix(source.suffix + '.meta')
        if not meta.exists():
            importer = ('MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n'
                        '  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
                        '  userData: \n  assetBundleName: \n  assetBundleVariant: \n') if source.suffix == '.cs' else (
                        'TextScriptImporter:\n  externalObjects: {}\n  userData: \n'
                        '  assetBundleName: \n  assetBundleVariant: \n')
            meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n' + importer, encoding='utf-8')
        entries.append({'source': str(meta), 'destination': str(destination) + '.meta', 'expected_sha256': None})
(work / 'unity-install.json').write_text(json.dumps(entries, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'prepared_files':len(entries),'manager_original_sha256':hashlib.sha256(original).hexdigest()}))
