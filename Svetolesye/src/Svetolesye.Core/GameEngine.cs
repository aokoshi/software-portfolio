namespace Svetolesye.Core;

public sealed class GameEngine(Random? random=null)
{
    readonly Random rng=random??new Random();
    public World Map {get;private set;}=new();
    public SaveData? Data {get;private set;}
    public Screen Screen {get;private set;}=Screen.Title;
    public Battle? Fight {get;private set;}
    public string Dialogue {get;private set;}="";
    public string Speaker {get;private set;}="";
    public string Toast {get;set;}="";
    public EvolutionNotice? Evolution {get;private set;}
    public (int X,int Y) Facing {get;private set;}=(0,1);
    public int GrassSteps {get;private set;}
    public event Action<SaveData>? SaveRequested;
    public event Action<GameEffect>? Effect;
    SaveData? checkpoint;
    readonly Queue<(string Who,string Text)> messages=[];
    bool victoryAfterDialogue;
    public Creature Lead=>Data!.Team[Data.Lead];
    public int DistinctSpecies=>Data?.Team.Select(c=>Bestiary.Family(c.SpeciesId)).Distinct().Count()??0;
    public void StartChoosing()=>Screen=Screen.Starters;
    public void ConfirmNew()=>Screen=Screen.ConfirmNew;
    public void Title()=>Screen=Screen.Title;
    void Emit(string kind,Element element=Element.Leaf,int value=0,bool enemy=true)=>Effect?.Invoke(new(kind,element,value,enemy));
    public void Start(string starter)
    {
        if(!new[]{"moss","spark","drop"}.Contains(starter))throw new ArgumentException("Выберите одного из трёх спутников.");
        Data=new SaveData{Team=[Creature.Create(starter,5)],Seen=[starter]};Map=new();Facing=(0,1);GrassSteps=0;Fight=null;messages.Clear();victoryAfterDialogue=false;Screen=Screen.World;Persist();
        Say("Тим, исследователь",$"Твой спутник — {Lead.Species.Name}! Найди в траве ещё два разных вида, а потом приходи к хранителю на северо-востоке.\n\nЛада у красного домика вылечит команду. За рощей вас ждут ещё две земли!");
    }
    public void Load(SaveData data){data=SaveStore.Clone(data);SaveStore.Migrate(data);SaveStore.Validate(data);Data=data;Map=new(data.Region);Fight=null;checkpoint=null;messages.Clear();victoryAfterDialogue=false;GrassSteps=0;Screen=Screen.World;Facing=(0,1);}
    public SaveData? SafeSnapshot()=>Data==null?null:SaveStore.Clone(Fight!=null&&checkpoint!=null?checkpoint:Data);
    public void Persist(){var d=SafeSnapshot();if(d!=null)SaveRequested?.Invoke(d);}
    public void Say(string who,string text){Speaker=who;Dialogue=text;Screen=Screen.Dialogue;}
    public void Continue()
    {
        if(Screen==Screen.Evolution){Screen=Screen.Team;return;}
        if(Screen==Screen.Dialogue)
        {
            if(messages.TryDequeue(out var next)){Say(next.Who,next.Text);return;}
            Screen=victoryAfterDialogue?Screen.Victory:Screen.World;victoryAfterDialogue=false;return;
        }
        if(Screen!=Screen.Battle||Fight==null||!Fight.Waiting)return;
        if(Fight.Outcome!=BattleOutcome.None){FinishBattle();return;}
        Fight.Waiting=false;Fight.Message="Что предпримет "+Lead.Species.Name+"?";
    }
    public void Open(Screen screen)
    {
        if(Data==null||Screen is not (Screen.World or Screen.Team or Screen.Collection or Screen.Help or Screen.Victory))return;
        if(screen is Screen.Team or Screen.Collection or Screen.Help or Screen.World)Screen=screen;
    }
    public bool Move(int dx,int dy)
    {
        if(Screen!=Screen.World||Data==null||Math.Abs(dx)+Math.Abs(dy)!=1)return false;Facing=(dx,dy);
        int x=Data.X+dx,y=Data.Y+dy;if(!Map.Walkable(x,y))return false;
        var portal=Map.Portals.FirstOrDefault(p=>p.X==x&&p.Y==y);
        if(portal!=null)
        {
            if(portal.Destination==Region.Coast&&!Data.KeeperDefeated){Say("Тропа закрыта","Сначала пройди испытание Ветрокрона в роще.");return false;}
            if(portal.Destination==Region.Summit&&!Data.TideDefeated){Say("Перевал закрыт","Зажги три маяка и победи Приливня на берегу.");return false;}
            Data.Region=portal.Destination;Map=new(Data.Region);Data.X=portal.SpawnX;Data.Y=portal.SpawnY;Data.Steps++;GrassSteps=0;Persist();Emit("portal");Toast=Map.Name;return true;
        }
        Data.X=x;Data.Y=y;Data.Steps++;
        if(Map.Grass(x,y))
        {
            GrassSteps++;
            if(GrassSteps>=4&&(rng.NextDouble()<.18||GrassSteps>=9))
            {
                GrassSteps=0;
                string[] pool=Data.Region switch {Region.Coast=>["drop","pebble","tide","spark","boulder"],Region.Summit=>["luna","boulder","flare","verdant"],_=>x<11?["moss","moth","pebble"]:["spark","drop","pebble","moth"]};
                int min=Data.Region==Region.Valley?3:Data.Region==Region.Coast?8:11;
                BeginBattle(Creature.Create(pool[rng.Next(pool.Length)],rng.Next(min,min+3)),false);
            }
        }
        return true;
    }
    public void Interact()
    {
        if(Screen!=Screen.World||Data==null)return;
        var npc=Map.Residents.FirstOrDefault(n=>n.X==Data.X+Facing.X&&n.Y==Data.Y+Facing.Y)??Map.Residents.FirstOrDefault(n=>Math.Abs(n.X-Data.X)+Math.Abs(n.Y-Data.Y)==1);
        if(npc==null){Toast="Подойди к человеку или указателю и нажми E.";return;}
        if(npc.Kind.StartsWith("beacon-"))
        {
            bool added=Data.Beacons.Add(npc.Kind);if(added){Persist();Emit("beacon");}
            Say(npc.Name,added?$"Маяк зажёгся! Огней на берегу: {Data.Beacons.Count}/3.\n\nКогда зажгутся все три, Приливень у северного берега примет испытание.":"Маяк уже горит. Его свет указывает путь через воду.");return;
        }
        if(npc.Kind.StartsWith("trial-"))
        {
            if(Data.Trials.Contains(npc.Kind)){Say(npc.Name,"Твою команду здесь помнят. Двигайся к вершине!");return;}
            BeginBattle(Creature.Create(npc.Kind=="trial-air"?"luna":"boulder",npc.Kind=="trial-air"?13:14),true,npc.Kind);return;
        }
        switch(npc.Kind)
        {
            case "healer":HealAtVillage();Say("Лада, хранительница родников","Команда здорова, силы стихии восстановлены. Припасы пополнены: не меньше 8 семян и 3 тоников.\n\nНа 8-м уровне спутник готов к эволюции. Открой команду клавишей C и выбери «Эволюция».");break;
            case "guide":Say(npc.Name,GuideText());break;
            case "sign":Say(npc.Name,Data.Region switch {Region.Coast=>"Три маяка: на западе, у южного моста и на востоке.\n\nПриливень ждёт на севере. После испытания откроется восточная тропа на Лунный перевал.",Region.Summit=>"Страж ветра — на северо-западе. Страж камня — на юго-востоке.\n\nПройди оба испытания и найди Астрарона на вершине. Лада разбила лагерь на юго-западе.",_=>"← Западная опушка · → Восточный луг\n↑ Ветрокрон · → на краю восточной тропы: Зеркальный берег\n\nC — команда · Tab — коллекция · M — звук · F5 — сохранить"});break;
            case "keeper":
                bool defeated=Data.Region switch{Region.Coast=>Data.TideDefeated,Region.Summit=>Data.SummitDefeated,_=>Data.KeeperDefeated};
                if(defeated){Say(Bestiary.Get(Map.BossId).Name,"Испытание пройдено. Пусть свет ведёт вашу команду дальше.");break;}
                if(Data.Region==Region.Valley&&DistinctSpecies<3){Say("Голос рощи","Сначала подружись с тремя разными видами. Эволюции одного вида считаются одной семьёй.");break;}
                if(Data.Region==Region.Coast&&Data.Beacons.Count<3){Say("Голос прилива",$"Зажги все три маяка. Сейчас горят: {Data.Beacons.Count}/3.");break;}
                if(Data.Region==Region.Summit&&Data.Trials.Count<2){Say("Голос вершины","Пройди испытания обоих стражей: ветра и камня.");break;}
                BeginBattle(Creature.Create(Map.BossId,Data.Region==Region.Valley?8:Data.Region==Region.Coast?12:16),true);break;
        }
    }
    string GuideText()=>Data!.Region switch
    {
        Region.Coast=>$"Это Зеркальный берег. Зажжено маяков: {Data.Beacons.Count}/3.\n\nИщи их на трёх островках. Здесь встречаются существа 8–10 уровня и развитые формы. Подготовь свою команду к Приливню!",
        Region.Summit=>$"Добро пожаловать на Лунный перевал. Испытаний стражей пройдено: {Data.Trials.Count}/2.\n\nДикие существа здесь 11–13 уровня. Помни об эволюциях и лечись у Лады перед вершиной.",
        _=>Data.KeeperDefeated?"Ветрокрон открыл тропу! Иди на восток по нижней дороге до светящейся арки — она приведёт к Зеркальному берегу.":$"В команде {DistinctSpecies} из 3 нужных видов. Найди их в траве и посети Ветрокрона.\n\nЗа победы и приручение команда получает XP. На 8-м уровне можно эволюционировать!"
    };
    public void HealAtVillage(){if(Data==null)return;foreach(var c in Data.Team)c.Heal();Data.Seeds=Math.Max(8,Data.Seeds);Data.Tonics=Math.Max(3,Data.Tonics);Persist();Emit("heal");}
    public void SetLead(int index)
    {
        if(Screen!=Screen.Team||Data==null||index<0||index>=Data.Team.Count)return;
        if(Data.Team[index].Hp==0){Toast="Сначала вылечи этого спутника у Лады.";return;}Data.Lead=index;Toast=Lead.Species.Name+" теперь ведёт команду.";Persist();
    }
    public void Evolve(int index)
    {
        if(Screen!=Screen.Team||Data==null||index<0||index>=Data.Team.Count)return;
        var c=Data.Team[index];if(!c.CanEvolve){Toast="Эволюция доступна для начальной формы с 8-го уровня.";return;}
        string old=c.Evolve();Data.Seen.Add(old);Data.Seen.Add(c.SpeciesId);Evolution=new(old,c.SpeciesId,c.Level);Persist();Screen=Screen.Evolution;Emit("evolution",c.Species.Element);
    }
    public void Release(int index)
    {
        if(Screen!=Screen.Team||Data==null||Data.Team.Count<=1||index<0||index>=Data.Team.Count)return;
        if(!Data.Team.Where((_,i)=>i!=index).Any(c=>c.Hp>0)){Toast="В команде должен остаться здоровый спутник.";return;}
        string name=Data.Team[index].Species.Name;Data.Team.RemoveAt(index);
        if(Data.Lead==index)Data.Lead=Data.Team.FindIndex(c=>c.Hp>0);else if(Data.Lead>index)Data.Lead--;
        Toast=name+" вернулся в лес. Спасибо за путешествие!";Persist();
    }
    public void BeginBattle(Creature enemy,bool boss,string encounterId="")
    {
        if(Data==null||Fight!=null)return;
        checkpoint=SaveStore.Clone(Data);SaveRequested?.Invoke(checkpoint);
        if(Lead.Hp==0)Data.Lead=Data.Team.FindIndex(c=>c.Hp>0);
        Data.Seen.Add(enemy.SpeciesId);Fight=new(){Enemy=enemy,Boss=boss,EncounterId=encounterId.Length>0?encounterId:boss?Map.BossId:"",Waiting=true,Message=boss?enemy.Species.Name+" готов испытать вашу команду!":"Из травы появляется "+enemy.Species.Name+"!"};
        Fight.Participants.Add(Data.Lead);Screen=Screen.Battle;Emit("encounter");
    }
    int Damage(Creature attacker,Creature defender,bool special)
    {
        double factor=special?Bestiary.Effect(attacker.Species.Element,defender.Species.Element):1;
        int raw=attacker.Species.Power+attacker.Level-(defender.Species.Guard+defender.Level)/3;
        return Math.Max(2,(int)Math.Round((raw+rng.Next(-1,2))*(special?1.2:0.85)*factor));
    }
    bool CanAct=>Screen==Screen.Battle&&Fight!=null&&!Fight.Waiting&&Fight.Outcome==BattleOutcome.None;
    public void Attack(bool special)
    {
        if(!CanAct)return;
        if(special&&Lead.Energy==0){Toast="Силы стихии закончились. Используй обычный удар.";return;}
        if(special)Lead.Energy--;int damage=Damage(Lead,Fight!.Enemy,special);Fight.Enemy.Hp=Math.Max(0,Fight.Enemy.Hp-damage);Emit(special?"special":"hit",Lead.Species.Element,damage);
        string text=$"{Lead.Species.Name}: {(special?Lead.Species.Move:"Удар")}! Урон: {damage}.";
        if(special){double effect=Bestiary.Effect(Lead.Species.Element,Fight.Enemy.Species.Element);text+=effect>1?" Очень эффективно!":effect<1?" Не очень эффективно…":"";}
        if(Fight.Enemy.Hp==0)EndTurn(text+"\nПротивник побеждён!",BattleOutcome.Win);else EnemyTurn(text);
    }
    void EnemyTurn(string text)
    {
        var battle=Fight!;int damage=Damage(battle.Enemy,Lead,rng.NextDouble()<.35);Lead.Hp=Math.Max(0,Lead.Hp-damage);Emit("reply",battle.Enemy.Species.Element,damage,false);
        text+=$"\n{battle.Enemy.Species.Name} отвечает. Урон: {damage}.";
        if(Lead.Hp==0)
        {
            text+="\n"+Lead.Species.Name+" устал и не может продолжать.";
            int next=Data!.Team.FindIndex(c=>c.Hp>0);
            if(next<0){EndTurn(text+" Команда возвращается к роднику.",BattleOutcome.Loss);return;}
            Data.Lead=next;battle.Participants.Add(next);text+=" На помощь приходит "+Lead.Species.Name+"!";
        }
        EndTurn(text);
    }
    void EndTurn(string text,BattleOutcome outcome=BattleOutcome.None){Fight!.Message=text;Fight.Waiting=true;Fight.Outcome=outcome;}
    public void Capture()
    {
        if(!CanAct)return;
        if(Fight!.Boss){Toast="Хранитель не приручается. Пройди его испытание.";return;}
        if(Data!.Team.Count>=6){Toast="В команде уже шесть спутников. Продолжи бой или уйди.";return;}
        if(Data.Seeds==0){Toast="Нет семян дружбы. Лада пополнит припасы.";return;}
        Data.Seeds--;double chance=.25+.65*(1-(double)Fight.Enemy.Hp/Fight.Enemy.MaxHp);
        if(rng.NextDouble()<chance){Emit("capture");EndTurn(Fight.Enemy.Species.Name+" принял семя дружбы и присоединился к команде!",BattleOutcome.Capture);}
        else{Emit("miss");EnemyTurn("Семя вспыхнуло… но существо пока не доверяет тебе. Ослабь его ещё немного.");}
    }
    public void Tonic()
    {
        if(!CanAct)return;if(Data!.Tonics==0){Toast="Тоники закончились. Припасы есть у Лады.";return;}if(Lead.Hp==Lead.MaxHp){Toast="Здоровье уже полное.";return;}
        Data.Tonics--;int healed=Math.Min(45,Lead.MaxHp-Lead.Hp);Lead.Hp+=healed;Emit("heal",value:healed,enemy:false);EnemyTurn($"Тоник восстановил {healed} здоровья.");
    }
    public void Swap()
    {
        if(!CanAct)return;
        int next=Enumerable.Range(1,Data!.Team.Count-1).Select(i=>(Data.Lead+i)%Data.Team.Count).FirstOrDefault(i=>Data.Team[i].Hp>0,-1);
        if(next<0){Toast="Других здоровых спутников нет.";return;}
        Data.Lead=next;Fight!.Participants.Add(next);EnemyTurn(Lead.Species.Name+" выходит вперёд!");
    }
    public void Run(){if(!CanAct)return;if(Fight!.Boss){Toast="Из испытания можно вернуться только после победы или поражения.";return;}EndTurn("Вы спокойно отошли от дикого существа.",BattleOutcome.Escape);}
    void FinishBattle()
    {
        var fight=Fight!;bool won=fight.Outcome is BattleOutcome.Win or BattleOutcome.Capture;
        if(won)
        {
            Data!.Wins++;int reward=fight.Boss?40+fight.Enemy.Level*5:12+fight.Enemy.Level*3;
            var report=new List<string>();bool levelled=false;
            // Award XP before adding the caught creature: newly caught companions do not earn their own reward.
            for(int i=0;i<Data.Team.Count;i++)
            {
                var c=Data.Team[i];int xp=fight.Participants.Contains(i)?reward:Math.Max(1,reward/2);if(c.Hp==0)xp=Math.Max(1,xp/2);
                bool wasMax=c.Level==Creature.MaxLevel;int gained=c.GainExperience(xp);levelled|=gained>0;
                report.Add($"{c.Species.Name}: "+(wasMax?"максимальный уровень":$"+{xp} XP")+(gained>0?$" → ур. {c.Level}":"")+(c.CanEvolve?" · готов к эволюции":""));
            }
            Emit(levelled?"level":"victory");
            foreach(var page in report.Chunk(3))messages.Enqueue(("Опыт команды",string.Join("\n",page)+"\n\nЭволюция — в меню команды (C), с 8 уровня."));
            if(fight.Outcome==BattleOutcome.Capture){Data.Team.Add(fight.Enemy);messages.Enqueue(("Новый спутник",fight.Enemy.Species.Name+" теперь в команде!"));}
            if(fight.Boss)
            {
                switch(fight.EncounterId)
                {
                    case "keeper":Data.KeeperDefeated=true;messages.Enqueue(("Голос рощи","Ветрокрон открывает восточную тропу. Иди к светящейся арке в конце нижней дороги — впереди Зеркальный берег!"));victoryAfterDialogue=true;break;
                    case "tidekeeper":Data.TideDefeated=true;messages.Enqueue(("Голос прилива","Три маяка сияют в унисон. Восточная арка на севере берега ведёт к Лунному перевалу!"));victoryAfterDialogue=true;break;
                    case "astral":Data.SummitDefeated=true;messages.Enqueue(("Свет трёх земель","Астрарон узнаёт вашу команду. Долина, берег и перевал снова связаны. Большое приключение завершено!"));victoryAfterDialogue=true;break;
                    default:Data.Trials.Add(fight.EncounterId);messages.Enqueue(("Испытание стража",$"Испытаний на перевале пройдено: {Data.Trials.Count}/2.\n\nПройди оба и отправляйся к Астрарону на вершине."));break;
                }
            }
        }
        if(fight.Outcome==BattleOutcome.Loss)
        {
            var camp=Map.Camp;Data!.X=camp.X;Data.Y=camp.Y;foreach(var c in Data.Team)c.Heal();Data.Seeds=Math.Max(8,Data.Seeds);Data.Tonics=Math.Max(3,Data.Tonics);
            messages.Enqueue(("У родника","Лада вылечила команду и пополнила припасы. Прогресс испытаний сохранён. Попробуй другую тактику!"));Emit("lose");
        }
        Fight=null;checkpoint=null;Screen=Screen.World;Persist();
        if(messages.TryDequeue(out var first))Say(first.Who,first.Text);
    }
}
