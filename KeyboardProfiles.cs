using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public sealed class KeyboardProfile
{
    public string Id, Name, Family, Support;
    public bool Experimental;
    public KeyboardProfile(string id, string name, string family, string support, bool experimental) {
        Id=id; Name=name; Family=family; Support=support; Experimental=experimental;
    }
    public override string ToString() { return Name; }
}

public static class KeyboardProfiles
{
    public static readonly KeyboardProfile[] All = {
        new KeyboardProfile("f75", "AULA F75", "13", "Tested on this keyboard over 2.4 GHz", false),
        new KeyboardProfile("f99pro", "AULA F99 Pro", "13", "Documented protocol; needs hardware validation", true),
        new KeyboardProfile("f75max", "AULA F75 Max", "20", "Documented protocol; needs Windows hardware validation", true),
        new KeyboardProfile("s75pro", "AULA S75 Pro", "20", "Documented protocol; needs hardware validation", true),
        new KeyboardProfile("f87", "AULA F87", "13", "Compatible receiver mode; battery support unverified", true),
        new KeyboardProfile("f87pro", "AULA F87 Pro", "13", "Compatible receiver mode; battery support unverified", true),
        new KeyboardProfile("f65", "AULA F65", "compatible", "Compatible receiver mode; hardware revisions unverified", true),
        new KeyboardProfile("f75pro", "AULA F75 Pro", "compatible", "Compatible receiver mode; battery support unverified", true),
        new KeyboardProfile("f99", "AULA F99", "compatible", "Compatible receiver mode; battery support unverified", true),
        new KeyboardProfile("f108", "AULA F108", "compatible", "Compatible receiver mode; battery support unverified", true),
        new KeyboardProfile("f108pro", "AULA F108 Pro", "compatible", "Compatible receiver mode; battery support unverified", true)
    };
    public static KeyboardProfile Get(string id) { return All.FirstOrDefault(p => p.Id == id) ?? All[0]; }
    public static bool Accepts(KeyboardProfile profile, HidEndpoint endpoint) {
        return endpoint.Family != null && (profile.Family == "compatible" || endpoint.Family == profile.Family);
    }
    public static HidEndpoint Select(KeyboardProfile profile, IEnumerable<HidEndpoint> endpoints, string selectedPath) {
        var matches = endpoints.Where(e => Accepts(profile,e)).ToArray();
        if (!String.IsNullOrEmpty(selectedPath)) return matches.FirstOrDefault(e => e.Path.Equals(selectedPath,StringComparison.OrdinalIgnoreCase));
        if (matches.Length > 1) throw new InvalidOperationException("Multiple receivers found. Choose one from the Receiver menu.");
        return matches.FirstOrDefault();
    }
}

sealed class KeyboardSettings
{
    public string Model = "f75", ReceiverPath = "";
    internal static string FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AulaF75Battery","keyboard.txt");
    public static KeyboardSettings Load() {
        var settings = new KeyboardSettings();
        try { var lines=File.ReadAllLines(FileName); if(lines.Length>0) settings.Model=KeyboardProfiles.Get(lines[0]).Id; if(lines.Length>1) settings.ReceiverPath=lines[1]; } catch(IOException) { } catch(UnauthorizedAccessException) { }
        return settings;
    }
    public void Save() {
        Directory.CreateDirectory(Path.GetDirectoryName(FileName));
        File.WriteAllLines(FileName,new[] { Model, ReceiverPath });
    }
}
