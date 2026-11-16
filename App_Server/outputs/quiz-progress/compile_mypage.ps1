$ErrorActionPreference = 'Stop'
$task = $PSScriptRoot
$project = 'C:/Users/hk010/Ant Teacher_V2'
[xml]$csproj = Get-Content -LiteralPath (Join-Path $project 'Assembly-CSharp.csproj') -Raw -Encoding UTF8
$options = @('/nologo', '/target:library', '/nostdlib+', '/langversion:9', '/nowarn:0649,1701')
$options += '/out:"' + (Join-Path $task 'MyPageProgress.CompileCheck.dll') + '"'
foreach ($hint in $csproj.SelectNodes("//*[local-name()='Reference']/*[local-name()='HintPath']")) {
    $path = $hint.InnerText
    if (![IO.Path]::IsPathRooted($path)) { $path = Join-Path $project $path }
    if (Test-Path -LiteralPath $path) { $options += '/reference:"' + $path + '"' }
}
$options += '"' + (Join-Path $task 'unity/MyPageController.cs') + '"'
foreach ($source in @('Assets/Scripts/MyPage/MyPageHoldingRowUI.cs','Assets/Scripts/Common/CandleModels.cs','Assets/Scripts/Term/Quiz/FinanceQuizProgressState.cs','Assets/Scripts/Term/Quiz/FinanceQuizProgressService.cs','Assets/Scripts/Login/AuthManager.cs','Assets/Scripts/Login/ServerConfig.cs')) {
    $options += '"' + (Join-Path $project $source) + '"'
}
$response = Join-Path $task 'compile-mypage.rsp'
[IO.File]::WriteAllLines($response, $options, [Text.UTF8Encoding]::new($false))
& 'C:/Program Files/dotnet/dotnet.exe' 'C:/Program Files/dotnet/sdk/9.0.300/Roslyn/bincore/csc.dll' ('@' + $response)
if ($LASTEXITCODE -eq 0) { Write-Output 'MyPage quiz progress: compilation passed.' }
exit $LASTEXITCODE
