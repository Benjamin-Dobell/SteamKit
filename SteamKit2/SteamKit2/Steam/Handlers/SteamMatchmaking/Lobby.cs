using System;
using System.Collections.Generic;
using System.IO;
using SteamKit2.Internal;
using static System.Text.Encoding;

namespace SteamKit2
{
    public partial class SteamMatchmaking
    {
        /// <summary>
        /// Represents a Steam lobby.
        /// </summary>
        public sealed class Lobby
        {
            /// <summary>
            /// The lobby filter base class.
            /// </summary>
            public abstract class Filter
            {
                /// <summary>
                /// The type of filter.
                /// </summary>
                public ELobbyFilterType FilterType { get; }

                /// <summary>
                /// The metadata key this filter pertains to. Under certain circumstances e.g. a distance
                /// filter, this will be an empty string.
                /// </summary>
                public string Key { get; }

                /// <summary>
                /// The comparison method used by this filter.
                /// </summary>
                public ELobbyFilterComparison Comparison { get; }

                /// <summary>
                /// Base constructor for all filter sub-classes.
                /// </summary>
                /// <param name="filterType">The type of filter.</param>
                /// <param name="key">The metadata key this filter pertains to.</param>
                /// <param name="comparison">The comparison method used by this filter.</param>
                protected Filter( ELobbyFilterType filterType, string key, ELobbyFilterComparison comparison )
                {
                    FilterType = filterType;
                    Key = key;
                    Comparison = comparison;
                }

                /// <summary>
                /// Serializes the filter into a representation used internally by SteamMatchmaking.
                /// </summary>
                /// <returns>A protobuf serializable representation of this filter.</returns>
                public virtual CMsgClientMMSGetLobbyList.Filter Serialize()
                {
                    CMsgClientMMSGetLobbyList.Filter filter = new CMsgClientMMSGetLobbyList.Filter();
                    filter.filter_type = ( int )FilterType;
                    filter.key = Key;
                    filter.comparision = ( int )Comparison;
                    return filter;
                }
            }

            /// <summary>
            /// Can be used to filter lobbies geographically.
            /// </summary>
            public sealed class DistanceFilter : Filter
            {
                /// <summary>
                /// Steam distance filter value.
                /// </summary>
                public ELobbyFilterDistance Value { get; }

                /// <summary>
                /// Initializes a new instance of the <see cref="DistanceFilter"/> class.
                /// </summary>
                /// <param name="value">Steam distance filter value.</param>
                public DistanceFilter( ELobbyFilterDistance value ) : base( ELobbyFilterType.Distance, "", ELobbyFilterComparison.Equal )
                {
                    Value = value;
                }

                /// <summary>
                /// Serializes the distance filter into a representation used internally by SteamMatchmaking.
                /// </summary>
                /// <returns>A protobuf serializable representation of this filter.</returns>
                public override CMsgClientMMSGetLobbyList.Filter Serialize()
                {
                    CMsgClientMMSGetLobbyList.Filter filter = base.Serialize();
                    filter.value = ( ( int )Value ).ToString();
                    return filter;
                }
            }

            /// <summary>
            /// Can be used to filter lobbies by comparing an integer against a value in each lobby's metadata.
            /// </summary>
            public sealed class NumericalFilter : Filter
            {
                /// <summary>
                /// Integer value to compare against.
                /// </summary>
                public int Value { get; }

                /// <summary>
                /// Initializes a new instance of the <see cref="NumericalFilter"/> class.
                /// </summary>
                /// <param name="key">The metadata key this filter pertains to.</param>
                /// <param name="comparison">The comparison method used by this filter.</param>
                /// <param name="value">Integer value to compare against.</param>
                public NumericalFilter( string key, ELobbyFilterComparison comparison, int value ) : base( ELobbyFilterType.Numerical, key, comparison )
                {
                    Value = value;
                }

