using System;
using System.Collections.Generic;
using SteamKit2.GC;

namespace SteamKit.CSGO
{
    /// <summary>
    /// Stores callbacks keyed on message identifiers. When a message is
    /// handled the callback is dequeued and invoked.  This is a simplified
    /// version of the callback store used by the third‑party SteamKit‑CSGO
    /// library.
    /// </summary>
    internal class CallbackStore
    {
        private readonly Dictionary<uint, Queue<Action<IPacketGCMsg>>> _dict = new();

        /// <summary>
        /// Attempts to dequeue the next callback for the given key.
        /// </summary>
        /// <param name="key">The GC message id.</param>
        /// <param name="func">The callback to invoke when the message arrives.</param>
        /// <returns>True if a callback was found.</returns>
        public bool TryGetValue(uint key, out Action<IPacketGCMsg> func)
        {
            if (_dict.ContainsKey(key) && _dict[key].Count != 0)
            {
                func = _dict[key].Dequeue();
                return true;
            }
            func = null;
            return false;
        }

        /// <summary>
        /// Enqueues a callback to be executed when a message with the specified
        /// key arrives from the Game Coordinator.
        /// </summary>
        /// <param name="key">The GC message id to map the callback to.</param>
        /// <param name="action">The callback to register.</param>
        public void Add(uint key, Action<IPacketGCMsg> action)
        {
            if (!_dict.ContainsKey(key))
                _dict.Add(key, new Queue<Action<IPacketGCMsg>>());

            _dict[key].Enqueue(action);
        }
    }
}