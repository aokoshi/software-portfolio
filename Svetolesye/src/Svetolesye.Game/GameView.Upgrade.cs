using System.Windows;
using System.Windows.Media;
using Svetolesye.Core;
namespace Svetolesye.Game;
public sealed partial class GameView
{
    public AudioSettings Audio {get;set;}=new();
    public string AudioStatus {get;set;}="";
    public Action? AudioChanged,UiSound;
    public bool AudioPanelOpen {get;private set;}
    int collectionPage;
    readonly List<(GameEffect Effect,double Start)> effects=[];
    public bool BattleFxBusy=>effects.Any(e=>Animation<e.Start+.8);
    public void AddEffect(GameEffect effect,double delay=0)=>effects.Add((effect,Animation+delay));
    public void ShowAudio()=>AudioPanelOpen=true;
    public void CloseAudio()=>AudioPanelOpen=false;
    public void ChangeCollectionPage(int delta)=>collectionPage=Math.Clamp(collectionPage+delta,0,(Bestiary.All.Length-1)/6);
    void Xp(Creature c,double x,double y,double width)
    {
        Box(x,y,width,5,"#c4c8bd");Box(x,y,width*(c.Level==Creature.MaxLevel?1:(double)c.Experience/c.NextLevel),5,"#8b88b9");
    }
    void AudioScene()
    {
        hits.Clear();dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(190,25,44,37)),null,new Rect(0,0,960,640));
        Panel(190,110,580,425);Text("Звуки Светолесья",220,134,29,Ink,true);
        Text("Мелодии, природа и маленькие события",221,177,15,Muted);
        string[] labels=["Музыка","Эмбиент","Эффекты"];
        int[] values=[Audio.Music,Audio.Ambience,Audio.Effects];
        for(int i=0;i<3;i++)
        {
            int index=i;double y=226+i*69;
            Text(labels[i],222,y,18,Ink,true);
            Button("−",400,y-4,46,()=>Adjust(index,-10));
            Box(460,y+10,180,7,"#c8ccba");Box(460,y+10,180*values[i]/100.0,7,"#779a83");
            Text(values[i]+"%",651,y,14,Muted);
            Button("+",702,y-4,44,()=>Adjust(index,10));
        }
        Button(Audio.Muted?"M  Включить звук":"M  Выключить звук",220,450,267,()=>{Audio.Muted=!Audio.Muted;AudioChanged?.Invoke();});
        Button("Esc  Готово",504,450,238,CloseAudio,true);
        Text(AudioStatus,221,504,10,Muted,false,521);
        void Adjust(int index,int change)
        {
            if(index==0)Audio.Music=Math.Clamp(Audio.Music+change,0,100);
            if(index==1)Audio.Ambience=Math.Clamp(Audio.Ambience+change,0,100);
            if(index==2)Audio.Effects=Math.Clamp(Audio.Effects+change,0,100);
            AudioChanged?.Invoke();
        }
    }
    void WorldAtmosphere()
    {
        dc.PushClip(new RectangleGeometry(new Rect(24,88,624,480)));
        foreach(var p in Engine.Map.Portals)
        {
            double x=24+p.X*24,y=88+p.Y*24;
            Box(x+1,y-13,4,36,"#927d82");Box(x+20,y-13,4,36,"#927d82");Box(x+1,y-16,23,5,"#f0dc94");
            for(int k=0;k<4;k++)Box(x+6+k*4,y-8+(Math.Sin(Animation*2+k)+1)*9,2,3,"#fff4bd");
        }
        for(int i=0;i<20;i++)
        {
            double x=30+(i*113+Math.Sin(Animation*.4+i)*14)%610;
            double y=95+(i*71+Math.Sin(Animation*.7+i)*8)%465;
            Box(x,y,2,2,Engine.Map.Region==Region.Summit?"#f5e3ff":"#f5efbf");
        }
        dc.Pop();
    }
    void DrawEffects()
    {
        effects.RemoveAll(e=>Animation>e.Start+1.25);
        if(Engine.Screen!=Screen.Battle)return;
        foreach(var (effect,start) in effects)
        {
            double t=Animation-start;if(t<0||t>1.2)continue;
            double x=effect.OnEnemy?748:233,y=effect.OnEnemy?182:331;
            string color=effect.Kind=="heal"?"#b5ef91":effect.Element switch{Element.Ember=>"#ffc16e",Element.Water=>"#9ce4ed",Element.Stone=>"#e5c5a0",Element.Air=>"#dac9ff",_=>"#c1e78a"};
            dc.PushOpacity(Math.Clamp(1-t/1.2,0,1));
            for(int i=0;i<12;i++){double a=i*Math.PI/6+t*2,r=12+t*67;Box(x+Math.Cos(a)*r,y+Math.Sin(a)*r,5,5,color);}
            if(effect.Value>0){Box(x-25,y-58-t*27,60,30,Dark);Text((effect.Kind=="heal"?"+":"−")+effect.Value,x-18,y-57-t*27,20,"#fff7d4",true);}
            dc.Pop();
        }
    }
    void EvolutionScene()
    {
        var e=Engine.Evolution!;var before=Bestiary.Get(e.Before);var after=Bestiary.Get(e.After);
        Box(0,0,960,640,"#d7d8bf");Panel(90,65,780,510);
        Text("НОВАЯ СТУПЕНЬ ДРУЖБЫ",332,98,16,Muted,true);
        Sprite(e.Before,200,191,140);Sprite(e.After,573,170,180);
        Text("→",438,215,55,"#9d895d",true);
        for(int i=0;i<16;i++){double a=i*Math.PI/8+Animation*.35;Box(662+Math.Cos(a)*119,261+Math.Sin(a)*105,4,4,"#b99b62");}
        Text(before.Name+" → "+after.Name,210,375,31,Ink,true,650);
        Text("Уровень "+e.Level+" · характеристики выросли, накопленный опыт сохранён",189,427,16,Muted,false,610);
        Button("E  Вернуться к команде",290,496,380,()=>Engine.Continue(),true,h:46);
    }
}
