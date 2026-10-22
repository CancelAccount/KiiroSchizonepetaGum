<#
.SYNOPSIS
    KiiroSchizonepetaGum 打包脚本：一次运行产出 Release 包（具备可编译工程时另产 Debug 包）。

.DESCRIPTION
    版本号唯一来源为 About/About.xml 的 <modVersion>，脚本负责：
      1. 校验工作区状态与 tag 是否已存在（仅告警，不阻塞）；
      2. 对 $BuildVersionDirs 中的版本目录分别以 Release / Debug 配置编译（输出目录由 csproj 隔离）；
      3. 按发布白名单组装包目录（复制阶段即排除 Source / AssembliesDebug，避免与 IDE 抢 obj 句柄）；
      4. Debug 包用 AssembliesDebug 的 dll 覆盖，并把包内 <modVersion> 改写为 "<版本>-debug"；
      5. 压缩为 dist/ 下的 zip，并校验关键文件齐全。

.PARAMETER CreateRelease
    打包完成后调用 gh CLI 创建 GitHub Release 并上传 zip（需已安装并登录 gh，且已先 git push）。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\Build-Release.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\Build-Release.ps1 -CreateRelease

.NOTES
    详见 Spec/02-发布流水线.md
#>
[CmdletBinding()]
param(
    [switch]$CreateRelease
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ==== 可配置项 ====
$PackageName    = 'KiiroSchizonepetaGum'                  # 发布包顶层文件夹名（zip 解压后的目录名）
$AssemblyName   = 'KiiroSchizonepetaGum'                  # 编译产物 dll 名（取自 csproj 项目名）
$IncludeDirs    = @('About', 'Sounds', 'Textures')        # 进入发布包的根级目录白名单
$IncludeFiles   = @('LoadFolders.xml')                    # 进入发布包的根级文件白名单
# 需要从源码编译的版本目录。1.6 工程引用已随游戏升级失效的 RimWorldWin64_Data\Managed，
# 故 1.6 沿用仓库内既有的 dll；1.7 工程引用游戏根目录程序集，可正常编译。
$BuildVersionDirs   = @('1.7')
# 进入发布包的版本目录（每个目录下需有 Assemblies\）
$PackageVersionDirs = @('1.6', '1.7')
# 版本目录内不进包的子目录（复制阶段排除）
$VersionExcludeDirs = @('Source', 'AssembliesDebug', 'obj', 'bin')
$DistDirName    = 'dist'                                  # 打包输出目录（已被 .gitignore 忽略）
$DebugModSuffix = 'debug'                                 # 调试包版本号与文件名后缀
# ==================

$RepoRoot  = Split-Path -Parent $PSScriptRoot
$AboutPath = Join-Path $RepoRoot 'About\About.xml'
$DistDir   = Join-Path $RepoRoot $DistDirName
$StageDir  = Join-Path $DistDir 'stage'

<#
.SYNOPSIS 输出阶段提示。
#>
function Write-Step {
    param([string]$Text)
    Write-Host "==> $Text" -ForegroundColor Cyan
}

<#
.SYNOPSIS 从 About.xml 读取 <modVersion>。
.PARAMETER Path About.xml 的绝对路径。
#>
function Get-ModVersion {
    param([string]$Path)

    if (-not (Test-Path $Path)) { throw "未找到 About.xml：$Path" }

    [xml]$xml = Get-Content -Path $Path -Raw
    $version = $xml.ModMetaData.modVersion
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "About.xml 中缺少 <modVersion>，请先补全后再打包"
    }
    return $version.Trim()
}

<#
.SYNOPSIS 以指定配置编译指定版本目录下的工程。
.PARAMETER Configuration 编译配置（Release / Debug）。
.PARAMETER VersionDir 版本目录名（如 1.7）。
#>
function Invoke-Build {
    param([string]$Configuration, [string]$VersionDir)

    # 工程布局两种都支持：Source\<名>.csproj（扁平）与 Source\<名>\<名>.csproj（多一层）
    $candidates = @(
        (Join-Path $RepoRoot "$VersionDir\Source\$AssemblyName.csproj"),
        (Join-Path $RepoRoot "$VersionDir\Source\$AssemblyName\$AssemblyName.csproj")
    )
    $csproj = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $csproj) { throw "未找到工程文件：$($candidates -join ' 或 ')" }

    Write-Step "编译 $Configuration 配置（$VersionDir）"
    & dotnet build $csproj -c $Configuration -v minimal
    if ($LASTEXITCODE -ne 0) { throw "$VersionDir $Configuration 编译失败（exit $LASTEXITCODE）" }
}

