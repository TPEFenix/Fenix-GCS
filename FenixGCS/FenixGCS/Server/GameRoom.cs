using System;
using System.Collections.Concurrent;
namespace FenixGCSApi.Server
{
    public delegate void GameRoomMemberModifyEvent(GameRoom sender, string userID);
    public class GameRoom
    {
        public GameRoomMemberModifyEvent OnJoin;
        public GameRoomMemberModifyEvent OnLeave;

        public string RoomID { get; set; }
        public int MaxMemberCount { get; set; }
        public string RoomInfo { get; set; }
        public string HostUserID { get; set; }
        public ConcurrentList<string> MemberIDs { get; set; } = new ConcurrentList<string>();

        public GameRoom(string roomID, int maxMemberCount, string hostUserID, string roomInfo = null)
        {
            RoomID = roomID;
            MaxMemberCount = maxMemberCount;
            HostUserID = hostUserID;
            RoomInfo = roomInfo;
        }

        public bool AddUser(string userID)
        {
            if (!userID.Contains(userID) && MemberIDs.Count < MaxMemberCount)
            {
                MemberIDs.Add(userID);
                OnJoin?.Invoke(this, userID);
                return true;
            }
            return false;
        }
        public bool RemoveUser(string userID)
        {
            if (userID.Contains(userID))
            {
                MemberIDs.Remove(userID);
                OnLeave?.Invoke(this, userID);
                return true;
            }
            return false;
        }

    }
}
