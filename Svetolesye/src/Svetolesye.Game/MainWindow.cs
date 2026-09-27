using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Svetolesye.Core;
namespace Svetolesye.Game;
public sealed class MainWindow : Window
{
    readonly GameEngine engine=new();
    readonly SaveStore saves;
    readonly AudioDirector audio;
    readonly string audioSettingsPath;
    readonly List<(double When,GameEffect Effect)> soundQueue=[];
    double lastTick;
    readonly GameView view;
    readonly HashSet<Key> pressed=[];
    readonly Stopwatch watch=Stopwatch.StartNew();
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(25)};
    double nextMove,moveStarted,moveDuration=.15,toastUntil;
    double oldOffsetX,oldOffsetY;
    string lastToast="";
    public MainWindow(SaveStore store)
    {
        Icon=new BitmapImage(new Uri("pack://application:,,,/Assets/Svetolesye.ico"));saves=store;Title="Светолесье — путешествие трёх земель";Width=1152;Height=808;MinWidth=800;MinHeight=580;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Art.B("#243e3d");
        view=new GameView(engine){HasSave=saves.Exists};Content=new Viewbox{Child=view,Stretch=Stretch.Uniform};
                audioSettingsPath=Path.Combine(Path.GetDirectoryName(saves.FilePath)!,"audio-settings.json");
        audio=new AudioDirector(Path.Combine(AppContext.BaseDirectory,"Assets","Audio"),AudioSettings.Load(audioSettingsPath));
        view.Audio=audio.Settings;view.AudioChanged=()=>{audio.Changed();try{audio.Settings.Save(audioSettingsPath);}catch(Exception ex){engine.Toast="Не удалось сохранить громкость: "+ex.Message;}};
        view.UiSound=()=>audio.Play(new("click"));
        engine.Effect+=effect=>{double delay=effect.Kind=="reply"?.55:0;view.AddEffect(effect,delay);soundQueue.Add((watch.Elapsed.TotalSeconds+delay,effect));};
        engine.SaveRequested+=Save;
        view.NewRequested=()=>{if(saves.Exists)engine.ConfirmNew();else engine.StartChoosing();};
        view.LoadRequested=Load;view.SaveRequested=()=>{engine.Toast="";engine.Persist();if(string.IsNullOrEmpty(engine.Toast))engine.Toast="Путешествие сохранено.";};view.QuitRequested=Close;
        PreviewKeyDown+=HandleKey;PreviewKeyUp+=(_,e)=>pressed.Remove(e.Key);
        Deactivated+=(_,_)=>{pressed.Clear();view.FocusLost=true;audio.SetActive(false);view.InvalidateVisual();};Activated+=(_,_)=>{view.FocusLost=false;audio.SetActive(true);view.InvalidateVisual();};
        Closing+=CloseGame;Closed+=(_,_)=>{timer.Stop();audio.Dispose();};Loaded+=(_,_)=>{view.Focus();audio.Start();};
        timer.Tick+=(_,_)=>Tick();timer.Start();
    }
    void Save(SaveData data)
    {
        try{saves.Save(data);view.HasSave=true;}
        catch(Exception ex){engine.Toast="Не удалось сохранить: "+ex.Message;}
    }
    void Load()
    {
        try{var loaded=saves.Load();engine.Load(loaded.Data);engine.Persist();if(loaded.Recovered)engine.Toast="Восстановлено предыдущее сохранение из резервной копии.";}
        catch(Exception ex){engine.Toast=ex.Message;}view.InvalidateVisual();
    }
    void CloseGame(object? sender,CancelEventArgs e)
    {
        var snapshot=engine.SafeSnapshot();if(snapshot==null)return;
        try{saves.Save(snapshot);}
        catch(Exception ex){e.Cancel=true;MessageBox.Show(this,"Не удалось сохранить игру: "+ex.Message,"Светолесье",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
    void HandleKey(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.System)return;
        if(e.Key==Key.M&&!e.IsRepeat){audio.Settings.Muted=!audio.Settings.Muted;view.AudioChanged?.Invoke();engine.Toast=audio.Settings.Muted?"Звук выключен (M)":"Звук включён (M)";view.InvalidateVisual();return;}pressed.Add(e.Key);e.Handled=true;if(e.IsRepeat)return;
        if(view.AudioPanelOpen){pressed.Clear();if(e.Key==Key.Escape)view.CloseAudio();view.InvalidateVisual();return;}
        bool confirm=e.Key is Key.E or Key.Enter or Key.Space;
        int number=e.Key>=Key.D1&&e.Key<=Key.D6?(int)e.Key-(int)Key.D1+1:e.Key>=Key.NumPad1&&e.Key<=Key.NumPad6?(int)e.Key-(int)Key.NumPad1+1:0;
        switch(engine.Screen)
        {
            case Screen.Title:if(confirm){if(saves.Exists)Load();else engine.StartChoosing();}else if(e.Key==Key.N)view.NewRequested?.Invoke();break;
            case Screen.ConfirmNew:if(confirm)engine.StartChoosing();if(e.Key==Key.Escape)engine.Title();break;
            case Screen.Starters:if(number is >=1 and <=3)engine.Start(Bestiary.All[number-1].Id);break;
            case Screen.Dialogue:case Screen.Evolution:if(confirm)engine.Continue();break;
            case Screen.Battle:
                if(confirm&&engine.Fight!.Waiting&&!view.BattleFxBusy)engine.Continue();
                else switch(number){case 1:engine.Attack(false);break;case 2:engine.Attack(true);break;case 3:engine.Capture();break;case 4:engine.Tonic();break;case 5:engine.Swap();break;case 6:engine.Run();break;}
                break;
            case Screen.World:
                if(confirm)engine.Interact();else if(e.Key==Key.C)engine.Open(Screen.Team);else if(e.Key==Key.Tab)engine.Open(Screen.Collection);else if(e.Key==Key.F5)view.SaveRequested?.Invoke();else if(e.Key is Key.Escape or Key.OemQuestion or Key.F1)engine.Open(Screen.Help);break;
            case Screen.Collection:if(e.Key is Key.Left or Key.A)view.ChangeCollectionPage(-1);else if(e.Key is Key.Right or Key.D)view.ChangeCollectionPage(1);else if(e.Key is Key.Escape or Key.Tab)engine.Open(Screen.World);break;
            case Screen.Team:if(number>0)engine.SetLead(number-1);if(e.Key is Key.Escape or Key.C)engine.Open(Screen.World);break;
            default:if(e.Key is Key.Escape or Key.Tab||confirm)engine.Open(Screen.World);break;
        }
        view.InvalidateVisual();
    }
    void Tick()
    {
        double now=watch.Elapsed.TotalSeconds;view.Animation=now;audio.Update(engine,now-lastTick);lastTick=now;view.AudioStatus=audio.Status;
        foreach(var item in soundQueue.Where(s=>s.When<=now).ToArray()){audio.Play(item.Effect);soundQueue.Remove(item);}
        if(engine.Screen==Screen.World&&!view.AudioPanelOpen&&now>=nextMove)
        {
            int dx=0,dy=0;
            if(pressed.Contains(Key.Left)||pressed.Contains(Key.A))dx=-1;else if(pressed.Contains(Key.Right)||pressed.Contains(Key.D))dx=1;
            else if(pressed.Contains(Key.Up)||pressed.Contains(Key.W))dy=-1;else if(pressed.Contains(Key.Down)||pressed.Contains(Key.S))dy=1;
            if(dx!=0||dy!=0)
            {
                moveDuration=pressed.Contains(Key.LeftShift)||pressed.Contains(Key.RightShift)?.10:.16;
                var region=engine.Data!.Region;if(engine.Move(dx,dy)){oldOffsetX=region==engine.Data.Region?-dx*24:0;oldOffsetY=region==engine.Data.Region?-dy*24:0;moveStarted=now;}
                nextMove=now+moveDuration;
            }
        }
        double progress=Math.Clamp((now-moveStarted)/moveDuration,0,1);view.PlayerOffsetX=oldOffsetX*(1-progress);view.PlayerOffsetY=oldOffsetY*(1-progress);
        if(engine.Toast!=lastToast){lastToast=engine.Toast;toastUntil=now+5;}
        if(now>toastUntil&&engine.Toast.Length>0){engine.Toast="";lastToast="";}
        view.InvalidateVisual();
    }
    public void RenderCheck(string destination)
    {
        Directory.CreateDirectory(destination);view.HasSave=false;Render("title");
        engine.StartChoosing();Render("starters");engine.Start("moss");Render("dialogue");engine.Continue();Render("world");
        engine.Data!.Team=[Creature.Create("moss",8),Creature.Create("spark",8),Creature.Create("drop",8),Creature.Create("pebble",8),Creature.Create("moth",8),Creature.Create("tide",10)];
        foreach(var species in Bestiary.All)engine.Data.Seen.Add(species.Id);
        engine.Open(Screen.Team);Render("team");engine.Evolve(0);Render("evolution");engine.Continue();
        engine.Open(Screen.Collection);Render("collection");view.ChangeCollectionPage(1);Render("collection-2");view.ChangeCollectionPage(1);Render("collection-3");
        engine.Open(Screen.Help);Render("help");view.ShowAudio();Render("audio");view.CloseAudio();engine.Open(Screen.World);
        engine.BeginBattle(Creature.Create("spark",4),false);engine.Continue();Render("battle");engine.Attack(false);view.Animation+=.3;Render("battle-message");
        var demo=engine.SafeSnapshot()!;demo.KeeperDefeated=true;demo.Region=Region.Coast;demo.X=3;demo.Y=15;engine.Load(demo);Render("coast");
        demo=engine.SafeSnapshot()!;demo.TideDefeated=true;demo.Region=Region.Summit;demo.X=3;demo.Y=16;engine.Load(demo);Render("summit");
        engine.BeginBattle(Creature.Create("astral",16),true);engine.Continue();engine.Fight!.Enemy.Hp=1;engine.Attack(false);engine.Continue();Render("xp");
        while(engine.Screen==Screen.Dialogue)engine.Continue();Render("victory");
        File.WriteAllText(Path.Combine(destination,"render-check.txt"),"17 WPF scenes rendered successfully.");timer.Stop();audio.Dispose();        void Render(string name)
        {
            view.Measure(new Size(960,640));view.Arrange(new Rect(0,0,960,640));view.InvalidateVisual();view.UpdateLayout();
            var bitmap=new RenderTargetBitmap(960,640,96,96,PixelFormats.Pbgra32);bitmap.Render(view);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(destination,name+".png"));encoder.Save(file);
        }
    }
}
