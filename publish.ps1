$architekturen = @("win-x86", "win-x64", "linux-arm", "linux-x64")

if (Test-Path "obj") {
    Remove-Item "obj" -Recurse -Force
}
if (Test-Path "bin") {
    Remove-Item "bin" -Recurse -Force
}

foreach ($architektur in $architekturen) {
    dotnet publish -r $architektur -c release /p:PublishSingleFile=true
}