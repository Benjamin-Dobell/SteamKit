using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SteamKit2
{
    public partial class SteamMatchmaking
    {
        class LobbyCache
        {
            readonly ConcurrentDictionary<uint, ConcurrentDictionary<SteamID, Lobby>> lobbies =
                new ConcurrentDictionary<uint, ConcurrentDictionary<SteamID, Lobby>>();

            public Lobby GetLobby( uint appId, SteamID lobbySteamId )
            {
                return GetAppLobbies( appId ).TryGetValue( lobbySteamId, out var lobby ) ? lobby : null;
            }

            public void CacheLobby( uint appId, Lobby lobby )
            {
                GetAppLobbies( appId )[ lobby.SteamID ] = lobby;
            }

            public Lobby.Member AddLobbyMember( uint appId, SteamID lobbySteamId, SteamID memberId, string personaName )
            {
                var lobby = GetLobby( appId, lobbySteamId );

                if ( lobby == null )
                {
                    // Unknown lobby
                    return null;
                }

                var existingMember = lobby.Members.FirstOrDefault( m => m.SteamID == memberId );

                if ( existingMember != null )
                {
                    // Already in lobby
                    return null;
                }

                var addedMember = new Lobby.Member( memberId, personaName );

                var members = new List<Lobby.Member>( lobby.Members.Count + 1 );
                members.AddRange( lobby.Members );
                members.Add( addedMember );

                UpdateLobbyMembers( appId, lobby, members );

                return addedMember;
            }

            public Lobby.Member RemoveLobbyMember( uint appId, SteamID lobbySteamId, SteamID memberId )
            {
                var lobby = GetLobby( appId, lobbySteamId );
                var removedMember = lobby?.Members.FirstOrDefault( m => m.SteamID.Equals( memberId ) );

                if ( removedMember == null )
                {
                    // Not in a known lobby
                    return null;
                }

                var members = lobby.Members.Where( m => !m.Equals( removedMember ) ).ToList();
                UpdateLobbyMembers( appId, lobby, members );
                return removedMember;
            }

            void UpdateLobbyMembers( uint appId, Lobby lobby, IReadOnlyList<Lobby.Member> members )
            {
                CacheLobby( appId, new Lobby(
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
                ) );
            }

            ConcurrentDictionary<SteamID, Lobby> GetAppLobbies( uint appId )
            {
                return lobbies.GetOrAdd( appId, k => new ConcurrentDictionary<SteamID, Lobby>() );
            }
        }
    }
}
