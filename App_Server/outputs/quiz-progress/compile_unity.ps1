$ErrorActionPreference = 'Stop'
$task = $PSScriptRoot
$project = 'C:/Users/hk010/Ant Teacher_V2'
[xml]$csproj = Get-Content -LiteralPath (Join-Path $project 'Assembly-CSharp.csproj') -Raw -Encoding UTF8
$options = @('/nologo', '/target:library', '/nostdlib+', '/langversion:9', '/define:UNITY_ANDROID;UNITY_2022_2_OR_NEWER')
$options += '/out:"' + (Join-Path $task 'QuizProgress.CompileCheck.dll') + '"'
foreach ($hint in $csproj.SelectNodes("//*[local-name()='Reference']/*[local-name()='HintPath']")) {
    $path = $hint.InnerText
    if (![IO.Path]::IsPathRooted($path)) { $path = Join-Path $project $path }
    if (Test-Path -LiteralPath $path) { $options += '/reference:"' + $path + '"' }
}
foreach ($name in @('FinanceQuizProgressState.cs','FinanceQuizProgressService.cs','QuizLearningProgressUI.cs','FinanceQuizManager.cs')) {
    $options += '"' + (Join-Path $task ('unity/' + $name)) + '"'
}
foreach ($source in @('Assets/Scripts/Term/Quiz/FinanceQuizData.cs','Assets/Scripts/Term/Quiz/FinanceQuizButton.cs','Assets/Scripts/Term/JsonArrayHelper.cs','Assets/Scripts/Login/AuthManager.cs','Assets/Scripts/Login/ServerConfig.cs')) {
    $options += '"' + (Join-Path $project $source) + '"'
}
$response = Join-Path $task 'compile-unity.rsp'
[IO.File]::WriteAllLines($response, $options, [Text.UTF8Encoding]::new($false))
& 'C:/Program Files/dotnet/dotnet.exe' 'C:/Program Files/dotnet/sdk/9.0.300/Roslyn/bincore/csc.dll' ('@' + $response)
exit $LASTEXITCODE
