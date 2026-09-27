using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Svetolesye.Core;
namespace Svetolesye.Game;
public sealed partial class GameView : FrameworkElement
{
    public GameEngine Engine {get;}
    public bool HasSave {get;set;}
    public Action? NewRequested,LoadRequested,SaveRequested,QuitRequested;
    public double Animation {get;set;}
    public double PlayerOffsetX {get;set;}
    public double PlayerOffsetY {get;set;}
    public bool FocusLost {get;set;}
    readonly List<(Rect Rect,Action Action,bool Enabled)> hits=[];
    readonly Dictionary<string,Brush> brushes=[];
    DrawingGroup? world;
    Region? worldRegion;
    Point mouse=new(-1,-1);
    DrawingContext dc=null!;
    const string Ink="#304d49",Muted="#798777",Cream="#f5efd9",Gold="#dfb65f",Dark="#243e3d";
    Brush B(string color){if(!brushes.TryGetValue(color,out var brush))brushes[color]=brush=Art.B(color);return brush;}
    void Box(double x,double y,double w,double h,string color)=>dc.DrawRectangle(B(color),null,new Rect(x,y,w,h));
    void Line(double x,double y,double w,string color)=>Box(x,y,w,2,color);
    void Text(string value,double x,double y,double size=16,string color=Ink,bool bold=false,double max=900)
    {
        var text=new FormattedText(value,CultureInfo.GetCultureInfo("ru-RU"),FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,B(color),1){MaxTextWidth=Math.Max(1,max),MaxTextHeight=600};
        dc.DrawText(text,new Point(x,y));
    }
    void Panel(double x,double y,double w,double h,string fill=Cream)
    {
        Box(x+4,y+5,w,h,"#b1b99b");Box(x,y,w,h,Dark);Box(x+2,y+2,w-4,h-4,fill);Box(x+5,y+5,w-10,2,"#fffae8");
    }
    void Button(string label,double x,double y,double w,Action action,bool primary=false,bool enabled=true,double h=40)
    {
        var rect=new Rect(x,y,w,h);bool hovered=rect.Contains(mouse)&&enabled;
        string fill=!enabled?"#d4d5c2":primary?(hovered?"#51775d":"#3a604f"):(hovered?"#ece0b8":"#fcf6df");
        Box(x+2,y+3,w,h,"#b0b497");Box(x,y,w,h,Ink);Box(x+2,y+2,w-4,h-4,fill);
        Text(label,x+12,y+(h-22)/2,14,primary&&enabled?"#fff6dc":enabled?Ink:"#96a08a",true,w-20);
        hits.Add((rect,action,enabled));
    }
    void Sprite(string id,double x,double y,double size,bool flipped=false)
    {
        if(flipped){dc.PushTransform(new ScaleTransform(-1,1,x+size/2,y));dc.DrawImage(Art.Creature(id),new Rect(x,y,size,size));dc.Pop();}
        else dc.DrawImage(Art.Creature(id),new Rect(x,y,size,size));
    }
    void Hp(Creature c,double x,double y,double w,bool detailed=true)
    {
        Box(x,y,w,8,"#b6bc9d");Box(x,y,w*(double)c.Hp/c.MaxHp,8,c.Hp>c.MaxHp*.5?"#75a765":c.Hp>c.MaxHp*.25?"#d5a755":"#c6795d");
        if(detailed)Text($"{c.Hp} / {c.MaxHp} здоровья",x,y+13,12,Muted);
    }
    public GameView(GameEngine engine)
    {
        Engine=engine;Width=960;Height=640;Focusable=true;
        RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.NearestNeighbor);RenderOptions.SetEdgeMode(this,EdgeMode.Aliased);
        MouseMove+=(_,e)=>{mouse=e.GetPosition(this);Cursor=hits.Any(h=>h.Enabled&&h.Rect.Contains(mouse))?Cursors.Hand:Cursors.Arrow;InvalidateVisual();};
        MouseLeave+=(_,_)=>{mouse=new(-1,-1);InvalidateVisual();};
        MouseLeftButtonDown+=(_,e)=>{Focus();var item=hits.LastOrDefault(h=>h.Enabled&&h.Rect.Contains(e.GetPosition(this)));if(item.Action!=null){UiSound?.Invoke();item.Action();}InvalidateVisual();};
    }
    protected override void OnRender(DrawingContext context)
    {
        dc=context;hits.Clear();Box(0,0,960,640,Cream);
        switch(Engine.Screen)
        {
            case Screen.Title:case Screen.ConfirmNew:TitleScene();break;
            case Screen.Starters:StarterScene();break;
            case Screen.Battle:BattleScene();break;
            case Screen.Team:TeamScene();break;
            case Screen.Collection:CollectionScene();break;
            case Screen.Help:HelpScene();break;
            case Screen.Victory:VictoryScene();break;
            case Screen.Evolution:EvolutionScene();break;
            default:WorldScene();if(Engine.Screen==Screen.Dialogue)DialogueScene();break;
        }
        DrawEffects();
        if(AudioPanelOpen)AudioScene();
        if(!string.IsNullOrWhiteSpace(Engine.Toast))
        {
            Box(70,18,820,48,"#314e47");Text(Engine.Toast,86,28,14,"#fff5d9",false,790);
        }
        if(FocusLost&&Engine.Screen==Screen.World){Box(325,277,310,66,"#314e47");Text("Нажми на окно, чтобы продолжить",346,300,14,"#fff5d9");}
    }
    void Header(string title,string subtitle)
    {
        Box(0,0,960,64,Dark);Text("СВЕТОЛЕСЬЕ",24,12,22,"#f4e6b7",true);Text(subtitle,26,40,10,"#91b09b",false,500);
        Text(title,390,24,14,"#cbd7b0",true,330);
    }
    void DrawWorldTiles()
    {
        var region=Engine.Map.Region;
        if(worldRegion!=region){world=null;worldRegion=region;}
        if(world!=null){dc.DrawDrawing(world);return;}
        world=new DrawingGroup();var original=dc;dc=world.Open();
        string ground=region==Region.Coast?"#e1cfaa":region==Region.Summit?"#a5a9c2":"#afca86";
        string path=region==Region.Coast?"#efdbaf":region==Region.Summit?"#c8bed5":"#ddce9b";
        string grass=region==Region.Coast?"#93b8a0":region==Region.Summit?"#9295b7":"#9abc73";
        string blade=region==Region.Coast?"#5b9085":region==Region.Summit?"#6c6d98":"#638d51";
        string water=region==Region.Coast?"#629cad":region==Region.Summit?"#625c83":"#78aa9e";
        for(int y=0;y<World.Height;y++)for(int x=0;x<World.Width;x++)
        {
            double px=24+x*24,py=88+y*24;char tile=Engine.Map.Tiles[x,y];Box(px,py,24,24,ground);
            if(tile=='~'){Box(px,py,24,24,water);Box(px+3,py+6,10,2,region==Region.Summit?"#a499c6":"#b2d4c7");Box(px+13,py+17,8,2,region==Region.Summit?"#8076a4":"#7bb3b8");}
            else if(tile is 'p' or 'b')
            {
                Box(px,py,24,24,tile=='b'?"#967d68":path);
                if(tile=='b'){for(int k=0;k<4;k++)Box(px,py+k*6,24,2,"#d2b795");Box(px,py,2,24,"#605d58");Box(px+22,py,2,24,"#605d58");}
                else if((x+y)%2==0){Box(px+4,py+8,3,2,region==Region.Summit?"#a49abb":"#c7b67f");Box(px+16,py+18,3,2,"#ece0b7");}
            }
            else if(tile=='g')
            {
                Box(px,py,24,24,grass);for(int k=0;k<3;k++){int bx=k*8;Box(px+bx+1,py+7,2,7,blade);Box(px+bx+3,py+10,2,5,blade);Box(px+bx+4,py+5,2,8,region==Region.Summit?"#c4b2dc":"#abc686");}
            }
            else if(tile=='h')Box(px,py,24,24,"#d6c497");
            else if(tile=='#')
            {
                if(region==Region.Valley)dc.DrawImage(Art.Tree,new Rect(px-2,py-6,28,30));
                else {Box(px+2,py+7,21,17,region==Region.Coast?"#8b9e98":"#726985");Box(px+5,py+2,15,7,region==Region.Coast?"#adbbb0":"#a696bb");Box(px+5,py+9,4,6,region==Region.Coast?"#c8ceb0":"#cebde3");}
            }
            else if((x*7+y*13)%19==0){Box(px+7,py+12,3,6,blade);Box(px+5,py+10,7,3,region==Region.Summit?"#e7d1ff":"#f2d37d");}
            else if((x+y*3)%7==0)Box(px+6,py+12,2,3,blade);
        }
        if(region==Region.Valley){House(24+3*24,88+10*24,96,"#b96b58",true);House(24+9*24,88+10*24,72,"#789092",false);}
        else if(region==Region.Coast)House(24+3*24,88+11*24,72,"#699d9e",true);
        else{Box(24+2*24,88+14*24,56,9,"#e5ca94");Box(24+2*24+9,88+14*24-12,38,14,"#b598b5");Box(24+2*24+18,88+14*24-22,20,12,"#b598b5");}
        foreach(var (x,y) in new[]{(18,3),(20,3),(18,5),(20,5)}){if(Engine.Map.Tiles[x,y]=='~')continue;Box(24+x*24+4,88+y*24+5,16,15,"#899482");Box(24+x*24+6,88+y*24+5,12,4,"#bac3a3");}
        dc.Close();dc=original;world.Freeze();dc.DrawDrawing(world);
    }
    void House(double x,double y,double w,string roof,bool clinic)
    {
        Box(x+4,y+18,w-8,50,"#344f46");Box(x+6,y+20,w-12,46,"#ead9ab");
        Box(x,y+10,w,25,roof);Box(x+8,y+2,w-16,10,roof);Box(x+16,y-4,w-32,8,roof);
        for(int i=0;i<3;i++)Box(x+3,y+13+i*7,w-6,2,"#51674e");
        Box(x+w/2-8,y+43,16,25,"#6d7059");Box(x+12,y+42,14,13,"#628d8c");Box(x+15,y+44,4,8,"#b4ceaf");
        if(clinic){Box(x+w-26,y+41,16,16,"#fbedd1");Box(x+w-21,y+43,5,12,"#b7675c");Box(x+w-24,y+47,11,4,"#b7675c");}
    }
    void WorldScene()
    {
        var data=Engine.Data!;Header(Engine.Map.Name.ToUpperInvariant(),"Три земли · одно большое путешествие");
        Button("Звук",725,15,75,ShowAudio,h:34);Button("F5  Сохранить",810,15,128,()=>SaveRequested?.Invoke(),h:34);
        Text(data.Region switch {Region.Coast=>"ГЛАВА II   /   ТРИ МАЯКА   /   ЗЕРКАЛЬНЫЕ ВОДЫ",Region.Summit=>"ГЛАВА III   /   ДВА СТРАЖА   /   ЗВЁЗДНАЯ ВЕРШИНА",_=>"ГЛАВА I   /   ПОСЁЛОК   /   РОЩА"},24,69,10,Muted,true);
        Box(20,84,632,488,Ink);DrawWorldTiles();WorldAtmosphere();
        foreach(var npc in Engine.Map.Residents)
        {
            double x=24+npc.X*24,y=88+npc.Y*24;
                        if(npc.Kind=="keeper")Sprite(Engine.Map.BossId,x-8,y-18,40);
            else if(npc.Kind.StartsWith("beacon-")){bool lit=data.Beacons.Contains(npc.Kind);Box(x+8,y+10,10,15,"#7a8c8b");Box(x+5,y+3,16,12,lit?"#fff3a6":"#98b2c1");Box(x+8,y,10,5,lit?"#ffe074":"#7395a7");if(lit){Box(x+1,y-5,3,3,"#fff4b2");Box(x+21,y+1,3,3,"#fff4b2");}}
            else if(npc.Kind.StartsWith("trial-")){Sprite(npc.Kind=="trial-air"?"luna":"boulder",x-5,y-12,34);if(data.Trials.Contains(npc.Kind))Text("✓",x+10,y-28,16,"#fff2ac",true);}
            else if(npc.Kind=="sign"){Box(x+10,y+9,4,16,"#847955");Box(x+2,y+2,20,12,"#a89968");Box(x+4,y+4,16,2,"#ece0ae");}
            else dc.DrawImage(Art.Person(npc.Kind),new Rect(x,y-6,24,28));
        }
        Box(24+data.X*24+4+PlayerOffsetX,88+data.Y*24+20+PlayerOffsetY,18,5,"#789567");
        dc.DrawImage(Art.Person("player",Engine.Facing.Y<0?1:0,data.Steps%2),new Rect(24+data.X*24+PlayerOffsetX,88+data.Y*24-5+PlayerOffsetY,24,28));
        var nearby=Engine.Map.Residents.Any(n=>Math.Abs(n.X-data.X)+Math.Abs(n.Y-data.Y)==1);
        if(nearby){Box(24+data.X*24+8,88+data.Y*24-25,18,18,Cream);Text("E",24+data.X*24+12,88+data.Y*24-25,13,Ink,true);}
        Panel(672,88,264,132);Text("ПУТЕШЕСТВИЕ · "+((int)data.Region+1)+" / 3",689,105,11,Muted,true);
        string goal=data.Region switch {Region.Coast=>data.TideDefeated?"Берег пройден":"Зажги три маяка",Region.Summit=>data.SummitDefeated?"Три земли спасены":"Испытай команду",_=>data.KeeperDefeated?"Роща пройдена":"Найди новых друзей"};
        string progress=data.Region switch {Region.Coast=>$"Маяки: {data.Beacons.Count} / 3",Region.Summit=>$"Стражи: {data.Trials.Count} / 2",_=>$"Семейства: {Math.Min(3,Engine.DistinctSpecies)} / 3"};
        string hint=data.Region switch {Region.Coast=>data.TideDefeated?"Арка на северо-востоке →":data.Beacons.Count==3?"Приливень ждёт на севере":"Маяки: запад, юг и восток",Region.Summit=>data.SummitDefeated?"Собери все развитые формы":data.Trials.Count==2?"Астрарон ждёт на вершине":"Стражи: северо-запад и юго-восток",_=>data.KeeperDefeated?"Восточная арка ведёт к берегу":DistinctHint()};
        Text(goal,689,127,19,Ink,true,235);Text(progress,689,159,14,Ink,false,230);Text(hint,689,187,12,Muted,false,230);
        Panel(672,235,264,249);Text("ТВОЯ КОМАНДА",689,251,11,Muted,true);
        for(int i=0;i<data.Team.Count;i++)
        {
            var c=data.Team[i];double y=278+i*31;Sprite(c.SpeciesId,687,y-3,26);Text((i==data.Lead?"› ":"")+c.Species.Name,720,y-3,13,Ink,true,146);Text("ур. "+c.Level,878,y-2,11,Muted);Hp(c,720,y+17,182,false);
        }
        if(data.Team.Count<6)Text("Новые спутники появятся здесь",689,463,10,Muted);
        Panel(672,500,264,70);Text("ПРИПАСЫ",689,514,10,Muted,true);Text($"✦ Семена: {data.Seeds}     Тоники: {data.Tonics}",689,536,13,Ink,true);
        Button("E  Поговорить",24,592,160,()=>Engine.Interact());Button("C  Команда",196,592,146,()=>Engine.Open(Screen.Team));Button("Tab  Коллекция",354,592,162,()=>Engine.Open(Screen.Collection));Button("?  Помощь",528,592,124,()=>Engine.Open(Screen.Help));
        Text("WASD / стрелки — идти\nShift — быстрый шаг",687,591,12,Muted,false,250);
    }
    string DistinctHint()=>Engine.DistinctSpecies>=3?"Ветрокрон ждёт в роще":"Ищи существ в высокой траве";
    void DialogueScene()
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(95,26,51,43)),null,new Rect(0,64,960,576));
        hits.Clear();Panel(48,379,864,241);Text(Engine.Speaker.ToUpperInvariant(),70,398,13,"#8b7250",true,800);Line(70,426,820,"#c4c8a8");
        Text(Engine.Dialogue,70,442,16,Ink,false,818);Button("E  Далее →",744,565,145,()=>Engine.Continue(),true);
    }
}
