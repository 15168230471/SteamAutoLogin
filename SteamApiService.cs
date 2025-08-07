using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SteamAutoLogin
{
    /// <summary>
    /// A helper service for invoking the Steam Web API to query player level and experience.
    /// This service calls the IPlayerService.GetBadges endpoint which returns the user's
    /// overall Steam XP and level information. Counter‑Strike 2 uses the same private
    /// experience system as the Steam profile, so this value can be used to determine
    /// your current level and how much XP you need to rank up.
    /// </summary>
    public static class SteamApiService
    {
        /// <summary>
        /// Queries the Steam Web API for the provided SteamID. The API key must be a
        /// valid Web API key associated with the calling account.
        /// The tuple returned contains:
        ///  - Level: The player's current Steam level
        ///  - XP: The player's current accumulated XP
        ///  - XpNeededCurrentLevel: The XP required to reach the current level
        ///  - XpNeededToLevelUp: Additional XP required to reach the next level
        /// </summary>
        /// <param name="steamId">The 64‑bit Steam ID of the user to query.</param>
        /// <param name="apiKey">A valid Steam Web API key.</param>
        /// <returns>A tuple with level and experience information.</returns>
        public static async Task<(int Level, int XP, int XpNeededCurrentLevel, int XpNeededToLevelUp)> GetPlayerLevelAndXpAsync(string steamId, string apiKey)
        {
            using (HttpClient client = new HttpClient())
            {
                // Compose the request URL. IPlayerService.GetBadges returns player_xp,
                // player_level and other XP progress metrics. See https://wiki.teamfortress.com/wiki/WebAPI/GetBadges for details.
                string url = $"https://api.steampowered.com/IPlayerService/GetBadges/v1/?key={apiKey}&steamid={steamId}";
                string jsonStr = await client.GetStringAsync(url);
                var json = JObject.Parse(jsonStr);
                var response = json["response"];
                if (response == null)
                {
                    // In the unlikely event that the response is missing, return zeros.
                    return (0, 0, 0, 0);
                }
                int xp = response.Value<int?>("player_xp") ?? 0;
                int level = response.Value<int?>("player_level") ?? 0;
                int xpNeededCurrent = response.Value<int?>("player_xp_needed_current_level") ?? 0;
                int xpNeededToLevelUp = response.Value<int?>("player_xp_needed_to_level_up") ?? 0;
                return (level, xp, xpNeededCurrent, xpNeededToLevelUp);
            }
        }
    }
}