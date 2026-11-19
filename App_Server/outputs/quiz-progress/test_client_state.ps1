$ErrorActionPreference = 'Stop'
$task = $PSScriptRoot
$options = @('/nologo','/target:exe','/nostdlib+','/langversion:9')
$options += '/out:"' + (Join-Path $task 'ClientProgressTests.dll') + '"'
foreach ($ref in Get-ChildItem -LiteralPath 'C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/9.0.5/ref/net9.0' -Filter '*.dll') {
    $options += '/reference:"' + $ref.FullName + '"'
}
$json = 'C:/Users/hk010/Ant Teacher_V2/Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll'
$options += '/reference:"' + $json + '"'
$options += '"' + (Join-Path $task 'unity/FinanceQuizProgressState.cs') + '"'
$options += '"' + (Join-Path $task 'ClientProgressTests.cs') + '"'
$response = Join-Path $task 'client-state.rsp'
[IO.File]::WriteAllLines($response, $options, [Text.UTF8Encoding]::new($false))
& 'C:/Program Files/dotnet/dotnet.exe' 'C:/Program Files/dotnet/sdk/9.0.300/Roslyn/bincore/csc.dll' ('@' + $response)
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Copy-Item -LiteralPath $json -Destination (Join-Path $task 'Newtonsoft.Json.dll') -Force
$runtime = '{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.5"}}}'
[IO.File]::WriteAllText((Join-Path $task 'ClientProgressTests.runtimeconfig.json'),$runtime,[Text.UTF8Encoding]::new($false))
& 'C:/Program Files/dotnet/dotnet.exe' (Join-Path $task 'ClientProgressTests.dll')
exit $LASTEXITCODE
