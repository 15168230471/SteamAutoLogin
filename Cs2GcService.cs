using System;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.CSGO.Internal;

namespace SteamAutoLogin
{
    /// <summary>
    /// Provides functionality to query the CS2/CS:GO Game Coordinator for a
    /// player's level and experience using the Steam networking protocol.  This
    /// avoids relying on the public Web API (which requires an API key).
    /// </summary>
    public static class Cs2GcService
    {
        /// <summary>
        /// Connects to Steam, logs on using the provided credentials and
        /// 2FA code (generated from the given .maFile), launches the CS2 game
        /// coordinator session and requests the logged in user's profile.  The
        /// returned tuple contains the player level and current XP.
        /// </summary>
        /// <param name="username">The Steam account username.</param>
        /// <param name="password">The Steam account password.</param>
        /// <param name="maFilePath">Path to the .maFile used to generate two
        /// factor codes.  The shared_secret from this file is used to compute
        /// the current Steam Guard code.</param>
        /// <returns>A tuple containing (Level, Xp).  Throws on failure.</returns>
        public static async Task<(int Level, ulong Xp)> GetPlayerLevelAndXpAsync(string username, string password, string maFilePath)
        {
            var steamClient = new SteamClient();
            var callbackManager = new CallbackManager(steamClient);
            var steamUser = steamClient.GetHandler<SteamUser>();

            // Use our CsgoClient wrapper to talk to the GC
            var csgoClient = new SteamKit.CSGO.CsgoClient(steamClient, callbackManager);

            // We'll complete this task once we have the profile or encounter an error
            var completionSource = new TaskCompletionSource<(int, ulong)>();

            // Flag to control the callback loop
            bool running = true;

            // Handle connection event
            callbackManager.Subscribe<SteamClient.ConnectedCallback>(callback =>
            {
                if (callback.Result != EResult.OK)
                {
                    completionSource.TrySetException(new Exception($"连接Steam失败: {callback.Result}"));
                    running = false;
                    return;
                }

                // Generate the current 2FA code using the .maFile's shared secret
                string twoFactorCode;
                try
                {
                    twoFactorCode = SteamGuardHelper.GetSteamGuardCode(maFilePath);
                }
                catch (Exception ex)
                {
                    completionSource.TrySetException(new Exception($"无法生成Steam令牌: {ex.Message}"));
                    running = false;
                    return;
                }

                var logOnDetails = new SteamUser.LogOnDetails
                {
                    Username = username,
                    Password = password,
                    TwoFactorCode = twoFactorCode,
                    ShouldRememberPassword = false
                };
                steamUser.LogOn(logOnDetails);
            });

            // Handle logon result
            callbackManager.Subscribe<SteamUser.LoggedOnCallback>(callback =>
            {
                if (callback.Result != EResult.OK)
                {
                    completionSource.TrySetException(new Exception($"登录失败: {callback.Result}"));
                    running = false;
                    return;
                }

                // After logging on, start our GC session.  We pass a no‑op callback here
                // because we only care about the subsequent profile message.
                csgoClient.Launch(_ => { /* GC welcome received */ });
            });

            // Handle machine auth (sentry) requests.  This simple implementation
            // simply acknowledges the request without writing a file.  Without
            // responding Steam may log the user off when the client is run on a
            // new machine.
            callbackManager.Subscribe<SteamUser.UpdateMachineAuthCallback>(machineCallback =>
            {
                var response = new SteamUser.UpdateMachineAuthResponse
                {
                    FileName = machineCallback.FileName,
                    BytesWritten = machineCallback.BytesToWrite,
                    FileSize = machineCallback.BytesToWrite,
                    FileHash = machineCallback.FileHash,
                    Result = EResult.OK,
                    LastError = 0,
                    OneTimePassword = machineCallback.OneTimePassword
                };
                steamUser.SendMachineAuthResponse(response);
            });

            // Once the GC sends us a welcome we can request our profile
            csgoClient.Launch(welcome =>
            {
                // Acquire our 32‑bit account id from the logged in user's SteamID
                uint accountId = steamUser.SteamID.AccountID;
                csgoClient.PlayerProfileRequest(accountId, profile =>
                {
                    try
                    {
                        // Attempt to extract the first profile from the returned message
                        var profilesProperty = profile.GetType().GetProperty("account_profiles") ?? profile.GetType().GetProperty("AccountProfiles");
                        var profiles = profilesProperty?.GetValue(profile) as System.Collections.IEnumerable;
                        if (profiles != null)
                        {
                            foreach (var p in profiles)
                            {
                                // Extract the level and XP properties.  Try both camelCase and PascalCase names
                                var levelProp = p.GetType().GetProperty("player_level") ?? p.GetType().GetProperty("PlayerLevel");
                                var xpProp = p.GetType().GetProperty("player_cur_xp") ?? p.GetType().GetProperty("PlayerCurXp");
                                if (levelProp != null && xpProp != null)
                                {
                                    int level = Convert.ToInt32(levelProp.GetValue(p));
                                    ulong xp = Convert.ToUInt64(xpProp.GetValue(p));
                                    completionSource.TrySetResult((level, xp));
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        completionSource.TrySetException(new Exception($"解析玩家信息失败: {ex.Message}"));
                    }
                    finally
                    {
                        // We're done – log off and disconnect
                        steamUser.LogOff();
                        steamClient.Disconnect();
                        running = false;
                    }
                });
            });

            // Handle disconnection events
            callbackManager.Subscribe<SteamClient.DisconnectedCallback>(callback =>
            {
                if (!completionSource.Task.IsCompleted)
                    completionSource.TrySetException(new Exception("与Steam的连接中断"));
                running = false;
            });

            // Connect to Steam
            steamClient.Connect();

            // Pump callbacks until we receive the result or an exception is set
            await Task.Run(() =>
            {
                while (running && !completionSource.Task.IsCompleted)
                {
                    callbackManager.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
                }
            });

            return await completionSource.Task;
        }
    }
}