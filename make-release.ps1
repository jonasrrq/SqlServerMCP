param(
    [string]$OutDir = ".\dist",
    [string]$SkillPath = ".\.agents\skills\SqlServerMCP"
)

if (-Not (Test-Path $SkillPath)) {
    Write-Error "Skill path not found: $SkillPath"
    exit 1
}

$skillFile = Join-Path $SkillPath 'SKILL.md'
$toolsFile = Join-Path $SkillPath 'tools.json'

if (-Not (Test-Path $skillFile)) {
    Write-Error "SKILL.md not found: $skillFile"
    exit 1
}
if (-Not (Test-Path $toolsFile)) {
    Write-Error "tools.json not found: $toolsFile"
    exit 1
}

$frontmatter = Get-Content -Path $skillFile -TotalCount 10
if ($frontmatter[0] -ne '---') {
    Write-Error "SKILL.md must start with YAML frontmatter (---)"
    exit 1
}
if (-Not ($frontmatter | Where-Object { $_ -match '^name:\s*\S+' })) {
    Write-Error "SKILL.md frontmatter is missing 'name'"
    exit 1
}
if (-Not ($frontmatter | Where-Object { $_ -match '^description:\s*\S+' })) {
    Write-Error "SKILL.md frontmatter is missing 'description'"
    exit 1
}
try {
    Get-Content -Raw $toolsFile | ConvertFrom-Json | Out-Null
} catch {
    Write-Error "tools.json is not valid JSON: $($_.Exception.Message)"
    exit 1
}

Write-Output "Validated skill: $SkillPath"

if (-Not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$dest = Join-Path $OutDir 'skills\SqlServerMCP'

Write-Output "Copying skill to $dest"
robocopy $SkillPath $dest /E | Out-Null

$zip = Join-Path $OutDir 'SqlServerMCP-skill.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Output "Compressing $dest -> $zip"
Compress-Archive -Path $dest -DestinationPath $zip -Force
Write-Output "Done: $zip"
