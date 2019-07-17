using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SteamKit2.Internal;

namespace SteamKit2
{
    /// <summary>
    /// This handler is used for creating, joining and obtaining lobby information.
    /// </summary>
    public partial class SteamMatchmaking : ClientMsgHandler
    {
        readonly Dictionary<EMsg, Action<IPacketMsg>> dispatchMap;

        readonly LobbyCache lobbyCache = new LobbyCache();

        internal SteamMatchmaking()
        {
            dispatchMap = new Dictionary<EMsg, Action<IPacketMsg>>
            {
                { EMsg.ClientMMSCreateLobbyResponse, HandleCreateLobbyResponse },
                { EMsg.ClientMMSSetLobbyDataResponse, HandleSetLobbyDataResponse },
                { EMsg.ClientMMSSetLobbyOwnerResponse, HandleSetLobbyOwnerResponse },
                { EMsg.ClientMMSLobbyData, HandleLobbyData },
                { EMsg.ClientMMSGetLobbyListResponse, HandleLobbyListResponse },
                { EMsg.ClientMMSJoinLobbyResponse, HandleJoinLobbyResponse },
                { EMsg.ClientMMSLeaveLobbyResponse, HandleLeaveLobbyResponse },
                { EMsg.ClientMMSUserJoinedLobby, HandleUserJoinedLobby },
                { EMsg.ClientMMSUserLeftLobby, HandleUserLeftLobby },
            };
        }

        /// <summary>
        /// Sends a request to create a new lobby.
        /// </summary>
        /// <param name="appId">ID of the app the lobby will belong to.</param>
        /// <param name="type">The type of lobby.</param>
        /// <param name="maxMembers">The maximum number of members that may occupy the lobby.</param>
        /// <returns><c>false</c>, if the request could not be submitted i.e. not yet logged in. Otherwise, <c>true</c>.</returns>
        public bool CreateLobby( uint appId, ELobbyType type, int maxMembers )
        {
            if ( Client.CellID == null )
            {
                return false;
            }

            string personaName = Client.GetHandler<SteamFriends>().GetPersonaName();

            var createLobby = new ClientMsgProtobuf<CMsgClientMMSCreateLobby>( EMsg.ClientMMSCreateLobby )
            {
                Body =
                {
                    app_id = appId,
                    max_members = maxMembers,
                    lobby_type = ( int )type,
                    lobby_flags = 0,
                    cell_id = Client.CellID.Value,
                    public_ip = NetHelpers.GetIPAddress( Client.PublicIP ),
                    persona_name_owner = personaName
                }
            };

            Send( createLobby, appId );
            return true;
        }

        /// <summary>
        /// Sends a request to update a lobby.
        /// </summary>
        /// <param name="appId">ID of app the lobby belongs to.</param>
        /// <param name="lobbySteamId">The SteamID of the lobby that should be updated.</param>
        /// <param name="lobbyType">The new lobby type.</param>
        /// <param name="lobbyFlags">The new lobby flags.</param>
        /// <param name="maxMembers">The new maximum number of members that may occupy the lobby.</param>
        /// <param name="metadata">The new metadata for the lobby.</param>
        public void SetLobbyData( uint appId, SteamID lobbySteamId, ELobbyType lobbyType, int lobbyFlags, int maxMembers, Dictionary<string, string> metadata )
        {
            var setLobbyData = new ClientMsgProtobuf<CMsgClientMMSSetLobbyData>( EMsg.ClientMMSSetLobbyData )
            {
                Body =
                {
                    app_id = appId,
                    steam_id_lobby = lobbySteamId,
                    steam_id_member = 0,
                    max_members = maxMembers,
                    lobby_type = ( int )lobbyType,
                    lobby_flags = lobbyFlags,
                    metadata = Lobby.EncodeMetadata( metadata )
                }
            };

            Send( setLobbyData, appId );
        }

        /// <summary>
        /// Sends a request to update the current user's lobby metadata.
        /// </summary>
        /// <param name="appId">ID of app the lobby belongs to.</param>
        /// <param name="lobbySteamId">The SteamID of the lobby that should be updated.</param>
        /// <param name="metadata">The new metadata for the lobby.</param>
        /// <returns><c>false</c>, if the request could not be submitted i.e. not yet logged in. Otherwise, <c>true</c>.</returns>
        public bool SetLobbyMemberData( uint appId, SteamID lobbySteamId, Dictionary<string, string> metadata )
        {
            if ( Client.SteamID == null )
            {
                return false;
            }

            var setLobbyData = new ClientMsgProtobuf<CMsgClientMMSSetLobbyData>( EMsg.ClientMMSSetLobbyData )
            {
                Body =
                {
                    app_id = appId,
                    steam_id_lobby = lobbySteamId,
                    steam_id_member = Client.SteamID,
                    metadata = Lobby.EncodeMetadata( metadata )
                }
            };

            Send( setLobbyData, appId );
            return true;
        }

