# アイコンの SVG から、実行ファイルとトレイが使う画像を書き出す。
# 形と色の決まりは doc/DESIGN.md 第 10 節、手順は doc/DEVELOPMENT.md 第 2.3 節にある。
#
#   source.svg         -> icons/32x32.png, icons/128x128.png, icons/source-1024.png
#   source-16.svg      -> icons/icon.ico の 16px（20px 以上は source.svg から）
#   tray-on-light.svg  -> icons/tray/light-{16,24,32}.rgba
#   tray-on-dark.svg   -> icons/tray/dark-{16,24,32}.rgba
#
# トレイの画像を PNG ではなく生の RGBA にするのは、実行時に PNG の復号器を
# 抱えずに済ませるためである（doc/ARCHITECTURE.md 第 8.3 節）。
#
# 使い方: リポジトリの根で  powershell -File tools/export-icons.ps1

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$icons = Join-Path $root "src-tauri\icons"
$work = Join-Path ([IO.Path]::GetTempPath()) "shelfy-icons"
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory -Path $work | Out-Null

# SVG を指定の大きさの PNG に描く。描画は Tauri CLI（resvg）に任せる。
function Export-Png([string]$svg, [int[]]$sizes, [string]$name) {
    $out = Join-Path $work $name
    $list = $sizes -join ","
    cmd /c "npx tauri icon `"$(Join-Path $icons $svg)`" -o `"$out`" --png $list >nul 2>&1"
    if ($LASTEXITCODE -ne 0) { throw "tauri icon に失敗しました: $svg" }
    return $out
}

# PNG を読み、非乗算の RGBA のバイト列にする
function Read-Rgba([string]$png) {
    $bitmap = [System.Drawing.Bitmap]::FromFile($png)
    try {
        $rect = New-Object System.Drawing.Rectangle 0, 0, $bitmap.Width, $bitmap.Height
        $data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $bytes = New-Object byte[] ($bitmap.Width * $bitmap.Height * 4)
        # 1 行ずつ写す。Stride は幅 x 4 に等しい（32bpp のため）。
        [Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        $bitmap.UnlockBits($data)
    }
    finally {
        $bitmap.Dispose()
    }
    # BGRA を RGBA に並べ替える
    for ($i = 0; $i -lt $bytes.Length; $i += 4) {
        $b = $bytes[$i]; $bytes[$i] = $bytes[$i + 2]; $bytes[$i + 2] = $b
    }
    return , $bytes
}

function Write-Ico([object[]]$entries, [string]$path) {
    $stream = New-Object IO.MemoryStream
    $writer = New-Object IO.BinaryWriter $stream
    $writer.Write([int16]0); $writer.Write([int16]1); $writer.Write([int16]$entries.Count)
    $offset = 6 + 16 * $entries.Count
    foreach ($entry in $entries) {
        $dimension = if ($entry.Size -ge 256) { 0 } else { $entry.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([int16]1); $writer.Write([int16]32)
        $writer.Write([int]$entry.Bytes.Length); $writer.Write([int]$offset)
        $offset += $entry.Bytes.Length
    }
    foreach ($entry in $entries) { $writer.Write($entry.Bytes) }
    $writer.Flush()
    [IO.File]::WriteAllBytes($path, $stream.ToArray())
}

# ------------------------------------------------------------ アプリアイコン

$main = Export-Png "source.svg" @(20, 24, 32, 40, 48, 64, 128, 256, 1024) "main"
$small = Export-Png "source-16.svg" @(16) "small"

Copy-Item (Join-Path $main "32x32.png") (Join-Path $icons "32x32.png") -Force
Copy-Item (Join-Path $main "128x128.png") (Join-Path $icons "128x128.png") -Force
Copy-Item (Join-Path $main "1024x1024.png") (Join-Path $icons "source-1024.png") -Force

# 各サイズは PNG のまま格納する。BMP で入れると 64px 以下だけで数十 KB 増え、実行ファイルに載る。
# 先頭は 32px にする。Tauri はウィンドウとトレイの既定のアイコンに、先頭の 1 枚を使うためである。
$entries = @()
foreach ($size in 32, 16, 20, 24, 40, 48, 64, 256) {
    $png = if ($size -eq 16) { Join-Path $small "16x16.png" } else { Join-Path $main "${size}x${size}.png" }
    $entries += [pscustomobject]@{ Size = $size; Bytes = [IO.File]::ReadAllBytes($png) }
}
Write-Ico $entries (Join-Path $icons "icon.ico")

# ------------------------------------------------------------ トレイ

$tray = Join-Path $icons "tray"
New-Item -ItemType Directory -Path $tray -Force | Out-Null
foreach ($theme in "light", "dark") {
    $out = Export-Png "tray-on-$theme.svg" @(16, 24, 32) "tray-$theme"
    foreach ($size in 16, 24, 32) {
        $rgba = Read-Rgba (Join-Path $out "${size}x${size}.png")
        [IO.File]::WriteAllBytes((Join-Path $tray "$theme-$size.rgba"), $rgba)
    }
}

Remove-Item -Recurse -Force $work
Write-Output "書き出しました: $icons"
