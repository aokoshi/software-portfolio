using System.Text;
using System.Text.Json;

string output=Path.GetFullPath(args.FirstOrDefault()??"src/Svetolesye.Game/Assets/Audio");Directory.CreateDirectory(output);
var manifests=new List<object>();
void Save(string name,Sound sound,string kind)
{
 var stats=sound.Write(Path.Combine(output,name+".wav"),kind=="music"?.65:kind=="ambience"?.3:.60);manifests.Add(new{name,kind,seconds=sound.Seconds,sampleRate=Sound.Rate,channels=2,peak=stats.Peak,rms=stats.Rms});Console.WriteLine($"{name}: {sound.Seconds:0.0}s, peak {stats.Peak:0.000}");
}
// All motifs, harmonies and synthesis below are original, deterministic source assets.
foreach(var (name,bpm,root,mode) in new[]{("valley",94,60,0),("coast",86,62,1),("summit",78,57,2),("battle",132,57,3),("boss",144,55,4)})
{
 double beat=60.0/bpm;int bars=8;var song=new Sound(bars*4*beat);int[] roots=mode==2?[0,5,3,7,0,5,8,7]:mode>=3?[0,5,3,7,0,8,5,7]:[0,5,9,7,0,5,2,7];
 int[][] motifs=mode switch {
  0=>[[12,16,19,16,14,12,9,14],[12,14,16,19,21,19,16,14],[16,19,24,21,19,16,14,12],[14,16,19,14,11,14,7,11]],
  1=>[[12,14,19,21,19,14,12,7],[9,12,14,16,14,12,9,7],[16,19,21,24,21,19,14,12],[14,11,7,11,14,19,14,11]],
  2=>[[12,15,19,22,19,15,12,10],[12,17,19,24,22,19,17,15],[15,19,22,24,22,19,15,12],[14,17,19,23,19,17,14,11]],
  _=>[[12,12,19,15,17,19,22,19],[17,17,24,20,19,17,15,14],[15,19,22,19,17,15,14,12],[14,14,19,23,19,17,14,11]]};
 for(int bar=0;bar<bars;bar++)
 {
  double t=bar*4*beat;int chordRoot=root+roots[bar];bool minor=mode>=2||roots[bar]==9;
  foreach(int offset in new[]{0,minor?3:4,7})song.Tone(t,3.85*beat,chordRoot+offset,.034,Voice.Pad,offset==0?-.45:.45);
  for(int b=0;b<4;b++){song.Tone(t+b*beat,.76*beat,chordRoot-12+(b==2?7:0),.14,Voice.Bass,0);if(mode>=3||b%2==0)song.Kick(t+b*beat,mode>=3?.14:.075);if(b%2==1)song.Noise(t+b*beat,.10,mode>=3?.055:.022,.15);}
  for(int step=0;step<8;step++)
  {
   double time=t+step*beat/2;int pitch=root+motifs[bar%4][step]+(bar>=4&&step==6?12:0);
   if(mode==2&&step%3==1)continue;
   song.Tone(time,beat*(mode==2?.88:.43),pitch,mode>=3?.16:.115,mode==1?Voice.Bell:mode==2?Voice.Flute:Voice.Lead,Math.Sin(step*.7)*.18);
   song.Tone(time,beat*.35,chordRoot+12+new[]{0,minor?3:4,7,12}[step%4],.032,Voice.Bell,step%2==0?-.65:.65);
   if(mode>=3)song.Noise(time,.025,.018,-.25);
  }
 }
 song.Echo(.22*beat,.16);Save(name,song,"music");
}
foreach(string zone in new[]{"forest","sea","wind"})
{
 var ambience=new Sound(24);var rng=new Random(zone=="forest"?120:zone=="sea"?230:340);double smooth=0,slow=0;
 for(int i=0;i<ambience.Left.Length;i++)
 {
  double t=(double)i/Sound.Rate;smooth=.965*smooth+.035*(rng.NextDouble()*2-1);slow=.995*slow+.005*(rng.NextDouble()*2-1);
  double swell=zone=="sea"?.45+.45*Math.Pow(Math.Sin(t*.55),2):zone=="wind"?.4+.35*Math.Sin(t*.32):.3;
  double value=(smooth*(zone=="forest"?.23:.7)+slow)*swell;
  ambience.Left[i]+=value;ambience.Right[i]+=value*(.85+.1*Math.Sin(t*.8));
 }
 if(zone=="forest")for(int i=0;i<13;i++){double t=.8+i*1.75;ambience.Chirp(t,.16,1750+120*(i%4),2500+100*(i%3),.025,Math.Sin(i)*.8);ambience.Chirp(t+.23,.10,2400,1900,.02,Math.Sin(i)*.8);}
 if(zone=="sea")for(int i=0;i<5;i++)ambience.Chirp(2+i*4.7,.45,820,680,.018,.65);
 if(zone=="wind")for(int i=0;i<7;i++)ambience.Tone(1+i*3.2,1.3,81+(i%3)*7,.017,Voice.Bell,Math.Sin(i));
 Save("ambient-"+zone,ambience,"ambience");
}
foreach(string name in new[]{"hit","leaf","ember","water","stone","air","capture","miss","heal","level","evolution","portal","beacon","victory","lose","encounter","click"})
{
 double duration=name=="evolution"?3.2:name is "victory" or "level" or "capture"?1.4:name=="heal"?1.2:name=="lose"?1.5:.7;
 var fx=new Sound(duration);
 switch(name)
 {
  case "hit":fx.Noise(0,.13,.32,0);fx.Chirp(0,.2,160,55,.35,0);break;
  case "leaf":for(int i=0;i<4;i++){fx.Noise(i*.09,.12,.1,i%2==0?-.5:.5);fx.Tone(i*.09,.13,76+i*2,.15,Voice.Flute,0);}break;
  case "ember":fx.Noise(0,.5,.18,0);fx.Chirp(0,.35,180,520,.2,0);break;
  case "water":for(int i=0;i<5;i++)fx.Chirp(i*.08,.16,380+i*70,850+i*30,.17,Math.Sin(i)*.5);break;
  case "stone":for(int i=0;i<3;i++){fx.Kick(i*.13,.35);fx.Noise(i*.13,.12,.2,0);}break;
  case "air":fx.Noise(0,.5,.1,0);fx.Chirp(0,.5,800,1800,.06,.4);break;
  case "miss":fx.Tone(0,.18,67,.18,Voice.Lead,0);fx.Tone(.18,.32,60,.18,Voice.Lead,0);break;
  case "lose":foreach(var (n,i) in new[]{72,67,63,60}.Select((n,i)=>(n,i)))fx.Tone(i*.25,.5,n,.2,Voice.Bell,0);break;
  case "evolution":for(int i=0;i<20;i++)fx.Tone(i*.11,.3,60+new[]{0,4,7,12,16}[i%5]+(i/5)*2,.13,Voice.Bell,Math.Sin(i)*.7);foreach(int n in new[]{72,76,79,84})fx.Tone(2.1,.9,n,.09,Voice.Pad,0);break;
  case "capture":case "victory":case "level":case "heal":
   int[] notes=name=="heal"?[72,76,79,84]:name=="level"?[67,72,76,79,84]:[60,64,67,72,79];
   for(int i=0;i<notes.Length;i++)fx.Tone(i*.14,.45,notes[i],.19,Voice.Bell,(i-2)*.15);break;
  case "portal":case "beacon":fx.Chirp(0,.45,240,950,.18,0);fx.Tone(.25,.4,84,.16,Voice.Bell,.3);break;
  case "encounter":fx.Tone(0,.13,55,.22,Voice.Lead,0);fx.Tone(.15,.13,62,.22,Voice.Lead,0);fx.Tone(.30,.25,67,.2,Voice.Lead,0);break;
  default:fx.Tone(0,.09,79,.15,Voice.Bell,0);break;
 }
 Save("fx-"+name,fx,"effect");
}
File.WriteAllText(Path.Combine(output,"manifest.json"),JsonSerializer.Serialize(manifests,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Generated {manifests.Count} original stereo WAV assets.");

enum Voice{Lead,Bass,Bell,Pad,Flute}
sealed class Sound(double seconds)
{
 public const int Rate=22050;
 public double Seconds{get;}=seconds;
 public double[] Left{get;}=new double[(int)(seconds*Rate)];
 public double[] Right{get;}=new double[(int)(seconds*Rate)];
 public void Tone(double start,double duration,int midi,double volume,Voice voice,double pan)
 {
  double frequency=440*Math.Pow(2,(midi-69)/12.0);
  Add(start,duration,pan,(t,u)=>{
   double p=2*Math.PI*frequency*t,s=Math.Sin(p);
   double tone=voice switch{Voice.Lead=>s*.72+Math.Sin(p*3)*.16+Math.Sin(p*5)*.07,Voice.Bass=>s*.9+Math.Sin(p*2)*.1,Voice.Bell=>s*.8+Math.Sin(p*2.01)*.18*Math.Exp(-t*9),Voice.Pad=>s*.65+Math.Sin(p*1.003)*.2+Math.Sin(p*2)*.1,_=>s*.92+Math.Sin(p*2)*.08};
   double decay=voice==Voice.Bell?Math.Exp(-u*4):voice==Voice.Pad?.8:1-.45*u;
   return tone*volume*decay;
  });
 }
 public void Chirp(double start,double duration,double from,double to,double volume,double pan)=>Add(start,duration,pan,(t,u)=>Math.Sin(2*Math.PI*(from*t+(to-from)*t*t/(2*duration)))*volume*(1-u));
 public void Noise(double start,double duration,double volume,double pan)
 {
  var rng=new Random((int)(start*10000)+123);double filtered=0;
  Add(start,duration,pan,(t,u)=>{filtered=filtered*.65+(rng.NextDouble()*2-1)*.35;return filtered*volume*Math.Exp(-u*5);});
 }
 public void Kick(double start,double volume)=>Add(start,.18,0,(t,u)=>Math.Sin(2*Math.PI*(62*t+65*(1-Math.Exp(-t*35))/35))*Math.Exp(-u*7)*volume);
 void Add(double start,double duration,double pan,Func<double,double,double> wave)
 {
  int first=(int)(start*Rate),count=(int)(duration*Rate);double l=Math.Sqrt((1-pan)*.5),r=Math.Sqrt((1+pan)*.5);
  for(int i=0;i<count&&first+i<Left.Length;i++)
  {
   double t=(double)i/Rate,u=(double)i/count,env=Math.Min(1,t/.012)*Math.Min(1,(duration-t)/.045);double v=wave(t,u)*Math.Max(0,env);
   Left[first+i]+=v*l;Right[first+i]+=v*r;
  }
 }
 public void Echo(double delay,double feedback)
 {
  int n=(int)(delay*Rate);for(int i=Left.Length-1;i>=n;i--){Left[i]+=Right[i-n]*feedback;Right[i]+=Left[i-n]*feedback;}
 }
 public (double Peak,double Rms) Write(string path,double target)
 {
  double raw=Left.Concat(Right).Max(Math.Abs),gain=raw>0?target/raw:1;
  using var writer=new BinaryWriter(File.Create(path));int dataSize=Left.Length*4;
  writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+dataSize);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
  writer.Write(16);writer.Write((short)1);writer.Write((short)2);writer.Write(Rate);writer.Write(Rate*4);writer.Write((short)4);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(dataSize);
  double sum=0,peak=0;
  for(int i=0;i<Left.Length;i++)
  {
   double edge=Math.Min(1,Math.Min((double)i/400,(double)(Left.Length-1-i)/400));
   foreach(double sample in new[]{Left[i],Right[i]}){double v=sample*gain*edge;peak=Math.Max(peak,Math.Abs(v));sum+=v*v;writer.Write((short)Math.Round(Math.Clamp(v,-1,1)*32767));}
  }
  return(peak,Math.Sqrt(sum/(Left.Length*2)));
 }
}
