$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$destination=Join-Path $PSScriptRoot '../assets/icons'
$originals=Join-Path $PSScriptRoot '../assets-source/generated-icons'
New-Item -ItemType Directory -Force $destination,$originals | Out-Null
$mapping=Get-Content (Join-Path $PSScriptRoot 'generated-icons.json') -Raw | ConvertFrom-Json
foreach($entry in $mapping){
    Copy-Item -LiteralPath $entry.path -Destination (Join-Path $originals ($entry.id+'.png')) -Force
    $inputImage=[Drawing.Image]::FromFile($entry.path)
    $outputImage=New-Object Drawing.Bitmap(128,128,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics=[Drawing.Graphics]::FromImage($outputImage)
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.DrawImage($inputImage,0,0,128,128)
    $outputImage.Save((Join-Path $destination ($entry.id+'.png')),[Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose();$outputImage.Dispose();$inputImage.Dispose()
}
$sheet=New-Object Drawing.Bitmap(640,480)
$g=[Drawing.Graphics]::FromImage($sheet)
$g.Clear([Drawing.Color]::FromArgb(34,38,42))
$font=New-Object Drawing.Font('Arial',10)
for($i=0;$i -lt $mapping.Count;$i++){
    $entry=$mapping[$i];$x=($i%4)*160;$y=[Math]::Floor($i/4)*120
    $image=[Drawing.Image]::FromFile((Join-Path $destination ($entry.id+'.png')))
    $g.DrawImage($image,[int]($x+32),[int]$y,96,96)
    $g.DrawString($entry.id,$font,[Drawing.Brushes]::White,[single]($x+5),[single]($y+100))
    $image.Dispose()
}
$sheet.Save((Join-Path $PSScriptRoot '../assets/icon-preview.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$font.Dispose();$sheet.Dispose()
Write-Output 'Exported 13 transparent 128 x 128 icons.'
