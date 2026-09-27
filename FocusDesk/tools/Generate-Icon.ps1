# Draw the code-native FocusDesk mark into multiple PNG frames in one Windows ICO.
Add-Type -AssemblyName System.Drawing
$assetDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/FocusDesk.App/Assets'))
$frames = @()
foreach ($size in @(16,20,24,32,40,48,64,128,256)) {
    $bitmap = [Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([Drawing.Color]::Transparent)
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $inset = [single]($size * 0.025)
    $width = [single]($size - 2*$inset)
    $diameter = [single]($size * 0.48)
    $path.AddArc($inset,$inset,$diameter,$diameter,180,90)
    $path.AddArc(($inset+$width-$diameter),$inset,$diameter,$diameter,270,90)
    $path.AddArc(($inset+$width-$diameter),($inset+$width-$diameter),$diameter,$diameter,0,90)
    $path.AddArc($inset,($inset+$width-$diameter),$diameter,$diameter,90,90)
    $path.CloseFigure()
    $brush = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#7160D5'))
    $g.FillPath($brush,$path)
    $font = [Drawing.Font]::new('Segoe UI',[single]($size*0.82),[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
    $format = [Drawing.StringFormat]::new()
    $format.Alignment = [Drawing.StringAlignment]::Center
    $format.LineAlignment = [Drawing.StringAlignment]::Center
    $rect = [Drawing.RectangleF]::new([single](-$size*0.018),[single](-$size*0.028),[single]$size,[single]$size)
    $g.DrawString('f',$font,[Drawing.Brushes]::White,$rect,$format)
    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $frames += ,@($size,$stream.ToArray())
    if ($size -eq 256) { $bitmap.Save((Join-Path $assetDir 'FocusDesk.png'),[Drawing.Imaging.ImageFormat]::Png) }
    $stream.Dispose(); $format.Dispose(); $font.Dispose(); $brush.Dispose(); $path.Dispose(); $g.Dispose(); $bitmap.Dispose()
}
$file = [IO.File]::Create((Join-Path $assetDir 'FocusDesk.ico'))
$writer = [IO.BinaryWriter]::new($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
$offset = 6+16*$frames.Count
foreach ($frame in $frames) {
    $dimension = if ($frame[0] -eq 256) {0} else {$frame[0]}
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$frame[1].Length); $writer.Write([uint32]$offset)
    $offset += $frame[1].Length
}
foreach ($frame in $frames) { $writer.Write([byte[]]$frame[1]) }
$writer.Dispose()