        /// <summary>
        /// Sends a request to update the owner of a lobby.
        /// </summary>
        /// <param name="appId">ID of app the lobby belongs to.</param>
        /// <param name="lobbySteamId">The SteamID of the lobby that should have its owner updated.</param>
        /// <param name="newOwner">The SteamID of the new owner.</param>
        public void SetLobbyOwner( uint appId, SteamID lobbySteamId, SteamID newOwner )
        {
            var setLobbyOwner = new ClientMsgProtobuf<CMsgClientMMSSetLobbyOwner>( EMsg.ClientMMSSetLobbyOwner )
            {
                Body =
                {
                    app_id = appId,
                    steam_id_lobby = lobbySteamId,
                    steam_id_new_owner = newOwner
                }
            };

            Send( setLobbyOwner, appId );
        }

        /// <summary>
        /// Sends a request to obtains a list of lobbies matching the specified criteria.
        /// </summary>
        /// <param name="appId">The ID of app for which we're requesting a list of lobbies.</param>
        /// <param name="filters">An optional list of filters.</param>
        /// <param name="maxLobbies">An optional maximum number of lobbies that will be returned.</param>
        /// <returns><c>false</c>, if the request could not be submitted i.e. not yet logged in. Otherwise, <c>true</c>.</returns>
        public bool GetLobbyList( uint appId, List<Lobby.Filter> filters = null, int maxLobbies = -1 )
        {
            if ( Client.CellID == null )
            {
                return false;
            }

            var getLobbies = new ClientMsgProtobuf<CMsgClientMMSGetLobbyList>( EMsg.ClientMMSGetLobbyList )
            {
                Body =
                {
                    app_id = appId,
                    cell_id = Client.CellID.Value,
                    public_ip = NetHelpers.GetIPAddress( Client.PublicIP ),
                    num_lobbies_requested = maxLobbies
                }
            };

            if ( filters != null )
            {
                foreach ( var filter in filters )
                {
                    getLobbies.Body.filters.Add( filter.Serialize() );
                }
            }

            Send( getLobbies, appId );

            return true;
        }

        /// <summary>
        /// Sends a request to join a lobby.
        /// </summary>
        /// <param name="appId">ID of app the lobby belongs to.</param>
        /// <param name="lobbySteamId">The SteamID of the lobby that should be joined.</param>
        /// <returns><c>false</c>, if the request could not be submitted i.e. not yet logged in. Otherwise, <c>true</c>.</returns>
        public bool JoinLobby( uint appId, SteamID lobbySteamId )
        {
            var personaName = Client.GetHandler<SteamFriends>()?.GetPersonaName();

            var joinLobby = new ClientMsgProtobuf<CMsgClientMMSJoinLobby>( EMsg.ClientMMSJoinLobby )
            {
                Body =
                {
                    app_id = appId,
                    persona_name = personaName,
                    steam_id_lobby = lobbySteamId
                }
            };

            Send( joinLobby, appId );

            return true;
        }

        /// <summary>
        /// Sends a request to leave a lobby.
        /// </summary>
        /// <param name="appId">ID of app the lobby belongs to.</param>
        /// <param name="lobbySteamId">The SteamID of the lobby that should be left.</param>
        /// <returns><c>false</c>, if the request could not be submitted i.e. not yet logged in. Otherwise, <c>true</c>.</returns>
        public void LeaveLobby( uint appId, SteamID lobbySteamId )
        {
            var leaveLobby = new ClientMsgProtobuf<CMsgClientMMSLeaveLobby>( EMsg.ClientMMSLeaveLobby )
            {
                Body =
                {
                    app_id = appId,
                    steam_id_lobby = lobbySteamId
                }
            };

            Send( leaveLobby, appId );
        }

        /// <summary>
        /// Sends a request to obtain a lobby's data.
        /// </summary>
        /// <param name="appId">The ID of app which we're attempting to obtain lobby data for.</param>
        /// <param name="lobbySteamId">The SteamID of the lobby whose data is being requested.</param>
        public void GetLobbyData( uint appId, SteamID lobbySteamId )
        {
            var getLobbyData = new ClientMsgProtobuf<CMsgClientMMSGetLobbyData>( EMsg.ClientMMSGetLobbyData )
            {
                Body =
                {
                    app_id = appId,
                    steam_id_lobby = lobbySteamId
                }
            };

            Send( getLobbyData, appId );
        }

