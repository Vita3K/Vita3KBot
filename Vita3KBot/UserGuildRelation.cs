using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Vita3KBot
{
    public static class UserGuildRelation
    {
        // < UserID : Dictionary < GuildID : List < SteamID > > >
        public static Dictionary<ulong, Dictionary<ulong, List<long>>> UserGuildRelationDictionary { get; private set; }

        static UserGuildRelation()
        {
            if (File.Exists("Resources/UserGuildRelation.json"))
            {
                var file = File.ReadAllText("Resources/UserGuildRelation.json");
                UserGuildRelationDictionary =
                    JsonConvert.DeserializeObject<Dictionary<ulong, Dictionary<ulong, List<long>>>>(
                        file);
            }
            else
            {
                UserGuildRelationDictionary = new Dictionary<ulong, Dictionary<ulong, List<long>>>();
                Save();
            }
        }

        public static void Save()
        {
            var json = JsonConvert.SerializeObject(UserGuildRelationDictionary, Formatting.Indented);
            File.WriteAllText("Resources/UserGuildRelation.json", json);
        }
    }
}