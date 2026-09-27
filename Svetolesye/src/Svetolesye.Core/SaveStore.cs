using System.Text.Json;
namespace Svetolesye.Core;

public sealed class SaveStore(string file)
{
    static readonly JsonSerializerOptions Options=new(){WriteIndented=true};
    public string FilePath {get;}=Path.GetFullPath(file);
    public bool Exists=>File.Exists(FilePath)||File.Exists(FilePath+".bak");
    public static SaveData Clone(SaveData data)=>JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(data))!;
    public static void Validate(SaveData d)
    {
        if(d.Version!=2||d.Team==null||d.Team.Count is <1 or >6||d.Lead<0||d.Lead>=d.Team.Count||!Enum.IsDefined(d.Region)||!new World(d.Region).Walkable(d.X,d.Y)||d.Seeds is <0 or >999||d.Tonics is <0 or >999||d.Wins<0||d.Steps<0||d.Seen==null)throw new InvalidDataException("Сохранение имеет неверный формат.");
        foreach(var c in d.Team){if(c==null||c.Level is <1 or >Creature.MaxLevel||c.Experience<0||c.Experience>=c.NextLevel||c.Hp<0||c.Hp>c.MaxHp||c.Energy is <0 or >8)throw new InvalidDataException("Неверные характеристики существа.");_ = c.Species;}
                if(d.Beacons==null||d.Trials==null||d.Beacons.Any(id=>!new[]{"beacon-west","beacon-south","beacon-east"}.Contains(id))||d.Trials.Any(id=>!new[]{"trial-air","trial-stone"}.Contains(id)))throw new InvalidDataException("Некорректный прогресс испытаний.");
        if((d.Region!=Region.Valley&&!d.KeeperDefeated)||(d.Region==Region.Summit&&!d.TideDefeated)||(d.TideDefeated&&!d.KeeperDefeated)||(d.SummitDefeated&&!d.TideDefeated))throw new InvalidDataException("Нарушен порядок открытия областей.");
        foreach(string id in d.Seen)_=Bestiary.Get(id);
        if(d.Team.All(c=>c.Hp==0))throw new InvalidDataException("В команде нет здоровых существ.");
    }
    public static void Migrate(SaveData d)
    {
        if(d.Version!=1)return;
        d.Version=2;d.Region=Region.Valley;d.Beacons=[];d.Trials=[];d.TideDefeated=false;d.SummitDefeated=false;
    }
    public void Save(SaveData data)
    {
        Validate(data);Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temporary=FilePath+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(data,Options));
        // Preserve a known-good previous save. A corrupt main file never overwrites the recovery copy.
        if(File.Exists(FilePath)) {try{Read(FilePath);
            using(var original=JsonDocument.Parse(File.ReadAllText(FilePath)))
                if(original.RootElement.TryGetProperty("Version",out var v)&&v.GetInt32()==1&&!File.Exists(FilePath+".v1.bak"))File.Copy(FilePath,FilePath+".v1.bak");
            File.Copy(FilePath,FilePath+".bak",true);}catch(JsonException){}catch(InvalidDataException){}}
        File.Move(temporary,FilePath,true);
    }
    static SaveData Read(string path)
    {
        if(new FileInfo(path).Length>1_000_000)throw new InvalidDataException("Файл сохранения слишком большой.");
        var data=JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path))??throw new InvalidDataException("Сохранение пусто.");Migrate(data);Validate(data);return data;
    }
    public (SaveData Data,bool Recovered) Load()
    {
        try{return(Read(FilePath),false);}
        catch(Exception e) when(e is IOException or JsonException or InvalidDataException or ArgumentException)
        {if(File.Exists(FilePath+".bak"))return(Read(FilePath+".bak"),true);throw new InvalidDataException("Не удалось прочитать сохранение. Файлы оставлены без изменений.",e);}
    }
}
