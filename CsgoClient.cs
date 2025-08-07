using System;
using System.Timers;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.CSGO.Internal;

namespace SteamKit.CSGO
{
    /// <summary>
    /// Client for interacting with the Counter‑Strike 2 (CS:GO) Game Coordinator.
    /// This class is a simplified version of the CsgoClient from the open‑source
    /// SteamKit‑CSGO project.  It allows launching the game session and
    /// requesting player profiles through the GC.
    /// </summary>
    public partial class CsgoClient
    {
        // The application id for Counter‑Strike 2 / Counter‑Strike: Global Offensive
        private const int CsgoAppid = 730;

        private readonly bool _debug;
        private readonly SteamGameCoordinator _gameCoordinator;
        private readonly CallbackStore _gcMap = new();
        private readonly SteamClient _steamClient;
        private readonly SteamUser _steamUser;
        private readonly Timer HelloTimer;

        /// <summary>
        /// Constructs a CsgoClient using a logged in <see cref="SteamClient"/> and
        /// the associated <see cref="CallbackManager"/>.  You must provide the
        /// same callback manager used for login in order to receive GC messages.
        /// </summary>
        /// <param name="steamClient">The logged in Steam client.</param>
        /// <param name="callbackManager">The callback manager used when
        /// processing Steam callbacks.</param>
        /// <param name="debug">Whether to print debug messages to the console.</param>
        public CsgoClient(SteamClient steamClient, CallbackManager callbackManager, bool debug = false)
        {
            _steamClient = steamClient;
            _steamUser = steamClient.GetHandler<SteamUser>();
            _gameCoordinator = steamClient.GetHandler<SteamGameCoordinator>();
            _debug = debug;

            callbackManager.Subscribe<SteamGameCoordinator.MessageCallback>(OnGcMessage);

            HelloTimer = new Timer(1000);
            HelloTimer.AutoReset = true;
            HelloTimer.Elapsed += Knock;
        }

        /// <summary>
        /// Sends a periodic "hello" to the Game Coordinator until it replies.
        /// </summary>
        private void Knock(object? sender, ElapsedEventArgs e)
        {
            if (_debug)
                Console.WriteLine("Sending GC ClientHello");
            var clientMsg = new ClientGCMsgProtobuf<CMsgClientHello>((uint)EGCBaseClientMsg.k_EMsgGCClientHello);
            _gameCoordinator.Send(clientMsg, CsgoAppid);
        }

        /// <summary>
        /// Called whenever a message is received from the Game Coordinator.  This
        /// dispatches the message to the appropriate callback registered with
        /// <see cref="CallbackStore"/>.
        /// </summary>
        private void OnGcMessage(SteamGameCoordinator.MessageCallback obj)
        {
            if (_debug)
            {
                // Attempt to resolve the GC message name.  Fall back to base EMsg
                // names if unknown.
                string name = Enum.GetName(typeof(ECsgoGCMsg), obj.EMsg) ?? Enum.GetName(typeof(EMsg), obj.EMsg) ?? obj.EMsg.ToString();
                Console.WriteLine($"GC Message: {name}");
            }

            // Stop the hello timer once the GC welcomes us
            if (obj.EMsg == (uint)EGCBaseClientMsg.k_EMsgGCClientWelcome)
                HelloTimer.Stop();

            if (_gcMap.TryGetValue(obj.EMsg, out var func))
            {
                func(obj.Message);
            }
        }

        /// <summary>
        /// Launches the game session for CS:GO/CS2.  This will cause the Steam
        /// client to appear as playing the game and will automatically send
        /// periodic Hello messages until a GC welcome is received.  When the
        /// welcome arrives the provided callback is invoked.
        /// </summary>
        /// <param name="callback">The callback to invoke when the GC
        /// acknowledges the client.</param>
        public void Launch(Action<CMsgClientWelcome> callback)
        {
            // Register for the welcome message
            _gcMap.Add((uint)EGCBaseClientMsg.k_EMsgGCClientWelcome, msg =>
            {
                var welcome = new ClientGCMsgProtobuf<CMsgClientWelcome>(msg).Body;
                callback(welcome);
            });

            if (_debug)
                Console.WriteLine("Requesting to play CS:GO/CS2");

            // Inform Steam we are playing CS:GO/CS2
            var playGame = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
            playGame.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
            {
                game_id = CsgoAppid
            });
            _steamClient.Send(playGame);

            // Start the Hello timer
            HelloTimer.Start();
        }
    }
}