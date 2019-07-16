using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SteamKit2.Internal;

namespace SteamKit2
{
    /// <summary>
    /// This handler is used for finding and creating.
    /// TODO: Join, invite and leave lobbies.
    /// </summary>
    public partial class SteamMatchmaking : ClientMsgHandler
    {
        readonly Dictionary<EMsg, Action<IPacketMsg>> dispatchMap;

        readonly ConcurrentDictionary<uint, ConcurrentDictionary<SteamID, Lobby>> lobbies =
            new ConcurrentDictionary<uint, ConcurrentDictionary<SteamID, Lobby>>();

        internal SteamMatchmaking()
        {
            dispatchMap = new Dictionary<EMsg, Action<IPacketMsg>>
            {
                { EMsg.ClientMMSCreateLobbyResponse, HandleCreateLobbyResponse },
                { EMsg.ClientMMSSetLobbyDataResponse, HandleSetLobbyDataResponse },
                { EMsg.ClientMMSSetLobbyOwnerResponse, HandleSetLobbyOwnerResponse },
                { EMsg.ClientMMSLobbyData, HandleLobbyData },
                { EMsg.ClientMMSGetLobbyListResponse, HandleLobbyListResponse },
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
        /// <returns>false, if the request could not be submitted i.e. you're not yet logged in. Otherwise, true.</returns>
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
                    persona_name_owner = personaName,
                }
            };

            Send( createLobby, appId );
            return true;
        }

        /// <summary>
        /// Sends a request to update a lobby.
        /// </summary>
        /// <param name="appId">ID of app the lobby belong to.</param>
        /// <param name="lobbyId">The SteamID of the lobby that should be updated.</param>
        /// <param name="lobbyType">The new lobby type.</param>
        /// <param name="maxMembers">The new maximum number of members that may occupy the lobby.</param>
        /// <param name="metadata">The new metadata for the lobby.</param>
        public void SetLobbyData( uint appId, SteamID lobbyId, ELobbyType lobbyType, int maxMembers, Dictionary<string, string> metadata )
        {
            var setLobbyData = new ClientMsgProtobuf<CMsgClientMMSSetLobbyData>( EMsg.ClientMMSSetLobbyData )
            {
                Body =
                {
                    app_id = appId,
                    steam_id_lobby = lobbyId.ConvertToUInt64(),
                    steam_id_member = 0,
                    max_members = maxMembers,
                    lobby_type = ( int )lobbyType,
                    lobby_flags = 0, // TODO: Lobby flags?
                    metadata = Lobby.EncodeMetadata( metadata )
                }
            };

            Send( setLobbyData, appId );
        }

