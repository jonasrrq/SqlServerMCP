param(
    [string]$OutDir = ".\dist",
    [string]$SkillPath = ".\.agents\skills\mcp-sqlserver"
)

if (-Not (Test-Path $SkillPath)) {
    Write-Error "Skill path not found: $SkillPath"
    exit 1
}

if (-Not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$dest = Join-Path $OutDir 'skills\mcp-sqlserver'

Write-Output "Copying skill to $dest"
robocopy $SkillPath $dest /E | Out-Null

$zip = Join-Path $OutDir 'mcp-sqlserver-skill.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Output "Compressing $dest -> $zip"
Compress-Archive -Path $dest -DestinationPath $zip -Force
Write-Output "Done: $zip"