        /// <summary>
        /// Sends a lobby invite request.
        /// NOTE: Steam provides no functionality to determine if the user was successfully invited.
        /// </summary>
        /// <param name="appId">The ID of app which owns the lobby we're inviting a user to.</param>
        /// <param name="lobbySteamId">The SteamID of the lobby we're inviting a user to.</param>
        /// <param name="userSteamId">The SteamID of the user we're inviting.</param>
        public void InviteToLobby( uint appId, SteamID lobbySteamId, SteamID userSteamId )
        {
            var getLobbyData = new ClientMsgProtobuf<CMsgClientMMSInviteToLobby>( EMsg.ClientMMSInviteToLobby )
            {
                Body =
                {
                    app_id = appId,
                    steam_id_lobby = lobbySteamId,
                    steam_id_user_invited = userSteamId
                }
            };

            Send( getLobbyData, appId );
        }

        /// <summary>
        /// Obtains a <see cref="Lobby"/>, by its SteamID, if the data is cached locally.
        /// This method does not send a network request.
        /// </summary>
        /// <param name="appId">The ID of app which we're attempting to obtain a lobby for.</param>
        /// <param name="lobbySteamId">The SteamID of the lobby that should be returned.</param>
        /// <returns>The <see cref="Lobby"/> corresponding with the specified app and lobby ID, if cached. Otherwise, <c>null</c>.</returns>
        public Lobby GetLobby( uint appId, SteamID lobbySteamId )
        {
            return lobbyCache.GetLobby( appId, lobbySteamId );
        }

        /// <summary>
        /// Sends a matchmaking message for a specific app.
        /// </summary>
        /// <param name="msg">The matchmaking message to send.</param>
        /// <param name="appId">The ID of the app this message pertains to.</param>
        public void Send( ClientMsgProtobuf msg, uint appId )
        {
            if ( msg == null )
            {
                throw new ArgumentNullException( nameof(msg) );
            }

            msg.ProtoHeader.routing_appid = appId;
            Client.Send( msg );
        }

        /// <summary>
        /// Handles a client message. This should not be called directly.
        /// </summary>
        /// <param name="packetMsg">The packet message that contains the data.</param>
        public override void HandleMsg( IPacketMsg packetMsg )
        {
            if ( packetMsg == null )
            {
                throw new ArgumentNullException( nameof(packetMsg) );
            }

            if ( dispatchMap.TryGetValue( packetMsg.MsgType, out var handler ) )
            {
                handler( packetMsg );
            }
        }

        #region ClientMsg Handlers

        void HandleCreateLobbyResponse( IPacketMsg packetMsg )
        {
            var lobbyListResponse = new ClientMsgProtobuf<CMsgClientMMSCreateLobbyResponse>( packetMsg );
            var body = lobbyListResponse.Body;

            Client.PostCallback( new CreateLobbyCallback(
                body.app_id,
                ( EResult )body.eresult,
                body.steam_id_lobby
            ) );
        }

        void HandleSetLobbyDataResponse( IPacketMsg packetMsg )
        {
            var lobbyListResponse = new ClientMsgProtobuf<CMsgClientMMSSetLobbyDataResponse>( packetMsg );
            var body = lobbyListResponse.Body;

            Client.PostCallback( new SetLobbyDataCallback(
                body.app_id,
                ( EResult )body.eresult,
                body.steam_id_lobby
            ) );
        }

        void HandleSetLobbyOwnerResponse( IPacketMsg packetMsg )
        {
            var setLobbyOwnerResponse = new ClientMsgProtobuf<CMsgClientMMSSetLobbyOwnerResponse>( packetMsg );
            var body = setLobbyOwnerResponse.Body;

            Client.PostCallback( new SetLobbyOwnerCallback(
                body.app_id,
                ( EResult )body.eresult,
                body.steam_id_lobby
            ) );
        }

        void HandleLobbyListResponse( IPacketMsg packetMsg )
        {
            var lobbyListResponse = new ClientMsgProtobuf<CMsgClientMMSGetLobbyListResponse>( packetMsg );
            var body = lobbyListResponse.Body;

            List<Lobby> lobbyList =
                body.lobbies.ConvertAll( lobby =>
                {
                    var existingLobby = lobbyCache.GetLobby( body.app_id, lobby.steam_id );
                    var members = existingLobby?.Members;

                    return new Lobby(
                        lobby.steam_id,
                        ( ELobbyType )lobby.lobby_type,
                        lobby.lobby_flags,
                        existingLobby?.OwnerSteamID,
                        Lobby.DecodeMetadata( lobby.metadata ),
                        lobby.max_members,
                        lobby.num_members,
                        members,
                        lobby.distance,
                        lobby.weight
                    );
                } );

            foreach ( var lobby in lobbyList )
            {
                lobbyCache.CacheLobby( body.app_id, lobby );
            }

            Client.PostCallback( new LobbyListCallback(
                body.app_id,
                ( EResult )body.eresult,
                lobbyList
            ) );
        }

