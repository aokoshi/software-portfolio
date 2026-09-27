Add-Type -AssemblyName System.Drawing
$assetDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/Svetolesye.Game/Assets'))
$rows=@('......33........','.....333...3....','......232333....','.......233......','....########....','...#11111111#...','..#3111111111#..','..#11W#11W#11#..','..#1111111111#..','..#1111RR1111#..','...#12222221#...','..#1112222111#..','..#1122222211#..','...#22222222#...','...##22##22##...','....###..###....')
$colors=@{'#'='#334a45';'1'='#73ad54';'2'='#526f62';'3'='#bdd488';'W'='#fff7da';'R'='#b57573'}
$frames=@()
foreach($size in @(16,24,32,48,64,128,256)) {
 $bitmap=[Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
 $g=[Drawing.Graphics]::FromImage($bitmap)
 $g.Clear([Drawing.Color]::Transparent)
 for($y=0;$y -lt 16;$y++){for($x=0;$x -lt 16;$x++){
  $key=[string]$rows[$y][$x]
  if($colors.ContainsKey($key)){
   $brush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($colors[$key]))
   $x1=[int][Math]::Floor($x*$size/16);$y1=[int][Math]::Floor($y*$size/16)
   $x2=[int][Math]::Floor(($x+1)*$size/16);$y2=[int][Math]::Floor(($y+1)*$size/16)
   $g.FillRectangle($brush,$x1,$y1,($x2-$x1),($y2-$y1));$brush.Dispose()
  }
 }}
 $stream=[IO.MemoryStream]::new();$bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
 $frames+=,@($size,$stream.ToArray())
 if($size -eq 256){$bitmap.Save((Join-Path $assetDir 'Svetolesye.png'),[Drawing.Imaging.ImageFormat]::Png)}
 $stream.Dispose();$g.Dispose();$bitmap.Dispose()
}
$writer=[IO.BinaryWriter]::new([IO.File]::Create((Join-Path $assetDir 'Svetolesye.ico')))
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
$offset=6+16*$frames.Count
foreach($frame in $frames){
 $dimension=if($frame[0] -eq 256){0}else{$frame[0]}
 $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0)
 $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame[1].Length);$writer.Write([uint32]$offset)
 $offset+=$frame[1].Length
}
foreach($frame in $frames){$writer.Write([byte[]]$frame[1])}
$writer.Dispose()
