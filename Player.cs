using System;
using SteamKit2.GC;
using SteamKit2.GC.CSGO.Internal;

namespace SteamKit.CSGO
{
    public partial class CsgoClient
    {
        /// <summary>
        /// Requests a player profile from the CS:GO/CS2 Game Coordinator.  The
        /// account id passed here should be the 32‑bit account id (the low bits
        /// of a SteamID64).  When the Game Coordinator replies the provided
        /// callback is invoked with the deserialized <see cref="CMsgGCCStrike15_v2_PlayersProfile"/>.
        /// </summary>
        /// <param name="accountId">The 32‑bit account id of the player whose
        /// profile you wish to fetch.</param>
        /// <param name="callback">The callback to invoke when the profile
        /// message is received.</param>
        public void PlayerProfileRequest(uint accountId, Action<CMsgGCCStrike15_v2_PlayersProfile> callback)
        {
            // According to the SteamKit‑CSGO reference implementation the request_level
            // parameter controls how much ancillary data the GC includes.  A value
            // of 32 appears to return the full profile with XP and level fields.
            PlayerProfileRequest(accountId, 32, callback);
        }

        /// <summary>
        /// Requests a player profile from the CS:GO/CS2 Game Coordinator.
        /// </summary>
        /// <param name="accountId">The 32‑bit account id of the player whose
        /// profile to fetch.</param>
        /// <param name="requestLevel">Additional request flags; default is 32.</param>
        /// <param name="callback">Callback invoked when the profile message is
        /// received.</param>
        public void PlayerProfileRequest(uint accountId, uint requestLevel, Action<CMsgGCCStrike15_v2_PlayersProfile> callback)
        {
            // Register our callback for the expected message id
            _gcMap.Add((uint)ECsgoGCMsg.k_EMsgGCCStrike15_v2_PlayersProfile, msg =>
            {
                var body = new ClientGCMsgProtobuf<CMsgGCCStrike15_v2_PlayersProfile>(msg).Body;
                callback(body);
            });

            if (_debug)
                Console.WriteLine($"Requesting profile for account: {accountId}");

            // Create the request message
            var clientMsg = new ClientGCMsgProtobuf<CMsgGCCStrike15_v2_ClientRequestPlayersProfile>((uint)ECsgoGCMsg.k_EMsgGCCStrike15_v2_ClientRequestPlayersProfile)
            {
                Body =
                {
                    account_id = accountId,
                    request_level = requestLevel
                }
            };

            // Send to the GC
            _gameCoordinator.Send(clientMsg, CsgoAppid);
        }
    }
}