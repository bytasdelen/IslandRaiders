using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// oyuncu ilerlemesini (para, envanter, can) diske yazar/okur; dosyaya sadece sunucu dokunur.
// kayit anahtari baglanti ekraninda girilen oyuncu adi - ayni adla girince kaldigi yerden devam edilir
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
    }

    [Serializable]
    private class SaveData
    {
        public List<PlayerEntry> players = new List<PlayerEntry>();
    }

    private static readonly Dictionary<ulong, string> namesByClientId = new Dictionary<ulong, string>();

    private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

    // baglanti onayinda payload'dan gelen ad buraya kaydedilir, spawn'da clientId ile geri okunur
    public static void RegisterName(ulong clientId, string playerName)
    {
        namesByClientId[clientId] = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
    }

    public static string GetName(ulong clientId)
    {
        return namesByClientId.TryGetValue(clientId, out string name) ? name : "Player";
    }

    public static PlayerEntry Find(string playerName)
    {
        return Load().players.Find(p => p.playerName == playerName);
    }

    public static void Store(PlayerEntry entry)
    {
        SaveData data = Load();
        data.players.RemoveAll(p => p.playerName == entry.playerName);
        data.players.Add(entry);
        File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
    }

    private static SaveData Load()
    {
        if (!File.Exists(FilePath))
        {
            return new SaveData();
        }
        return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
    }
}