<#
.SYNOPSIS 将版本目录复制进包内，复制阶段排除开发目录（避免与 IDE/MSBuild 争抢 obj 句柄）。
.PARAMETER SourceDir 源版本目录。
.PARAMETER DestDir 包内目标版本目录。
#>
function Copy-VersionDir {
    param([string]$SourceDir, [string]$DestDir)

    Write-Step "复制版本目录：$(Split-Path -Leaf $SourceDir)"

    # robocopy 退出码 0~7 表示成功（1=有文件被复制，3=有文件被复制且有额外文件）
    & robocopy $SourceDir $DestDir /E /XD $VersionExcludeDirs /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy 复制失败：$SourceDir（exit $LASTEXITCODE）" }
    $global:LASTEXITCODE = 0
}

<#
.SYNOPSIS 校验包内关键文件齐全（缺 dll 玩家会加载失败，缺 PublishedFileId 会新建工坊条目）。
.PARAMETER ModRoot 包内顶层目录。
.PARAMETER VersionDirs 包内应存在的版本目录。
#>
function Assert-PackageComplete {
    param([string]$ModRoot, [string[]]$VersionDirs)

    $required = @(
        'LoadFolders.xml',
        'About\About.xml',
        'About\PublishedFileId.txt'
    )
    foreach ($rel in $required) {
        $path = Join-Path $ModRoot $rel
        if (-not (Test-Path $path)) { throw "发布包缺少必需文件：$rel（$ModRoot）" }
    }

    foreach ($ver in $VersionDirs) {
        $dll = Join-Path $ModRoot "$ver\Assemblies\$AssemblyName.dll"
        if (-not (Test-Path $dll)) { throw "发布包缺少 $ver 的程序集：$dll" }
    }
}

<#
.SYNOPSIS 按白名单组装单个发布包目录。
.PARAMETER ModRoot 目标包根目录（即包内顶层文件夹）。
.PARAMETER VersionDirs 需要放入发布包的版本目录列表。
.PARAMETER UseDebugDll 是否用 AssembliesDebug 的 dll 覆盖发行版 dll。
.PARAMETER Version 版本号（用于调试包改写）。
#>
function Copy-PackageLayout {
    param(
        [string]$ModRoot,
        [string[]]$VersionDirs,
        [bool]$UseDebugDll,
        [string]$Version
    )

    if (Test-Path $ModRoot) { Remove-Item $ModRoot -Recurse -Force }
    New-Item -ItemType Directory -Path $ModRoot -Force | Out-Null

    # 根级目录白名单
    foreach ($dir in $IncludeDirs) {
        $src = Join-Path $RepoRoot $dir
        if (-not (Test-Path $src)) { throw "发布包必需目录缺失：$dir" }
        Copy-Item -Path $src -Destination (Join-Path $ModRoot $dir) -Recurse -Force
    }

    # 根级文件白名单（LoadFolders.xml 必须留在包根，否则版本目录不会被加载）
    foreach ($file in $IncludeFiles) {
        $src = Join-Path $RepoRoot $file
        if (-not (Test-Path $src)) { throw "发布包必需文件缺失：$file" }
        Copy-Item -Path $src -Destination (Join-Path $ModRoot $file) -Force
    }

    # 版本目录（Defs / Languages / Assemblies / AssetBundles）
    foreach ($ver in $VersionDirs) {
        $src = Join-Path $RepoRoot $ver
        if (-not (Test-Path $src)) { throw "发布包必需版本目录缺失：$ver" }
        Copy-VersionDir -SourceDir $src -DestDir (Join-Path $ModRoot $ver)
    }

    # 调试包：用 AssembliesDebug 的 dll 覆盖，并改写包内版本号便于游戏内区分
    if ($UseDebugDll) {
        foreach ($ver in $VersionDirs) {
            $debugDll = Join-Path $RepoRoot "$ver\AssembliesDebug\$AssemblyName.dll"
            if (-not (Test-Path $debugDll)) { throw "未找到 $ver 的调试产物：$debugDll" }
            Copy-Item -Path $debugDll -Destination (Join-Path $ModRoot "$ver\Assemblies\$AssemblyName.dll") -Force
        }

        $targetAbout = Join-Path $ModRoot 'About\About.xml'
        $text = [System.IO.File]::ReadAllText($targetAbout)
        $text = [regex]::Replace($text, '<modVersion>.*?</modVersion>', "<modVersion>$Version-$DebugModSuffix</modVersion>")
        [System.IO.File]::WriteAllText($targetAbout, $text, (New-Object System.Text.UTF8Encoding $true))
    }
}

