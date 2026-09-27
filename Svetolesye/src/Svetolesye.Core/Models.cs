namespace Svetolesye.Core;

public enum Element { Leaf, Ember, Water, Stone, Air }
public enum Region { Valley, Coast, Summit }
public record Species(string Id,string Name,Element Element,string Move,string Description,int Health,int Power,int Guard,string Color);
public static class Bestiary
{
    public static readonly Species[] All = [
        new("moss","Мшун",Element.Leaf,"Листопад","Тихий житель опушек. Носит на голове молодой папоротник.",24,7,5,"#73ad54"),
        new("spark","Искрик",Element.Ember,"Огонёк","Собирает солнечное тепло в кисточке хвоста.",20,9,4,"#e8a047"),
        new("drop","Каплик",Element.Water,"Прилив","Хранит в раковине чистую родниковую воду.",25,7,6,"#62abb1"),
        new("pebble","Крошень",Element.Stone,"Камнепад","Этот упрямый малыш похож на ожившую гальку.",28,7,8,"#b8a28a"),
        new("moth","Лунница",Element.Air,"Порыв","Оставляет в воздухе след из серебристой пыльцы.",21,8,4,"#b69cce"),
        new("keeper","Ветрокрон",Element.Leaf,"Корни света","Древний хранитель рощи. Открывает дорогу к берегу.",36,9,7,"#d5bb68"),
        new("verdant","Мохорог",Element.Leaf,"Лесной шквал","Эволюция Мшуна. Его рога укрыты густой листвой.",42,11,9,"#4e9767"),
        new("flare","Жаролис",Element.Ember,"Солнечный вихрь","Эволюция Искрика. Хвост пылает золотым огнём.",36,14,7,"#df7952"),
        new("tide","Акварин",Element.Water,"Водоворот","Эволюция Каплика. Умеет направлять морские течения.",43,11,10,"#4c9ec1"),
        new("boulder","Гранитыш",Element.Stone,"Обвал","Эволюция Крошня. Носит на плечах россыпь кристаллов.",48,11,13,"#a496b3"),
        new("luna","Лунокрыл",Element.Air,"Лунный ветер","Эволюция Лунницы. Крылья мерцают в темноте.",37,13,7,"#aa8ad5"),
        new("tidekeeper","Приливень",Element.Water,"Зеркальная волна","Хранитель трёх маяков на Зеркальном берегу.",46,13,10,"#6bbec6"),
        new("astral","Астрарон",Element.Air,"Звёздный поток","Хранитель перевала, связующий три земли.",52,16,12,"#c2a0e8")
    ];
    public static readonly IReadOnlyDictionary<string,string> Evolutions=new Dictionary<string,string>{["moss"]="verdant",["spark"]="flare",["drop"]="tide",["pebble"]="boulder",["moth"]="luna"};
    public const int EvolutionLevel=8;
    public static Species Get(string id)=>All.FirstOrDefault(s=>s.Id==id)??throw new InvalidDataException("Неизвестный вид существа.");
    public static string Family(string id)=>Evolutions.FirstOrDefault(p=>p.Value==id).Key??id;
    public static string ElementName(Element e)=>e switch {Element.Leaf=>"Лист",Element.Ember=>"Искра",Element.Water=>"Вода",Element.Stone=>"Камень",_=>"Ветер"};
    public static double Effect(Element attack,Element defence)
    {
        if((attack,defence) is (Element.Leaf,Element.Water) or (Element.Water,Element.Ember) or (Element.Ember,Element.Leaf) or (Element.Stone,Element.Air) or (Element.Air,Element.Leaf))return 1.5;
        if((attack,defence) is (Element.Water,Element.Leaf) or (Element.Ember,Element.Water) or (Element.Leaf,Element.Ember) or (Element.Air,Element.Stone))return .7;
        return 1;
    }
}
public sealed class Creature
{
    public const int MaxLevel=30;
    public string SpeciesId {get;set;}="moss";
    public int Level {get;set;}=5;
    public int Experience {get;set;}
    public int Hp {get;set;}
    public int Energy {get;set;}=8;
    [System.Text.Json.Serialization.JsonIgnore] public Species Species=>Bestiary.Get(SpeciesId);
    [System.Text.Json.Serialization.JsonIgnore] public int MaxHp=>Species.Health+Level*3;
    [System.Text.Json.Serialization.JsonIgnore] public int NextLevel=>12+Level*3;
    [System.Text.Json.Serialization.JsonIgnore] public bool CanEvolve=>Level>=Bestiary.EvolutionLevel&&Bestiary.Evolutions.ContainsKey(SpeciesId);
    public static Creature Create(string id,int level){var c=new Creature{SpeciesId=id,Level=level};c.Hp=c.MaxHp;return c;}
    public void Heal(){Hp=MaxHp;Energy=8;}
    public int GainExperience(int amount)
    {
        if(amount<0||amount>10000)throw new ArgumentOutOfRangeException(nameof(amount));
        int before=Level;if(Level>=MaxLevel){Experience=0;return 0;}Experience+=amount;
        while(Level<MaxLevel&&Experience>=NextLevel){Experience-=NextLevel;Level++;if(Hp>0)Hp=Math.Min(MaxHp,Hp+6);}
        if(Level==MaxLevel)Experience=0;return Level-before;
    }
    public string Evolve()
    {
        if(!CanEvolve)throw new InvalidOperationException("Эволюция пока недоступна.");
        string previous=SpeciesId;int oldMax=MaxHp;SpeciesId=Bestiary.Evolutions[SpeciesId];if(Hp>0)Hp+=MaxHp-oldMax;return previous;
    }
}
public sealed class SaveData
{
    public int Version {get;set;}=2;
    public Region Region {get;set;}=Region.Valley;
    public int X {get;set;}=7;
    public int Y {get;set;}=15;
    public List<Creature> Team {get;set;}=[];
    public int Lead {get;set;}
    public int Seeds {get;set;}=8;
    public int Tonics {get;set;}=3;
    public HashSet<string> Seen {get;set;}=[];
    public bool KeeperDefeated {get;set;}
    public bool TideDefeated {get;set;}
    public bool SummitDefeated {get;set;}
    public HashSet<string> Beacons {get;set;}=[];
    public HashSet<string> Trials {get;set;}=[];
    public int Wins {get;set;}
    public int Steps {get;set;}
}
public record Resident(int X,int Y,string Kind,string Name);
public record Portal(int X,int Y,Region Destination,int SpawnX,int SpawnY,string Label);
public sealed class World
{
    public const int Width=26,Height=20;
    public Region Region {get;}
    public string Name=>Region switch {Region.Coast=>"Зеркальный берег",Region.Summit=>"Лунный перевал",_=>"Долина тихого родника"};
    public (int X,int Y) Camp=>Region switch {Region.Coast=>(3,15),Region.Summit=>(3,16),_=>(7,15)};
    public char[,] Tiles {get;}=new char[Width,Height];
    public Resident[] Residents {get;}
    public Portal[] Portals {get;}
    public string BossId=>Region switch {Region.Coast=>"tidekeeper",Region.Summit=>"astral",_=>"keeper"};
    public World(Region region=Region.Valley)
    {
        Region=region;
        for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)Tiles[x,y]=(x==0||y==0||x==Width-1||y==Height-1)?'#':'.';
        if(region==Region.Valley)
        {
            Fill(2,2,9,7,'g');Fill(12,7,21,11,'g');Fill(21,13,24,17,'g');
            Fill(22,1,24,10,'~');Fill(23,11,24,12,'~');Fill(1,8,3,10,'~');
            Fill(3,14,21,15,'p');Fill(16,4,17,15,'p');Fill(6,7,7,15,'p');Fill(7,7,17,7,'p');Fill(17,4,20,5,'p');Fill(18,3,20,3,'p');
            Fill(3,11,6,12,'h');Fill(9,11,11,12,'h');
            foreach(var (x,y) in new[]{(10,3),(11,3),(10,4),(11,4),(2,17),(3,17),(4,17),(11,17),(12,17),(13,17),(20,17),(21,2),(13,2),(14,2),(2,6),(10,8),(11,8)})Tiles[x,y]='#';
            Residents=[new(5,13,"healer","Лада"),new(9,15,"guide","Тим"),new(19,4,"keeper","Хранитель"),new(13,13,"sign","Указатель")];
            Portals=[new(24,15,Region.Coast,2,15,"К берегу →")];Fill(21,15,24,15,'p');
        }
        else if(region==Region.Coast)
        {
            Fill(10,1,13,18,'~');Fill(20,10,24,17,'~');Fill(1,1,3,7,'~');
            Fill(4,6,8,10,'g');Fill(15,9,18,14,'g');Fill(16,1,22,2,'g');
            Fill(1,15,19,15,'p');Fill(5,4,6,15,'p');Fill(5,5,24,5,'p');Fill(18,3,19,15,'p');Fill(18,4,24,4,'p');Fill(18,7,22,7,'p');
            Fill(10,5,13,5,'b');Fill(10,15,13,15,'b');Fill(3,12,5,13,'h');
            Residents=[new(4,14,"healer","Лада"),new(7,15,"guide","Мира"),new(19,3,"keeper","Приливень"),new(8,5,"sign","Маяки"),new(5,4,"beacon-west","Западный маяк"),new(17,15,"beacon-south","Южный маяк"),new(21,7,"beacon-east","Восточный маяк")];
            Portals=[new(1,15,Region.Valley,23,15,"← Долина"),new(24,4,Region.Summit,2,16,"К перевалу →")];
        }
        else
        {
            Fill(10,2,10,17,'#');Fill(1,10,24,10,'#');Fill(20,2,24,6,'~');Fill(1,1,2,6,'~');
            Fill(3,2,8,7,'g');Fill(15,12,22,16,'g');Fill(12,2,16,5,'g');
            Fill(1,16,18,16,'p');Fill(5,4,5,16,'p');Fill(5,5,19,5,'p');Fill(17,4,17,16,'p');Fill(10,15,10,16,'b');Fill(17,3,20,4,'p');
            Residents=[new(3,15,"healer","Лада"),new(6,16,"guide","Северин"),new(19,3,"keeper","Астрарон"),new(6,9,"sign","Камень памяти"),new(7,4,"trial-air","Страж ветра"),new(20,14,"trial-stone","Страж камня")];
            Portals=[new(1,16,Region.Coast,23,4,"← Берег")];
        }
        foreach(var p in Portals)Tiles[p.X,p.Y]='p';
    }
    void Fill(int x1,int y1,int x2,int y2,char c){for(int y=y1;y<=y2;y++)for(int x=x1;x<=x2;x++)Tiles[x,y]=c;}
    public bool Walkable(int x,int y)=>x>=0&&y>=0&&x<Width&&y<Height&&Tiles[x,y] is not ('#' or '~' or 'h')&&!Residents.Any(n=>n.X==x&&n.Y==y);
    public bool Grass(int x,int y)=>Tiles[x,y]=='g';
}
public enum Screen { Title, Starters, World, Dialogue, Battle, Team, Collection, Help, Victory, ConfirmNew, Evolution }
public enum BattleOutcome { None, Win, Capture, Escape, Loss }
public record EvolutionNotice(string Before,string After,int Level);
public record GameEffect(string Kind,Element Element=Element.Leaf,int Value=0,bool OnEnemy=true);
public sealed class Battle
{
    public required Creature Enemy {get;init;}
    public bool Boss {get;init;}
    public string EncounterId {get;init;}="";
    public bool Waiting {get;set;}
    public string Message {get;set;}="";
    public BattleOutcome Outcome {get;set;}
    public HashSet<int> Participants {get;}=[];
}
