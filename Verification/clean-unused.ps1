$projectRoot = (Resolve-Path -LiteralPath "$PSScriptRoot/..").Path
$report = Get-Content -LiteralPath "$PSScriptRoot/cleanup-audit.json" -Raw | ConvertFrom-Json
$backupPath = 'C:\Users\gehbd\.codex\visualizations\2026\10\18\01a150bb-2ea6-71d3-8f98-7c6e06bdfbad\Paradox_Zero-respaldo-limpieza.zip'
if (Test-Path -LiteralPath $backupPath) { throw "Backup already exists: $backupPath" }
$backupSources = @()
foreach ($entry in $report) {
    if (Test-Path -LiteralPath $entry.Folder) {
        $resolved = (Resolve-Path -LiteralPath $entry.Folder).Path
        if (-not $resolved.StartsWith($projectRoot + '\Assets\')) { throw "Outside Assets: $resolved" }
        $backupSources += Get-ChildItem -LiteralPath $resolved -Recurse -File
        $backupSources += Get-Item -LiteralPath ($resolved + '.meta') -ErrorAction SilentlyContinue
    }
}
foreach ($name in @('Assembly-CSharp.csproj', 'Assembly-CSharp-Editor.csproj', 'My project_3.slnx', 'OMT_V5_test.slnx', 'LEEME_HACHA.md', 'LEEME_V5_JORMUNGANDR.md', 'LEEME_V6_AUDITORIA.md', 'LEEME_V7_MEJORAS.md', 'LEEME_V8_AUDITORIA.md')) {
    $backupSources += Get-Item -LiteralPath (Join-Path $projectRoot $name) -ErrorAction SilentlyContinue
}
foreach ($entry in Get-ChildItem -LiteralPath $PSScriptRoot) {
    if ($entry.Name -match '^(startup-|cleanup-|audit-cleanup|clean-unused)') { continue }
    if ($entry.PSIsContainer) { $backupSources += Get-ChildItem -LiteralPath $entry.FullName -Recurse -File }
    else { $backupSources += $entry }
}
Add-Type -AssemblyName System.IO.Compression
$archive = [IO.Compression.ZipFile]::Open($backupPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $backupSources) {
        $entryName = $file.FullName.Substring($projectRoot.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entryName) | Out-Null
    }
} finally { $archive.Dispose() }
$archive = [IO.Compression.ZipFile]::OpenRead($backupPath)
try {
    foreach ($file in $backupSources) {
        $entryName = $file.FullName.Substring($projectRoot.Length + 1).Replace('\', '/')
        $entry = $archive.GetEntry($entryName)
        if ($null -eq $entry -or $entry.Length -ne $file.Length) { throw "Backup incomplete: $entryName" }
        $stream = $entry.Open()
        try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
        finally { $stream.Dispose() }
        if ($hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw "Backup hash mismatch: $entryName" }
    }
} finally { $archive.Dispose() }
"Verified backup: $backupPath ($($backupSources.Count) files, SHA256 checked)" | Set-Content -LiteralPath "$PSScriptRoot/cleanup-backup.txt"
$retained = Join-Path $projectRoot 'Assets/OneMoreTime/Generated_Hacha_Final 2/LegacySpritesUnlit.mat'
$material = Join-Path $projectRoot 'Assets/OneMoreTime/Generated_Hacha_Final 1/SpritesUnlit.mat'
if (Test-Path -LiteralPath $material) {
    foreach ($source in @($material, ($material + '.meta'))) {
        $resolved = (Resolve-Path -LiteralPath $source).Path
        if (-not $resolved.StartsWith($projectRoot + '\')) { throw "Outside project: $resolved" }
    }
    if (Test-Path -LiteralPath $retained) { throw "Destination already exists: $retained" }
    Move-Item -LiteralPath $material -Destination $retained
    Move-Item -LiteralPath ($material + '.meta') -Destination ($retained + '.meta')
}
$deleted = @()
foreach ($entry in $report) {
    if (-not (Test-Path -LiteralPath $entry.Folder)) { continue }
    $resolved = (Resolve-Path -LiteralPath $entry.Folder).Path
    if (-not $resolved.StartsWith($projectRoot + '\Assets\')) { throw "Outside Assets: $resolved" }
    $remainingRequired = @($entry.RequiredPaths | Where-Object { $_ -and (Test-Path -LiteralPath $_) })
    if ($remainingRequired.Count -gt 0) { throw "Still required: $remainingRequired" }
    Remove-Item -LiteralPath $resolved -Recurse -Force
    if (Test-Path -LiteralPath ($resolved + '.meta')) { Remove-Item -LiteralPath ($resolved + '.meta') -Force }
    $deleted += $resolved
}
$extra = @('Assembly-CSharp.csproj', 'Assembly-CSharp-Editor.csproj', 'My project_3.slnx', 'OMT_V5_test.slnx', 'LEEME_HACHA.md', 'LEEME_V5_JORMUNGANDR.md', 'LEEME_V6_AUDITORIA.md', 'LEEME_V7_MEJORAS.md', 'LEEME_V8_AUDITORIA.md')
foreach ($name in $extra) {
    $path = Join-Path $projectRoot $name
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $resolved = (Resolve-Path -LiteralPath $path).Path
    if (-not $resolved.StartsWith($projectRoot + '\')) { throw "Outside project: $resolved" }
    Remove-Item -LiteralPath $resolved -Recurse -Force
    $deleted += $resolved
}
# Historical screenshots and traces are reproducible test outputs, not game assets.
foreach ($entry in Get-ChildItem -LiteralPath $PSScriptRoot) {
    if ($entry.Name -match '^(startup-|cleanup-|audit-cleanup|clean-unused)') { continue }
    $resolved = (Resolve-Path -LiteralPath $entry.FullName).Path
    if (-not $resolved.StartsWith($projectRoot + '\Verification\')) { throw "Outside Verification: $resolved" }
    Remove-Item -LiteralPath $resolved -Recurse -Force
    $deleted += $resolved
}
$deleted | Set-Content -LiteralPath "$PSScriptRoot/cleanup-removed.txt"
"Removed $($deleted.Count) obsolete files/folders; retained the referenced material and its GUID."