        void HandleJoinLobbyResponse( IPacketMsg packetMsg )
        {
            var joinLobbyResponse = new ClientMsgProtobuf<CMsgClientMMSJoinLobbyResponse>( packetMsg );
            var body = joinLobbyResponse.Body;

            Lobby joinedLobby = null;

            if ( body.steam_id_lobbySpecified )
            {
                var members =
                    body.members.ConvertAll( member => new Lobby.Member(
                        member.steam_id,
                        member.persona_name,
                        Lobby.DecodeMetadata( member.metadata )
                    ) );

                var cachedLobby = lobbyCache.GetLobby( body.app_id, body.steam_id_lobby );

                joinedLobby = new Lobby(
                    body.steam_id_lobby,
                    ( ELobbyType )body.lobby_type,
                    body.lobby_flags,
                    body.steam_id_lobby,
                    Lobby.DecodeMetadata( body.metadata ),
                    body.max_members,
                    members.Count,
                    members,
                    cachedLobby?.Distance,
                    cachedLobby?.Weight
                );

                lobbyCache.CacheLobby( body.app_id, joinedLobby );
            }

            Client.PostCallback( new JoinLobbyCallback(
                body.app_id,
                ( EChatRoomEnterResponse )body.chat_room_enter_response,
                joinedLobby
            ) );
        }

        void HandleLeaveLobbyResponse( IPacketMsg packetMsg )
        {
            var leaveLobbyResponse = new ClientMsgProtobuf<CMsgClientMMSLeaveLobbyResponse>( packetMsg );
            var body = leaveLobbyResponse.Body;

            if ( body.eresult == ( int )EResult.OK )
            {
                lobbyCache.RemoveLobbyMember( body.app_id, body.steam_id_lobby, Client.SteamID );
            }

            Client.PostCallback( new LeaveLobbyCallback(
                body.app_id,
                ( EResult )body.eresult,
                body.steam_id_lobby
            ) );
        }

        void HandleLobbyData( IPacketMsg packetMsg )
        {
            var lobbyListResponse = new ClientMsgProtobuf<CMsgClientMMSLobbyData>( packetMsg );
            var body = lobbyListResponse.Body;

            var cachedLobby = lobbyCache.GetLobby( body.app_id, body.steam_id_lobby );
            var updatedLobby = new Lobby(
                body.steam_id_lobby,
                ( ELobbyType )body.lobby_type,
                body.lobby_flags,
                body.steam_id_owner,
                Lobby.DecodeMetadata( body.metadata ),
                body.max_members,
                body.num_members,
                body.members.ConvertAll( member => new Lobby.Member(
                    member.steam_id,
                    member.persona_name,
                    Lobby.DecodeMetadata( member.metadata )
                ) ),
                cachedLobby?.Distance,
                cachedLobby?.Weight
            );

            lobbyCache.CacheLobby( body.app_id, updatedLobby );

            Client.PostCallback( new LobbyDataCallback( body.app_id, updatedLobby ) );
        }

        void HandleUserJoinedLobby( IPacketMsg packetMsg )
        {
            var userJoinedLobby = new ClientMsgProtobuf<CMsgClientMMSUserJoinedLobby>( packetMsg );
            var body = userJoinedLobby.Body;
            var joiningMember = lobbyCache.AddLobbyMember( body.app_id, body.steam_id_lobby, body.steam_id_user, body.persona_name );

            if ( joiningMember != null )
            {
                Client.PostCallback( new UserJoinedLobbyCallback(
                    body.app_id,
                    body.steam_id_lobby,
                    joiningMember
                ) );
            }
        }

        void HandleUserLeftLobby( IPacketMsg packetMsg )
        {
            var userLeftLobby = new ClientMsgProtobuf<CMsgClientMMSUserLeftLobby>( packetMsg );
            var body = userLeftLobby.Body;

            var leavingMember = lobbyCache.RemoveLobbyMember( body.app_id, body.steam_id_lobby, body.steam_id_user );

            Client.PostCallback( new UserLeftLobbyCallback(
                body.app_id,
                body.steam_id_lobby,
                leavingMember
            ) );
        }

        #endregion
    }
}