        /// <summary>
        /// Sends a request to update the owner of a lobby.
        /// </summary>
        /// <param name="appId">ID of app the lobby belong to.</param>
        /// <param name="lobbyId">The SteamID of the lobby that should have its owner updated.</param>
        /// <param name="newOwner">The SteamID of the new owner.</param>
        public void SetLobbyOwner( uint appId, SteamID lobbyId, SteamID newOwner )
        {
            var setLobbyOwner = new ClientMsgProtobuf<CMsgClientMMSSetLobbyOwner>( EMsg.ClientMMSSetLobbyOwner )
            {
                Body =
                {
                    app_id = appId,
                    steam_id_lobby = lobbyId.ConvertToUInt64(),
                    steam_id_new_owner = newOwner.ConvertToUInt64(),
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
        /// <returns>false, if the request could not be submitted i.e. you're not yet logged in. Otherwise, true.</returns>
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
        /// Obtains a <see cref="Lobby"/> by its SteamID, if we already have the data for the lobby locally.
        /// This method does not send a network request.
        /// </summary>
        /// <param name="appId">The ID of app which we're attempting to obtain a lobby for.</param>
        /// <param name="lobbyId">The SteamID of the lobby that we wish to obtain.</param>
        /// <returns>The <see cref="Lobby"/> corresponding with the specified app and lobby ID, if we have its data locally. Otherwise, null.</returns>
        public Lobby GetLobby( uint appId, SteamID lobbyId )
        {
            Lobby lobby;
            var appLobbies = lobbies.GetOrAdd( appId, k => new ConcurrentDictionary<SteamID, Lobby>() );
            appLobbies.TryGetValue( lobbyId, out lobby );
            return lobby;
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

            Action<IPacketMsg> handler;

            if ( dispatchMap.TryGetValue( packetMsg.MsgType, out handler ) )
            {
                handler( packetMsg );
            }
        }

        #region ClientMsg Handlers

        void HandleCreateLobbyResponse( IPacketMsg packetMsg )
        {
            var lobbyListResponse = new ClientMsgProtobuf<CMsgClientMMSCreateLobbyResponse>( packetMsg );
            CMsgClientMMSCreateLobbyResponse body = lobbyListResponse.Body;

            Client.PostCallback( new CreateLobbyCallback(
                body.app_id,
                ( EResult )body.eresult,
                new SteamID( body.steam_id_lobby )
            ) );
        }

        void HandleSetLobbyDataResponse( IPacketMsg packetMsg )
        {
            var lobbyListResponse = new ClientMsgProtobuf<CMsgClientMMSSetLobbyDataResponse>( packetMsg );
            CMsgClientMMSSetLobbyDataResponse body = lobbyListResponse.Body;

            Client.PostCallback( new SetLobbyDataCallback(
                body.app_id,
                ( EResult )body.eresult,
                new SteamID( body.steam_id_lobby )
            ) );
        }

        void HandleSetLobbyOwnerResponse( IPacketMsg packetMsg )
        {
            var setLobbyOwnerResponse = new ClientMsgProtobuf<CMsgClientMMSSetLobbyOwnerResponse>( packetMsg );
            CMsgClientMMSSetLobbyOwnerResponse body = setLobbyOwnerResponse.Body;

            Client.PostCallback( new SetLobbyOwnerCallback(
                body.app_id,
                ( EResult )body.eresult,
                new SteamID( body.steam_id_lobby )
            ) );
        }

        void HandleLobbyListResponse( IPacketMsg packetMsg )
        {
            var lobbyListResponse = new ClientMsgProtobuf<CMsgClientMMSGetLobbyListResponse>( packetMsg );
            CMsgClientMMSGetLobbyListResponse body = lobbyListResponse.Body;

            var appLobbies = lobbies.GetOrAdd( body.app_id, k => new ConcurrentDictionary<SteamID, Lobby>() );
            List<Lobby> lobbyList =
                body.lobbies.ConvertAll( lobby =>
                {
                    var lobbyId = new SteamID( lobby.steam_id );
                    var existingLobby = appLobbies.ContainsKey( lobbyId ) ? appLobbies[ lobbyId ] : null;
                    var members = existingLobby?.Members;

                    return new Lobby(
                        lobbyId,
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
                appLobbies[ lobby.SteamID ] = lobby;
            }

            Client.PostCallback( new LobbyListCallback(
                body.app_id,
                ( EResult )body.eresult,
                lobbyList
            ) );
        }

        void HandleLobbyData( IPacketMsg packetMsg )
        {
            var lobbyListResponse = new ClientMsgProtobuf<CMsgClientMMSLobbyData>( packetMsg );
            CMsgClientMMSLobbyData body = lobbyListResponse.Body;

            var lobbyId = new SteamID( body.steam_id_lobby );
            var appLobbies = lobbies.GetOrAdd( body.app_id, k => new ConcurrentDictionary<SteamID, Lobby>() );
            var existingLobby = appLobbies.ContainsKey( lobbyId ) ? appLobbies[ lobbyId ] : null;

            List<Lobby.Member> memberList =
                body.members.ConvertAll( member => new Lobby.Member(
                    new SteamID( member.steam_id ),
                    member.persona_name,
                    Lobby.DecodeMetadata( member.metadata )
                ) );

            var updatedLobby = new Lobby(
                lobbyId,
                ( ELobbyType )body.lobby_type,
                body.lobby_flags,
                new SteamID( body.steam_id_owner ),
                Lobby.DecodeMetadata( body.metadata ),
                body.max_members,
                body.num_members,
                memberList,
                existingLobby?.Distance,
                existingLobby?.Weight
            );

            appLobbies[ lobbyId ] = updatedLobby;

            Client.PostCallback( new LobbyDataCallback( body.app_id, updatedLobby ) );
        }

        void UpdateLobbyMembers( ConcurrentDictionary<SteamID, Lobby> appLobbies, Lobby lobby, IReadOnlyList<Lobby.Member> members )
        {
            var updatedLobby = new Lobby(
                lobby.SteamID,
                lobby.LobbyType,
                lobby.LobbyFlags,
                lobby.OwnerSteamID,
                lobby.Metadata,
                lobby.MaxMembers,
                lobby.NumMembers,
                members,
                lobby.Distance,
                lobby.Weight
            );

            appLobbies[ lobby.SteamID ] = updatedLobby;
        }

        void HandleUserJoinedLobby( IPacketMsg packetMsg )
        {
            var userJoinedLobby = new ClientMsgProtobuf<CMsgClientMMSUserJoinedLobby>( packetMsg );
            CMsgClientMMSUserJoinedLobby body = userJoinedLobby.Body;

            var lobbyId = new SteamID( body.steam_id_lobby );
            var userId = new SteamID( body.steam_id_user );

            var appLobbies = lobbies.GetOrAdd( body.app_id, k => new ConcurrentDictionary<SteamID, Lobby>() );
            var lobby = appLobbies.ContainsKey( lobbyId ) ? appLobbies[ lobbyId ] : null;

            if ( lobby == null )
            {
                // Unknown lobby
                return;
            }

            var existingMember = lobby.Members.FirstOrDefault( m => m.SteamID == userId );

            if ( existingMember != null )
            {
                // Already in lobby
                return;
            }

            var joiningMember = new Lobby.Member( new SteamID( body.steam_id_user ), body.persona_name );

            var updatedMembers = new List<Lobby.Member>( lobby.Members.Count + 1 );
            updatedMembers.AddRange( lobby.Members );
            updatedMembers.Add( joiningMember );

            UpdateLobbyMembers( appLobbies, lobby, updatedMembers );

            Client.PostCallback( new UserJoinedLobbyCallback(
                body.app_id,
                new SteamID( body.steam_id_lobby ),
                joiningMember
            ) );
        }

        void HandleUserLeftLobby( IPacketMsg packetMsg )
        {
            var userLeftLobby = new ClientMsgProtobuf<CMsgClientMMSUserLeftLobby>( packetMsg );
            CMsgClientMMSUserLeftLobby body = userLeftLobby.Body;

            var lobbyId = new SteamID( body.steam_id_lobby );
            var userId = new SteamID( body.steam_id_user );

            var appLobbies = lobbies.GetOrAdd( body.app_id, k => new ConcurrentDictionary<SteamID, Lobby>() );
            var lobby = appLobbies.ContainsKey( lobbyId ) ? appLobbies[ lobbyId ] : null;
            var leavingMember = lobby?.Members.FirstOrDefault( m => m.SteamID == userId );

            if ( leavingMember == null )
            {
                // Not in a known lobby
                return;
            }

            var updatedMembers = lobby.Members.Where( m => !m.Equals( leavingMember ) ).ToList();
            UpdateLobbyMembers( appLobbies, lobby, updatedMembers );

            Client.PostCallback( new UserLeftLobbyCallback(
                body.app_id,
                new SteamID( body.steam_id_lobby ),
                leavingMember
            ) );
        }

        #endregion
    }
}
