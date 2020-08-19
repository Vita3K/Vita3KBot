using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Vita3KBot
{
    internal static class LinkedAccounts
    {
        public static Dictionary<ulong, long> UserDictionary { get; private set; }

        static LinkedAccounts()
        {
            if (!Directory.Exists("Resources")) Directory.CreateDirectory("Resources");

            if (File.Exists("Resources/LinkedAccounts.json"))
            {
                var file = File.ReadAllText("Resources/LinkedAccounts.json");
                UserDictionary = JsonConvert.DeserializeObject<Dictionary<ulong, long>>(file);
            }
            else
            {
                UserDictionary = new Dictionary<ulong, long>();
                Save();
            }
        }

        public static void Save()
        {
            var json = JsonConvert.SerializeObject(UserDictionary, Formatting.Indented);
            File.WriteAllText("Resources/LinkedAccounts.json", json);
        }
    }
}