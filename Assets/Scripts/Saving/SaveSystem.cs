using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    [Serializable]
    public class PlayerEntry
    {
        public string playerName;
        public int coins;
        public int health;
        public int[] slots;
        public int[] ammo;
        public Vector3 position;
        public float yaw;
        public bool hasPosition;   
    }

    [Serializable]
    public class CrewEntry
    {
        public Vector3 localPosition;  
        public Quaternion localRotation;
        public int health;
    }

    [Serializable]
    public class ShipEntry
    {
        public Vector3 position;
        public Quaternion rotation;
        public List<CrewEntry> crew = new List<CrewEntry>();
    }

    [Serializable]
    public class ItemEntry
    {
        public int itemId;
        public int ammo;
        public int shipIndex = -1;      // -1 = yerde serbest, >=0 = o gemiye bagli (pozisyon lokal)
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    public class BoatEntry
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    public class WorldSave
    {
        public string id;
        public string displayName;
        public long savedAtTicks;
        public List<PlayerEntry> players = new List<PlayerEntry>();
        public List<ShipEntry> ships = new List<ShipEntry>();
        public List<ItemEntry> items = new List<ItemEntry>();
        public List<BoatEntry> boats = new List<BoatEntry>();
    }

    private static readonly Dictionary<ulong, string> namesByClientId = new Dictionary<ulong, string>();
    private static readonly Dictionary<string, PlayerEntry> sessionPlayers = new Dictionary<string, PlayerEntry>();

    private static WorldSave pendingLoad;
    private static string currentSaveId;
    private static string currentSaveOwner;

    private static string SaveDir => Path.Combine(Application.persistentDataPath, "saves");

    // --- isim kaydi (baglanti onayi) ---
    public static void RegisterName(ulong clientId, string playerName)
    {
        namesByClientId[clientId] = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
    }

    public static string GetName(ulong clientId)
    {
        return namesByClientId.TryGetValue(clientId, out string name) ? name : "Player";
    }

    public static void SetPlayer(PlayerEntry entry)
    {
        sessionPlayers[entry.playerName] = entry;
    }

    public static PlayerEntry GetPlayer(string playerName)
    {
        return sessionPlayers.TryGetValue(playerName, out PlayerEntry entry) ? entry : null;
    }

    public static List<PlayerEntry> SessionPlayers()
    {
        return new List<PlayerEntry>(sessionPlayers.Values);
    }

    // --- menude secim: yeni oyun / kayit yukleme ---
    // kayit host'un girdigi kullanici adiyla anahtarlanir: ayni adla tekrar oynanip kaydedilince
    // ayni dosyanin ustune yazilir (kullanici basina tek kayit), listede de bu ad gorunur
    public static void NewGame(string ownerName)
    {
        pendingLoad = null;
        currentSaveOwner = string.IsNullOrWhiteSpace(ownerName) ? "Player" : ownerName.Trim();
        currentSaveId = Sanitize(currentSaveOwner);
        sessionPlayers.Clear();
    }

    // host baslamadan ONCE cagirilir: dosyayi okur, oyuncu onbellegini doldurur (ilk spawn'lar bunu okur).
    // WorldSave'i geri dondurur ki cagiran (ConnectionManager) displayName'i isim alanina yazip
    // baglanti isminin kayittaki isimle AYNI olmasini garanti etsin - aksi halde GetPlayer bulamaz.
    public static WorldSave PrepareLoad(string id)
    {
        WorldSave save = ReadWorld(id);
        pendingLoad = save;
        currentSaveId = id;
        currentSaveOwner = save != null ? save.displayName : id;

        sessionPlayers.Clear();
        if (save != null)
        {
            foreach (PlayerEntry entry in save.players)
            {
                sessionPlayers[entry.playerName] = entry;
            }
        }
        return save;
    }

    // Gameplay sahnesinde host dunyayi kurarken gemi/item kismini bundan okur
    public static WorldSave ConsumePendingLoad()
    {
        WorldSave save = pendingLoad;
        pendingLoad = null;
        return save;
    }

    // --- dosya islemleri ---
    public static List<WorldSave> ListSaves()
    {
        var result = new List<WorldSave>();
        if (!Directory.Exists(SaveDir))
        {
            return result;
        }

        foreach (string path in Directory.GetFiles(SaveDir, "*.json"))
        {
            try
            {
                WorldSave save = JsonUtility.FromJson<WorldSave>(File.ReadAllText(path));
                if (save != null)
                {
                    result.Add(save);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save could not be read: {path} ({e.Message})");
            }
        }

        result.Sort((a, b) => b.savedAtTicks.CompareTo(a.savedAtTicks));
        return result;
    }

    public static WorldSave ReadWorld(string id)
    {
        string path = PathFor(id);
        return File.Exists(path) ? JsonUtility.FromJson<WorldSave>(File.ReadAllText(path)) : null;
    }

    public static void WriteWorld(WorldSave save)
    {
        Directory.CreateDirectory(SaveDir);

        // kullanici adi kayit anahtari; ayni adla tekrar kaydedince ustune yazilir
        if (string.IsNullOrEmpty(currentSaveId))
        {
            currentSaveId = Sanitize(currentSaveOwner);
        }
        save.id = currentSaveId;
        save.savedAtTicks = DateTime.Now.Ticks;
        save.displayName = string.IsNullOrEmpty(currentSaveOwner) ? currentSaveId : currentSaveOwner;

        File.WriteAllText(PathFor(currentSaveId), JsonUtility.ToJson(save, true));
    }

    // kullanici adini gecerli bir dosya adina cevirir 
    private static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Player";
        }

        var builder = new System.Text.StringBuilder();
        foreach (char c in name.Trim())
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }
        return builder.Length > 0 ? builder.ToString() : "Player";
    }

    public static void DeleteWorld(string id)
    {
        string path = PathFor(id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string PathFor(string id)
    {
        return Path.Combine(SaveDir, id + ".json");
    }
}
