using System.Windows;
using System.Windows.Media;
using Svetolesye.Core;
namespace Svetolesye.Game;
public sealed partial class GameView
{
    void TitleScene()
    {
        Box(0,0,960,640,"#d7dfb1");
        Box(0,365,960,275,"#9dbc7c");Box(0,391,960,8,"#aec98b");
        for(int x=0;x<960;x+=52){dc.DrawImage(Art.Tree,new Rect(x,320+(x%3)*6,70,80));dc.DrawImage(Art.Tree,new Rect(x-25,554,74,82));}
        Box(568,360,350,104,"#86a970");Box(610,331,260,130,"#86a970");
        Sprite("keeper",666,195+Math.Round(Math.Sin(Animation*2)*2),192);Sprite("moss",615,427,80);Sprite("spark",729,440,80);Sprite("drop",834,419,80);
        Panel(42,68,510,472,"#f6efd7");
        Text("НЕБОЛЬШАЯ ИГРА О БОЛЬШОЙ ДРУЖБЕ",70,96,11,"#798968",true);
        Text("СВЕТОЛЕСЬЕ",67,134,49,Ink,true,472);
        Text("Путешествие трёх земель",71,199,21,"#8d855d",false,450);
        Line(72,242,448,"#ccc8a4");
        Text("Знакомься с необычными существами.\nОткрой берег и перевал. Развивай команду.",72,265,17,Ink,false,446);
        Button(HasSave?"Продолжить путешествие  ↵":"Начать путешествие  ↵",72,355,448,()=>{if(HasSave)LoadRequested?.Invoke();else Engine.StartChoosing();},true,h:48);
        if(HasSave)Button("N  Новая история",72,419,215,()=>NewRequested?.Invoke());
        Button("Выйти",HasSave?303:72,419,HasSave?217:448,()=>QuitRequested?.Invoke());
        Button("Музыка и звуки",72,475,448,ShowAudio,h:36);
        Box(0,593,960,47,Cream);Text("WASD / стрелки — движение     E / Enter — действие     Мышь — меню",130,604,13,Ink,false,800);
        if(Engine.Screen==Screen.ConfirmNew)
        {
            hits.Clear();dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(160,25,44,37)),null,new Rect(0,0,960,640));Panel(200,204,560,232);
            Text("Начать заново?",226,229,28,Ink,true);Text("Текущее путешествие будет заменено новой историей. Выбери «Отмена», чтобы продолжить свою игру.",226,275,16,Ink,false,504);
            Button("Начать новую историю",226,368,268,()=>Engine.StartChoosing(),true);Button("Отмена",512,368,219,()=>Engine.Title());
        }
    }
    void StarterScene()
    {
        Header("ТВОЙ ПЕРВЫЙ СПУТНИК","История начинается у тихого родника");
        Text("Кто отправится с тобой?",42,89,29,Ink,true);Text("Все трое смогут пройти испытание. Выбери того, кто нравится тебе.",43,133,15,Muted);
        for(int i=0;i<3;i++)
        {
            var s=Bestiary.All[i];double x=42+i*298;Panel(x,181,280,396);
            Box(x+18,201,244,157,new[]{"#d6dfb8","#eed7a8","#c7dcd0"}[i]);Sprite(s.Id,x+74,213,132);
            Text(s.Name,x+22,376,27,Ink,true);Text(Bestiary.ElementName(s.Element).ToUpperInvariant(),x+23,418,11,Muted,true);
            Text(s.Description,x+23,448,14,Ink,false,236);Button($"{i+1}  Выбрать {s.Name}",x+20,519,240,()=>Engine.Start(s.Id),true);
        }
        Text("Управление: нажми 1, 2 или 3 — либо выбери мышью.",255,607,13,Muted);
    }
    void BattleScene()
    {
        var fight=Engine.Fight!;var enemy=fight.Enemy;var lead=Engine.Lead;
        Header(fight.Boss?"ИСПЫТАНИЕ ХРАНИТЕЛЯ":"ВСТРЕЧА В ТРАВЕ","Подружись со стихиями — и со своей командой");
        Box(24,85,912,347,"#dce2bd");Box(24,290,912,142,"#c9d7a9");
        // Stepped silhouettes keep the battlefield visually close to its pixel characters.
        for(int i=0;i<8;i++){Box(24+i*114,247+(i%3)*14,116,70,"#becf9d");Box(52+i*114,229+(i%3)*14,62,30,"#becf9d");}
        Box(626,232,239,13,"#9cb482");Box(650,244,190,7,"#b1c593");
        Box(110,389,266,14,"#9cb482");Box(135,403,216,6,"#b1c593");
        Sprite(enemy.SpeciesId,688,119+Math.Round(Math.Sin(Animation*2)*2),126);Sprite(lead.SpeciesId,160,267+Math.Round(Math.Sin(Animation*2+1)*2),142,true);
        Panel(58,112,349,118);Text(enemy.Species.Name,78,128,24,Ink,true,240);Text("ур. "+enemy.Level,331,137,14,Muted,true);
        Text(Bestiary.ElementName(enemy.Species.Element)+(fight.Boss?" · Хранитель":" · Дикий"),80,162,12,Muted);Hp(enemy,80,187,302,false);
        Panel(522,304,382,115);Text(lead.Species.Name,542,320,24,Ink,true);Text("ур. "+lead.Level,824,329,14,Muted,true);Hp(lead,544,365,335);Text("✦ "+lead.Energy+" / 8",814,389,11,Muted);
        Xp(lead,544,408,240);
        if(fight.Waiting)
        {
            Panel(24,455,912,163);Text(fight.Message,45,477,17,Ink,false,710);Button("E  Далее →",784,555,130,()=>Engine.Continue(),true,!BattleFxBusy);
        }
        else
        {
            Text("Что предпримет "+lead.Species.Name+"?",28,448,17,Ink,true);
            Button("1  Удар",24,483,291,()=>Engine.Attack(false),true,h:49);
            Button("2  "+lead.Species.Move+"  ["+lead.Energy+"]",334,483,292,()=>Engine.Attack(true),true,lead.Energy>0,h:49);
            Button("3  Семя дружбы  ["+Engine.Data!.Seeds+"]",645,483,291,()=>Engine.Capture(),enabled:!fight.Boss&&Engine.Data.Seeds>0&&Engine.Data.Team.Count<6,h:49);
            Button("4  Тоник  ["+Engine.Data.Tonics+"]",24,551,291,()=>Engine.Tonic(),enabled:Engine.Data.Tonics>0&&lead.Hp<lead.MaxHp,h:49);
            Button("5  Сменить спутника",334,551,292,()=>Engine.Swap(),enabled:Engine.Data.Team.Count(c=>c.Hp>0)>1,h:49);
            Button("6  Уйти",645,551,291,()=>Engine.Run(),enabled:!fight.Boss,h:49);
            Text("Лист > Вода > Искра > Лист     ·     Смена спутника и тоник занимают ход",26,617,11,Muted);
        }
    }
    void TeamScene()
    {
        Header("ТВОЯ КОМАНДА","Опыт за бои · эволюция с 8 уровня");Button("Esc  Назад",801,14,135,()=>Engine.Open(Screen.World),h:36);
        Text("Растём вместе",30,85,30,Ink,true);Text("Участвовавшие в бою получают полный XP, запасные — половину.",31,130,14,Muted);
        for(int i=0;i<Engine.Data!.Team.Count;i++)
        {
            int index=i;var c=Engine.Data.Team[i];double y=176+i*66;
            Panel(30,y,898,59,i==Engine.Data.Lead?"#e2e7c4":Cream);Sprite(c.SpeciesId,42,y+6,44);
            Text(c.Species.Name,97,y+4,18,Ink,true,190);
            Text($"Ур. {c.Level} · XP {c.Experience}/{c.NextLevel}",98,y+29,11,Muted);Xp(c,98,y+48,182);
            Hp(c,308,y+12,150);Text("✦ "+c.Energy+"/8",472,y+16,12,Muted);
            Button(i==Engine.Data.Lead?"Ведущий":"Выбрать",540,y+10,112,()=>Engine.SetLead(index),i==Engine.Data.Lead,c.Hp>0,h:36);
            Button(c.CanEvolve?"Эволюция ↑":Bestiary.Evolutions.ContainsKey(c.SpeciesId)?"С 8 уровня":"Развит",663,y+10,140,()=>Engine.Evolve(index),c.CanEvolve,c.CanEvolve,h:36);
            Button("Отпустить",814,y+10,100,()=>Release(index),enabled:Engine.Data.Team.Count>1,h:36);
        }
        Text("Эволюция усиливает спутника и сохраняет его уровень и опыт. Максимальный уровень — 30.",32,605,12,Muted,false,900);
    }    void Release(int index)
    {
        if(MessageBox.Show($"Отпустить {Engine.Data!.Team[index].Species.Name} обратно в лес? Это существо покинет команду.","Прощание со спутником",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes)Engine.Release(index);
    }
    void CollectionScene()
    {
        Header("ПОЛЕВОЙ ДНЕВНИК","Жители трёх земель и их развитые формы");Button("Esc  Назад",801,14,135,()=>Engine.Open(Screen.World),h:36);
        for(int i=0;i<Math.Min(6,Bestiary.All.Length-collectionPage*6);i++)
        {
            var s=Bestiary.All[collectionPage*6+i];bool seen=Engine.Data!.Seen.Contains(s.Id),owned=Engine.Data.Team.Any(c=>c.SpeciesId==s.Id);double x=28+i%3*312,y=92+i/3*257;
            Panel(x,y,282,235);Text("№ "+(collectionPage*6+i+1).ToString("00")+"  "+(owned?"В КОМАНДЕ":seen?"ВСТРЕЧЕН":"НЕИЗВЕСТЕН"),x+16,y+14,10,Muted,true);
            if(seen)Sprite(s.Id,x+182,y+35,76);else Text("?",x+207,y+43,48,"#b6bea0",true);
            Text(seen?s.Name:"???",x+16,y+53,23,Ink,true,177);Text(seen?Bestiary.ElementName(s.Element):"Неизвестный вид",x+17,y+91,12,Muted);
            Text(seen?s.Description:"Ищи в высокой траве и знакомься с жителями долины.",x+17,y+131,14,Ink,false,246);
            if(s.Id=="keeper"&&Engine.Data.KeeperDefeated)Text("ИСПЫТАНИЕ ПРОЙДЕНО",x+17,y+203,10,"#8a773e",true);
        }
        Text("Встречено: "+Engine.Data!.Seen.Count+" / "+Bestiary.All.Length+" · Страница "+(collectionPage+1)+" / 3",29,613,13,Muted);Button("←",710,602,64,()=>ChangeCollectionPage(-1),enabled:collectionPage>0,h:30);Button("→",790,602,64,()=>ChangeCollectionPage(1),enabled:collectionPage<2,h:30);
    }
    void HelpScene()
    {
        Header("ПАМЯТКА ПУТЕШЕСТВЕННИКА","Всё начинается с маленького шага");Button("Esc  Назад",801,14,135,()=>Engine.Open(Screen.World),h:36);
        Panel(30,92,435,499);Panel(487,92,441,499);
        Text("Как играть",53,114,26,Ink,true);
        Text("WASD / стрелки — движение\nShift — быстрый шаг\nE / Enter / Space — разговор, далее\nC — команда\nTab — коллекция\nF5 — сохранить\nM — выключить / включить звук\nEsc — помощь / вернуться\n\nВ бою: 1–6 выбирают действие.\nМеню также работают мышью.\n\nСохранение происходит после боёв,\nлечения и при выходе из игры.",53,162,17,Ink,false,390);
        Text("Дружба и стихии",510,114,26,Ink,true);
        Text("1. Выбери спутника и исследуй траву.\n\n2. Ослабь дикое существо ударом и используй семя дружбы. Чем меньше здоровья, тем выше шанс.\n\n3. Победи хранителя рощи и пройди через восточную арку к берегу.\n\n4. Зажги три маяка, открой перевал и победи двух стражей.\n\nЛист сильнее воды, вода — искры, искра — листа. Камень силён против ветра, а ветер — против листа.\n\nЦелитель в каждой земле лечит и пополняет припасы. Эволюция: C, с 8 уровня.",510,162,16,Ink,false,390);
        Button("Настройки звука",53,539,385,ShowAudio,h:34);
        Text("Если закрыть игру во время боя, путешествие продолжится с момента перед этой встречей.",31,611,12,Muted,false,900);
    }
    void VictoryScene()
    {
        Box(0,0,960,640,"#d9dfb3");Panel(100,54,760,532);Sprite(Engine.Map.BossId,383,84,192);
        Text("ХРАНИТЕЛЬ ПРИЗНАЛ ВАШУ КОМАНДУ",251,285,15,"#8b7950",true);
        Text("Испытание пройдено!",226,326,40,Ink,true,700);
        Text(Engine.Data!.SummitDefeated?"Все три испытания пройдены!\nПродолжай собирать существ и их развитые формы.":Engine.Data.TideDefeated?"Зеркальный берег пройден.\nАрка на северо-востоке ведёт к Лунному перевалу.":"Роща пройдена.\nВосточная арка ведёт к Зеркальному берегу.",200,391,17,Ink,false,620);
        Text($"Побед и приручений: {Engine.Data!.Wins}   ·   Встретил видов: {Engine.Data.Seen.Count}/{Bestiary.All.Length}",248,460,14,Muted);
        Button("Продолжить исследование  →",274,514,410,()=>Engine.Open(Screen.World),true,h:46);
    }
}
