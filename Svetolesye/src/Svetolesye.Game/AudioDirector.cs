using System.IO;
using System.Windows.Media;
using Svetolesye.Core;
namespace Svetolesye.Game;

// Independent players let ambience, music and short effects overlap without cutting one another off.
public sealed class AudioDirector : IDisposable
{
    readonly MediaPlayer music=new(),ambience=new();
    readonly MediaPlayer[] effects=Enumerable.Range(0,6).Select(_=>new MediaPlayer()).ToArray();
    readonly string directory;
    string musicName="",ambientName="";
    int effectIndex;
    bool active,started;
    double fade;
    public AudioSettings Settings {get;}
    public string Status {get;private set;}="Готово: музыка, окружение и эффекты";
    public AudioDirector(string directory,AudioSettings settings)
    {
        this.directory=directory;Settings=settings;
        foreach(var player in effects.Append(music).Append(ambience))player.MediaFailed+=(_,e)=>Status="Не удалось воспроизвести звук: "+e.ErrorException.Message;
        music.MediaEnded+=(_,_)=>{if(active&&!Settings.Muted){music.Position=TimeSpan.Zero;music.Play();}};
        ambience.MediaEnded+=(_,_)=>{if(active&&!Settings.Muted){ambience.Position=TimeSpan.Zero;ambience.Play();}};
    }
    public void Start(){started=true;SetActive(true);}
    public void SetActive(bool value)
    {
        active=value&&started;
        if(!active||Settings.Muted){music.Pause();ambience.Pause();foreach(var p in effects)p.Stop();}
        else {if(musicName.Length>0)music.Play();if(ambientName.Length>0)ambience.Play();}
    }
    public void Changed(){Settings.Clamp();SetActive(active);Volumes();}
    void Volumes(){music.Volume=Settings.Muted?0:Settings.Music/100.0*fade;ambience.Volume=Settings.Muted?0:Settings.Ambience/100.0;foreach(var p in effects)p.Volume=Settings.Muted?0:Settings.Effects/100.0;}
    void Track(MediaPlayer player,string name,ref string current)
    {
        if(current==name)return;player.Stop();current=name;
        if(name.Length==0){player.Close();return;}
        string path=Path.Combine(directory,name+".wav");
        if(!File.Exists(path)){Status="Не найден звуковой файл: "+name;return;}
        player.Open(new Uri(path));if(player==music)fade=0;
        if(active&&!Settings.Muted)player.Play();
    }
    public void Update(GameEngine game,double elapsed)
    {
        if(!started)return;
        string track=game.Screen==Screen.Battle?(game.Fight!.Boss?"boss":"battle"):game.Data?.Region switch {Region.Coast=>"coast",Region.Summit=>"summit",_=>"valley"};
        string ambient=game.Screen is Screen.World or Screen.Dialogue?game.Data?.Region switch{Region.Coast=>"ambient-sea",Region.Summit=>"ambient-wind",_=>"ambient-forest"}:"";
        Track(music,track,ref musicName);Track(ambience,ambient,ref ambientName);fade=Math.Min(1,fade+Math.Max(0,elapsed)*2);Volumes();
    }
    public void Play(GameEffect effect)
    {
        if(!started||!active||Settings.Muted||Settings.Effects==0)return;
        string name=effect.Kind=="special"?effect.Element.ToString().ToLowerInvariant():effect.Kind=="reply"?"hit":effect.Kind;
        string path=Path.Combine(directory,"fx-"+name+".wav");if(!File.Exists(path))return;
        var player=effects[effectIndex++%effects.Length];player.Stop();player.Open(new Uri(path));player.Volume=Settings.Effects/100.0;player.Play();
    }
    public void Dispose(){music.Close();ambience.Close();foreach(var p in effects)p.Close();}
}
