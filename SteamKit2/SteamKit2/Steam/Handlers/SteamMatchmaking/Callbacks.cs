using System.Collections.Generic;

namespace SteamKit2
{
    public partial class SteamMatchmaking
    {
        /// <summary>
        /// This callback is fired in response to <see cref="GetLobbyList"/>.
        /// </summary>
        public sealed class LobbyListCallback : CallbackMsg
        {
            /// <summary>
            /// ID of the app the lobbies belongs to.
            /// </summary>
            public uint AppID { get; }

            /// <summary>
            /// The result of the request.
            /// </summary>
            public EResult Result { get; }

            /// <summary>
            /// The list of lobbies matching the criteria specified with <see cref="GetLobbyList"/>.
            /// </summary>
            public List<Lobby> Lobbies { get; }

            internal LobbyListCallback( uint appId, EResult res, List<Lobby> lobbies )
            {
                AppID = appId;
                Result = res;
                Lobbies = lobbies;
            }
        }

        /// <summary>
        /// This callback is fired in response to <see cref="CreateLobby"/>.
        /// </summary>
        public sealed class CreateLobbyCallback : CallbackMsg
        {
            /// <summary>
            /// ID of the app the created lobby belongs to.
            /// </summary>
            public uint AppID { get; }

            /// <summary>
            /// The result of the request.
            /// </summary>
            public EResult Result { get; }

            /// <summary>
            /// The SteamID of the created lobby.
            /// </summary>
            public SteamID LobbySteamID { get; }

            internal CreateLobbyCallback( uint appId, EResult res, SteamID lobbySteamId )
            {
                AppID = appId;
                Result = res;
                LobbySteamID = lobbySteamId;
            }
        }

        /// <summary>
        /// This callback is fired in response to <see cref="SetLobbyData"/>.
        /// </summary>
        public class SetLobbyDataCallback : CallbackMsg
        {
            /// <summary>
            /// ID of app the targeted lobby belongs to.
            /// </summary>
            public uint AppID { get; }

            /// <summary>
            /// The result of the request.
            /// </summary>
            public EResult Result { get; }

            /// <summary>
            /// The SteamID of the targeted Lobby.
            /// </summary>
            public SteamID LobbySteamID { get; }

            internal SetLobbyDataCallback( uint appId, EResult res, SteamID lobbySteamId )
            {
                AppID = appId;
                Result = res;
                LobbySteamID = lobbySteamId;
            }
        }


        /// <summary>
        /// This callback is fired in response to <see cref="SetLobbyOwner"/>.
        /// </summary>
        public class SetLobbyOwnerCallback : CallbackMsg
        {
            /// <summary>
            /// ID of app the targeted lobby belongs to.
            /// </summary>
            public uint AppID { get; }

            /// <summary>
            /// The result of the request.
            /// </summary>
            public EResult Result { get; }

            /// <summary>
            /// The SteamID of the targeted Lobby.
            /// </summary>
            public SteamID LobbySteamId { get; }

            internal SetLobbyOwnerCallback( uint appId, EResult res, SteamID lobbySteamId )
            {
                AppID = appId;
                Result = res;
                LobbySteamId = lobbySteamId;
            }
        }

        /// <summary>
        /// This callback is fired whenever Steam sends us updated Lobby data.
        /// </summary>
        public class LobbyDataCallback : CallbackMsg
        {
            /// <summary>
            /// ID of app the updated lobby belongs to.
            /// </summary>
            public uint AppID { get; }

            /// <summary>
            /// The lobby that was updated. Keep in mind that it is not thread-safe to access collections
            /// members of this lobby on another thread as SteamMatchmaking may mutate it. However,
            /// <see cref="SteamMatchmaking.Lobby.Clone"/> is provided as a convenience method allowing you
            /// to easily create your own copy of this lobby's data.
            /// </summary>
            public Lobby Lobby { get; }

            internal LobbyDataCallback( uint appId, Lobby lobby )
            {
                AppID = appId;
                Lobby = lobby;
            }
        }

        /// <summary>
        /// This callback is fired whenever Steam informs us a user has joined a lobby.
        /// </summary>
        public class UserJoinedLobbyCallback : CallbackMsg
        {
            /// <summary>
            /// ID of app the lobby belongs to.
            /// </summary>
            public uint AppID { get; }

            /// <summary>
            /// The SteamID of the lobby that a member joined.
            /// </summary>
            public SteamID LobbySteamID { get; }

            /// <summary>
            /// The lobby member that joined.
            /// </summary>
            public Lobby.Member User { get; }

            internal UserJoinedLobbyCallback( uint appId, SteamID lobbySteamId, Lobby.Member user )
            {
                AppID = appId;
                LobbySteamID = lobbySteamId;
                User = user;
            }
        }

        /// <summary>
        /// This callback is fired whenever Steam informs us a user has left a lobby.
        /// </summary>
        public class UserLeftLobbyCallback : CallbackMsg
        {
            /// <summary>
            /// ID of app the lobby belongs to.
            /// </summary>
            public uint AppID { get; }

            /// <summary>
            /// The SteamID of the lobby that a member left.
            /// </summary>
            public SteamID LobbySteamID { get; }

            /// <summary>
            /// The lobby member that left.
            /// </summary>
            public Lobby.Member User { get; }

            internal UserLeftLobbyCallback( uint appId, SteamID lobbySteamId, Lobby.Member user )
            {
                AppID = appId;
                LobbySteamID = lobbySteamId;
                User = user;
            }
        }
    }
}
