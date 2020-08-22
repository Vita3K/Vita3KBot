using System;
using System.Collections.Generic;
using System.Linq;
using Victoria;

namespace Vita3KBot
{
    public static class QueueManager
    {
        private static readonly Dictionary<ulong, Queue<LavaTrack>> Queue =
            new Dictionary<ulong, Queue<LavaTrack>>();
        
        public static string PushTrack(this ulong guildId, LavaTrack track)
        {
            Queue.TryAdd(guildId, new Queue<LavaTrack>());
            Queue[guildId].Enqueue(track);
            return "Successfully added to queue.";
        }

        public static LavaTrack PopTrack(this ulong guildId)
        {
            Queue.TryAdd(guildId, new Queue<LavaTrack>());
            if (!Queue[guildId].Any())
            {
                throw new InvalidOperationException("Queue empty");
            }

            return Queue[guildId].Dequeue();
        }

        public static void PopAll(this ulong guildId)
        {
            Queue.TryAdd(guildId, new Queue<LavaTrack>());
            Queue[guildId].Clear();
        }

        public static List<LavaTrack> PlayList(this ulong guildId)
        {
            Queue.TryAdd(guildId, new Queue<LavaTrack>());
            return Queue[guildId].ToList();
        }
    }
}
