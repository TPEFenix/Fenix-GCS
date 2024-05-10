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
        public string RoomInfo { get; set; }
        public string HostUserID { get; set; }
        public ConcurrentList<string> MemberIDs { get; set; }

        public void AddUser(string userID)
        {
            if (!userID.Contains(userID))
            {
                MemberIDs.Add(userID);
                OnJoin?.Invoke(this,userID);
            }
        }
        public void RemoveUser(string userID)
        {
            if (userID.Contains(userID))
            {
                MemberIDs.Remove(userID);
                OnLeave?.Invoke(this, userID);
            }
        }

    }
}