                /// <summary>
                /// Serializes the numerical filter into a representation used internally by SteamMatchmaking.
                /// </summary>
                /// <returns>A protobuf serializable representation of this filter.</returns>
                public override CMsgClientMMSGetLobbyList.Filter Serialize()
                {
                    CMsgClientMMSGetLobbyList.Filter filter = base.Serialize();
                    filter.value = Value.ToString();
                    return filter;
                }
            }

            /// <summary>
            /// Can be used to filter lobbies by comparing a string against a value in each lobby's metadata.
            /// </summary>
            public sealed class StringFilter : Filter
            {
                /// <summary>
                /// String value to compare against.
                /// </summary>
                public string Value { get; }

                /// <summary>
                /// Initializes a new instance of the <see cref="StringFilter"/> class.
                /// </summary>
                /// <param name="key">The metadata key this filter pertains to.</param>
                /// <param name="comparison">The comparison method used by this filter.</param>
                /// <param name="value">String value to compare against.</param>
                public StringFilter( string key, ELobbyFilterComparison comparison, string value ) : base( ELobbyFilterType.String, key, comparison )
                {
                    Value = value;
                }

                /// <summary>
                /// Serializes the string filter into a representation used internally by SteamMatchmaking.
                /// </summary>
                /// <returns>A protobuf serializable representation of this filter.</returns>
                public override CMsgClientMMSGetLobbyList.Filter Serialize()
                {
                    CMsgClientMMSGetLobbyList.Filter filter = base.Serialize();
                    filter.value = Value;
                    return filter;
                }
            }

            /// <summary>
            /// Can be used to filter lobbies by comparing a string against a value in each lobby's metadata.
            /// </summary>
            public sealed class Member
            {
                /// <summary>
                /// SteamID of the lobby member.
                /// </summary>
                public SteamID SteamID { get; }

                /// <summary>
                /// Steam persona of the lobby member.
                /// </summary>
                public string PersonaName { get; }

                /// <summary>
                /// Metadata attached to the lobby member.
                /// </summary>
                public Dictionary<string, string> Metadata { get; }

                internal Member( SteamID steamId, string personaName, Dictionary<string, string> metadata = null )
                {
                    SteamID = steamId;
                    PersonaName = personaName;
                    Metadata = metadata ?? new Dictionary<string, string>();
                }

                /// <summary>
                /// Checks to see if this lobby member is equal to another. Only the SteamID of the lobby member is taken into account.
                /// </summary>
                /// <param name="obj"></param>
                /// <returns>true, if obj is <see cref="Member"/> with a matching SteamID. Otherwise, false.</returns>
                public override bool Equals( object obj )
                {
                    if ( obj is Member )
                    {
                        return SteamID.Equals( ( ( Member )obj ).SteamID );
                    }

                    return false;
                }

                /// <summary>
                /// Hash code of the lobby member. Only the SteamID of the lobby member is taken into account.
                /// </summary>
                /// <returns>The hash code of this lobby member.</returns>
                public override int GetHashCode()
                {
                    return SteamID.GetHashCode();
                }
            }

            /// <summary>
            /// SteamID of the lobby.
            /// </summary>
            public SteamID SteamID { get; }

            /// <summary>
            /// The type of the lobby.
            /// </summary>
            public ELobbyType LobbyType { get; }

            /// <summary>
            /// The lobby's flags.
            /// </summary>
            public int LobbyFlags { get; }

            /// <summary>
            /// The SteamID of the lobby's owner.
            /// </summary>
            public SteamID OwnerSteamID { get; }

            /// <summary>
            /// The metadata of the lobby; string key-value pairs.
            /// </summary>
            public Dictionary<string, string> Metadata { get; }

            /// <summary>
            /// The maximum number of members that can occupy the lobby.
            /// </summary>
            public int MaxMembers { get; }

            /// <summary>
            /// The number of member that are currently occupying the lobby.
            /// </summary>
            public int NumMembers { get; }

