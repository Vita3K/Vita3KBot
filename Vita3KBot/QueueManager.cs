using System;
using System.Collections.Generic;
using System.Linq;
using SharpLink;

namespace Vita3KBot
{
    public static class QueueManager
    {
        private static readonly Dictionary<ulong, Queue<LavalinkTrack>> Queue =
            new Dictionary<ulong, Queue<LavalinkTrack>>();
        
        public static string PushTrack(this ulong guildId, LavalinkTrack track)
        {
            Queue.TryAdd(guildId, new Queue<LavalinkTrack>());
            Queue[guildId].Enqueue(track);
            return "Successfully added to queue.";
        }

        public static LavalinkTrack PopTrack(this ulong guildId)
        {
            Queue.TryAdd(guildId, new Queue<LavalinkTrack>());
            if (!Queue[guildId].Any())
            {
                throw new InvalidOperationException("Queue empty");
            }

            return Queue[guildId].Dequeue();
        }

        public static void PopAll(this ulong guildId)
        {
            Queue.TryAdd(guildId, new Queue<LavalinkTrack>());
            Queue[guildId].Clear();
        }

        public static List<LavalinkTrack> PlayList(this ulong guildId)
        {
            Queue.TryAdd(guildId, new Queue<LavalinkTrack>());
            return Queue[guildId].ToList();
        }
    }
}
