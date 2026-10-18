$projectRoot = (Resolve-Path -LiteralPath "$PSScriptRoot/..").Path
$assetsRoot = Join-Path $projectRoot 'Assets'
$folders = @(Get-ChildItem -LiteralPath "$assetsRoot/OneMoreTime" -Directory | Where-Object { $_.Name -like 'Generated*' -and $_.Name -ne 'Generated_Hacha_Final 2' })
$folders += Get-Item -LiteralPath "$assetsRoot/_Recovery", "$assetsRoot/Scenes"
$guidMap = @{}
foreach ($meta in Get-ChildItem -LiteralPath $assetsRoot -Recurse -File -Filter '*.meta') {
    $match = [regex]::Match([IO.File]::ReadAllText($meta.FullName), '(?m)^guid: ([a-f0-9]{32})')
    if ($match.Success) { $guidMap[$match.Groups[1].Value] = $meta.FullName.Substring(0, $meta.FullName.Length - 5) }
}
$textExtensions = @('.unity', '.prefab', '.asset', '.mat', '.controller', '.anim', '.meta', '.json')
$sources = @(Get-ChildItem -LiteralPath $assetsRoot, (Join-Path $projectRoot 'ProjectSettings') -Recurse -File | Where-Object { $_.Extension -in $textExtensions })
$roots = @($sources | Where-Object {
    $path = $_.FullName
    -not ($folders | Where-Object { $path.StartsWith($_.FullName + '\') -or $path -eq ($_.FullName + '.meta') })
})
$reachable = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$queue = [Collections.Generic.Queue[string]]::new()
foreach ($source in $roots) { $queue.Enqueue($source.FullName) }
while ($queue.Count -gt 0) {
    $path = $queue.Dequeue()
    if (-not $reachable.Add($path) -or -not [IO.File]::Exists($path)) { continue }
    if ([IO.Path]::GetExtension($path) -notin $textExtensions) { continue }
    foreach ($reference in [regex]::Matches([IO.File]::ReadAllText($path), 'guid: ([a-f0-9]{32})')) {
        $guid = $reference.Groups[1].Value
        if ($guidMap.ContainsKey($guid)) {
            $queue.Enqueue($guidMap[$guid])
            if ([IO.File]::Exists($guidMap[$guid] + '.meta')) { $queue.Enqueue($guidMap[$guid] + '.meta') }
        }
    }
}
$report = foreach ($folder in $folders) {
    $files = @(Get-ChildItem -LiteralPath $folder.FullName -Recurse -File)
    $used = @($files | Where-Object { $reachable.Contains($_.FullName) })
    [pscustomobject]@{ Folder = $folder.FullName; Files = $files.Count; Bytes = ($files | Measure-Object -Property Length -Sum).Sum; RequiredFiles = $used.Count; RequiredPaths = @($used.FullName) }
}
$report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$PSScriptRoot/cleanup-audit.json"
$report | Select-Object Folder, Files, Bytes, RequiredFiles | Format-Table -AutoSize