            /// <summary>
            /// A list of lobby members, this does not include the lobby owner.
            /// </summary>
            public List<Member> Members { get; }

            /// <summary>
            /// The distance of the lobby.
            /// </summary>
            public float? Distance { get; }

            /// <summary>
            /// The weight of the lobby.
            /// </summary>
            public long? Weight { get; }

            internal Lobby( SteamID steamId, ELobbyType lobbyType, int lobbyFlags, SteamID ownerSteamId, Dictionary<string, string> metadata, int maxMembers,
                int numMembers, List<Member> members, float? distance, long? weight )
            {
                if ( members != null && members.Count != numMembers - 1 )
                {
                    throw new ArgumentException( "when members is non-null, members.Count must be equal to numMembers - 1" );
                }

                SteamID = steamId;
                LobbyType = lobbyType;
                LobbyFlags = lobbyFlags;
                OwnerSteamID = ownerSteamId;
                Metadata = metadata ?? new Dictionary<string, string>();
                MaxMembers = maxMembers;
                NumMembers = numMembers;
                Members = members ?? new List<Member>();
                Distance = distance;
                Weight = weight;
            }

            /// <summary>
            /// Return a new <see cref="Lobby"/> instance with equivalent data as this lobby, but is safe to
            /// access on another thread.
            /// </summary>
            /// <returns>A new <see cref="Lobby"/> instance that is equivalent to this one.</returns>
            public Lobby Clone()
            {
                return new Lobby(
                    SteamID,
                    LobbyType,
                    LobbyFlags,
                    OwnerSteamID,
                    new Dictionary<string, string>( Metadata ),
                    MaxMembers,
                    NumMembers,
                    new List<Member>( Members ), // We're not doing a deep copy as Member is never mutated.
                    Distance,
                    Weight
                );
            }

            internal static byte[] EncodeMetadata( Dictionary<string, string> metadata )
            {
                using ( var ms = new MemoryStream() )
                {
                    using ( var writer = new BinaryWriter( ms ) )
                    {
                        byte[] header = { 0, 0 };
                        byte[] footer = { 8, 8 };

                        writer.Write( header );

                        foreach ( var pair in metadata )
                        {
                            if ( pair.Value == null ) continue;
                            writer.Write( ( byte )1 );
                            writer.Write( UTF8.GetBytes( pair.Key ) );
                            writer.Write( ( byte )0 );
                            writer.Write( UTF8.GetBytes( pair.Value ) );
                            writer.Write( ( byte )0 );
                        }

                        ;

                        writer.Write( footer );
                    }

                    return ms.ToArray();
                }
            }

            internal static Dictionary<string, string> DecodeMetadata( byte[] data )
            {
                Dictionary<string, string> metadata = new Dictionary<string, string>();

                bool parsingKey = true;
                string key = null;

                int dataEnd = data.Length - 3;

                if ( dataEnd < 0 || data[ 0 ] != 0 || data[ 1 ] != 0 || data[ 2 ] != 1 || data[ dataEnd + 1 ] != 8 || data[ dataEnd + 1 ] != 8 )
                {
                    throw new FormatException( "Lobby metadata is of an unexpected format" );
                }

                int stringStartIndex = 3;

                for ( int i = stringStartIndex; i <= dataEnd; i++ )
                {
                    if ( data[ i ] == 0 )
                    {
                        if ( parsingKey )
                        {
                            key = UTF8.GetString( data, stringStartIndex, i - stringStartIndex );
                            parsingKey = false;
                        }
                        else
                        {
                            string value = UTF8.GetString( data, stringStartIndex, i - stringStartIndex );
                            metadata.Add( key, value );

                            if ( ++i <= dataEnd && data[ i ] != 1 )
                            {
                                throw new FormatException( "Lobby metadata is of an unexpected format" );
                            }

                            parsingKey = true;
                        }

                        stringStartIndex = i + 1;
                    }
                }

                return metadata;
            }
        }
    }
}
