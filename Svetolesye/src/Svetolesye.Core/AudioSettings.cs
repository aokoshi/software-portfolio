using System.Text.Json;
namespace Svetolesye.Core;
public sealed class AudioSettings
{
    public int Music {get;set;}=30;
    public int Ambience {get;set;}=25;
    public int Effects {get;set;}=45;
    public bool Muted {get;set;}
    public void Clamp(){Music=Math.Clamp(Music,0,100);Ambience=Math.Clamp(Ambience,0,100);Effects=Math.Clamp(Effects,0,100);}
    public static AudioSettings Load(string path)
    {
        try{var s=JsonSerializer.Deserialize<AudioSettings>(File.ReadAllText(path))??new();s.Clamp();return s;}
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException){return new();}
    }
    public void Save(string path){Clamp();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));File.Move(path+".tmp",path,true);}
}