<#
.SYNOPSIS 将目录压缩为 zip（保留顶层文件夹，显式 UTF-8 条目名以兼容中文文件名）。
.PARAMETER SourceDir 待压缩目录（其父目录作为 zip 内路径基准）。
.PARAMETER DestZip 目标 zip 路径。
#>
function New-ZipFromDirectory {
    param([string]$SourceDir, [string]$DestZip)

    Add-Type -AssemblyName System.IO.Compression | Out-Null

    if (Test-Path $DestZip) { Remove-Item $DestZip -Force }
    New-Item -ItemType Directory -Path (Split-Path -Parent $DestZip) -Force | Out-Null

    $baseDir = Split-Path -Parent $SourceDir
    $fileStream = [System.IO.File]::Open($DestZip, [System.IO.FileMode]::Create)
    $archive = New-Object -TypeName System.IO.Compression.ZipArchive -ArgumentList $fileStream, ([System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -Path $SourceDir -Recurse -File) {
            $entryName = $file.FullName.Substring($baseDir.Length + 1).Replace('\', '/')
            $entry = $archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
            $entryStream = $entry.Open()
            try {
                $inStream = [System.IO.File]::OpenRead($file.FullName)
                try { $inStream.CopyTo($entryStream) } finally { $inStream.Dispose() }
            } finally {
                $entryStream.Dispose()
            }
        }
    } finally {
        $archive.Dispose()
    }
}

# ================== 主流程 ==================

$version = Get-ModVersion -Path $AboutPath
Write-Step "版本号：$version"

# 工作区未提交改动会导致包内容不对应任何 commit，仅告警
$dirty = & git -C $RepoRoot status --porcelain
if ($dirty) {
    Write-Warning "工作区存在未提交改动，包内容可能不对应任何 commit：`n$dirty"
}

# 同名 tag 已存在通常意味着忘记提升版本号
$existingTag = & git -C $RepoRoot tag -l "v$version"
if ($existingTag) {
    Write-Warning "tag v$version 已存在，请确认是否已提升 <modVersion>"
}

if ($BuildVersionDirs.Count -eq 0) {
    Write-Warning "未配置可编译版本目录（`$BuildVersionDirs 为空），直接使用仓库内既有 dll 打包，不产出 Debug 包"
} else {
    foreach ($ver in $BuildVersionDirs) {
        Invoke-Build 'Release' $ver
        Invoke-Build 'Debug' $ver
    }
}

if (Test-Path $StageDir) { Remove-Item $StageDir -Recurse -Force }
New-Item -ItemType Directory -Path $StageDir -Force | Out-Null

$releaseModRoot = Join-Path $StageDir "release\$PackageName"
Copy-PackageLayout -ModRoot $releaseModRoot `
    -VersionDirs $PackageVersionDirs `
    -UseDebugDll $false -Version $version
Assert-PackageComplete -ModRoot $releaseModRoot -VersionDirs $PackageVersionDirs

$releaseZip = Join-Path $DistDir "$PackageName-$version.zip"
New-ZipFromDirectory -SourceDir $releaseModRoot -DestZip $releaseZip
Write-Step "Release 包完成：$releaseZip"

# Debug 包只包含当前可编译的版本：不可编译的版本无法提供可信的调试 dll
if ($BuildVersionDirs.Count -gt 0) {
    $debugModRoot = Join-Path $StageDir "$DebugModSuffix\$PackageName"
    Copy-PackageLayout -ModRoot $debugModRoot `
        -VersionDirs $BuildVersionDirs `
        -UseDebugDll $true -Version $version
    Assert-PackageComplete -ModRoot $debugModRoot -VersionDirs $BuildVersionDirs

    $debugZip = Join-Path $DistDir "$PackageName-$version-$DebugModSuffix.zip"
    New-ZipFromDirectory -SourceDir $debugModRoot -DestZip $debugZip
    Write-Step "Debug 包完成：$debugZip"
}

Write-Step "打包完成"
Write-Host "  Release 包：$releaseZip"
Write-Host "  组装目录  ：$StageDir（创意工坊上传源为 stage\release\$PackageName）"

if ($CreateRelease) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Warning "未检测到 gh CLI，跳过自动发布。请手动在 GitHub 创建 Release 并上传上述 zip。"
    } else {
        $assets = @($releaseZip)
        if ($BuildVersionDirs.Count -gt 0) {
            $assets += (Join-Path $DistDir "$PackageName-$version-$DebugModSuffix.zip")
        }
        Write-Step "创建 GitHub Release v$version"
        & gh release create "v$version" @assets --title "v$version" --generate-notes
        if ($LASTEXITCODE -ne 0) { throw "gh release create 失败（exit $LASTEXITCODE）" }
    }
}
