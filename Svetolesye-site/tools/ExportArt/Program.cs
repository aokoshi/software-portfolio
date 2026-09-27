using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Svetolesye.Game;
class Program {
 [STAThread] static void Main() {
 foreach(var id in new[]{"moss","spark","drop","verdant","flare","tide","keeper","astral"}) {
 var visual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.NearestNeighbor);
 using(var dc=visual.RenderOpen())dc.DrawImage(Art.Creature(id),new Rect(0,0,160,160));
 var bitmap=new RenderTargetBitmap(160,160,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
 var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
 using var file=File.Create(Path.Combine("dist/assets",id+".png"));encoder.Save(file);
 }
 }
}
