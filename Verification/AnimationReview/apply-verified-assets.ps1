$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath "$PSScriptRoot/../..").Path
$sandbox = (Resolve-Path -LiteralPath "$projectRoot/Verification/PresentationSandbox").Path
$proof = Get-Content -LiteralPath "$sandbox/Verification/startup-camera-results.txt"
if ($proof -match '^(FAIL|ERROR):' -or $proof[-1] -notmatch '^PASS: All startup') { throw 'Runtime validation did not pass.' }
$records = @(Get-Content -LiteralPath "$PSScriptRoot/original-animation-hashes.json" -Raw | ConvertFrom-Json)
Add-Type -AssemblyName System.IO.Compression
$archive = [IO.Compression.ZipFile]::OpenRead('C:/Users/gehbd/.codex/visualizations/2026/10/18/01a150bb-2ea6-71d3-8f98-7c6e06bdfbad/Paradox_Zero-animaciones-originales.zip')
$changes = @()
try {
    foreach ($record in $records) {
        $path = (Resolve-Path -LiteralPath $record.Path).Path
        if (-not $path.StartsWith($projectRoot + '\Assets\OneMoreTime\Generated_Hacha_Final 2\')) { throw "Outside active assets: $path" }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $record.Hash) { throw "Asset changed since snapshot: $path" }
        $relative = $path.Substring($projectRoot.Length + 1)
        $prepared = Join-Path $sandbox $relative
        $exists = Test-Path -LiteralPath $prepared
        if ($exists -and (Get-FileHash -LiteralPath $prepared -Algorithm SHA256).Hash -eq $record.Hash) { continue }
        if ($exists -and $path -notmatch '\.controller$|\.prefab$') { throw "Unexpected change to retained clip or meta: $path" }
        if (-not $exists -and $path -notmatch '\.anim(\.meta)?$') { throw "Unexpected deletion: $path" }
        $entry = $archive.GetEntry($relative.Replace('\','/'))
        if ($null -eq $entry) { throw "Missing backup: $relative" }
        $stream = $entry.Open()
        try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
        finally { $stream.Dispose() }
        if ($hash -ne $record.Hash) { throw "Backup hash mismatch: $relative" }
        $changes += [pscustomobject]@{ Path=$path; Prepared=$prepared; Delete=(-not $exists); Relative=$relative }
    }
} finally { $archive.Dispose() }
foreach ($change in $changes | Where-Object { -not $_.Delete }) {
    Copy-Item -LiteralPath $change.Prepared -Destination $change.Path -Force
}
foreach ($change in $changes | Where-Object Delete) {
    # Only exact .anim/.anim.meta files from the verified sandbox diff are removed.
    Remove-Item -LiteralPath $change.Path -Force
}
$changes | Select-Object Relative,Delete | ConvertTo-Json | Set-Content -LiteralPath "$PSScriptRoot/applied-assets.json"
Copy-Item -LiteralPath "$sandbox/Verification/startup-camera-results.txt" -Destination "$PSScriptRoot/validated-results.txt"
Copy-Item -LiteralPath "$sandbox/Verification/AnimationReview/rig-diagnostics.txt" -Destination "$PSScriptRoot/rig-diagnostics.txt"
foreach ($image in Get-ChildItem -LiteralPath "$sandbox/Verification/AnimationReview" -Filter '*.png') {
    Copy-Item -LiteralPath $image.FullName -Destination (Join-Path $PSScriptRoot $image.Name)
}
foreach ($image in Get-ChildItem -LiteralPath "$sandbox/Verification" -Filter 'startup-upgrades-*.png') {
    Copy-Item -LiteralPath $image.FullName -Destination (Join-Path $PSScriptRoot $image.Name)
}
"Applied $($changes.Count) verified asset changes; all retained animation clips preserved byte for byte."
