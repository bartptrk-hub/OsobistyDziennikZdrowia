$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskProject = Join-Path $taskRoot 'src/HistoriaMedyczna/HistoriaMedyczna.csproj'
$taskArtifacts = Join-Path $taskRoot '.artifacts/blazor-publish-check'
$taskProbe = Join-Path $taskArtifacts 'restore-probe'
$taskPublish = Join-Path $taskArtifacts 'published'
New-Item -ItemType Directory -Path $taskProbe -Force | Out-Null

# Reproduce the csproj-only stage used by cached Docker builds. The explicit
# RequiresAspNetWebAssets setting must still bring in the framework assets.
$taskProbeProject = Join-Path $taskProbe 'HistoriaMedyczna.csproj'
Copy-Item -LiteralPath $taskProject -Destination $taskProbeProject -Force
dotnet restore $taskProbeProject --nologo
if ($LASTEXITCODE -ne 0) { throw 'Restore probe failed.' }
$taskAssets = Get-Content -LiteralPath (Join-Path $taskProbe 'obj/project.assets.json') -Raw | ConvertFrom-Json
$taskAssetPackages = @($taskAssets.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'Microsoft.AspNetCore.App.Internal.Assets/*' })
if ($taskAssetPackages.Count -eq 0) { throw 'Blazor framework assets were omitted when restoring only the project file.' }
Write-Output 'PASS: restoring only the project still includes Blazor framework assets.'

dotnet publish $taskProject -c Release -o $taskPublish --artifacts-path (Join-Path $taskArtifacts 'build') /p:UseAppHost=false --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
$taskScript = Get-Item -LiteralPath (Join-Path $taskPublish 'wwwroot/_framework/blazor.web.js')
if ($taskScript.Length -eq 0) { throw 'Published bootstrap script is empty.' }
$taskManifest = Get-Content -LiteralPath (Join-Path $taskPublish 'HistoriaMedyczna.staticwebassets.endpoints.json') -Raw | ConvertFrom-Json
$taskRoutes = @($taskManifest.Endpoints | Where-Object { $_.Route -like '_framework/blazor.web*' })
if ($taskRoutes.Count -eq 0) { throw 'Published bootstrap script has no mapped routes.' }
if (Test-Path -LiteralPath (Join-Path $taskPublish 'appsettings.Local.json')) { throw 'Local configuration leaked into publish output.' }
Write-Output ('PASS: published Blazor bootstrap (' + $taskScript.Length + ' bytes), ' + $taskRoutes.Count + ' routes, no local settings.')
