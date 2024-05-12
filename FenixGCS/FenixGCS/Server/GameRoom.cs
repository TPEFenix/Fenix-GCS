using System;
using System.Collections.Concurrent;
namespace FenixGCSApi.Server
{
    /// <summary>
    /// 遊戲房間人數更動時的事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="userID"></param>
    public delegate void GameRoomMemberModifyEvent(GameRoom sender, string userID);

    public class GameRoom
    {
        #region Event
        /// <summary>
        /// 有使用者加入房間時
        /// </summary>
        public GameRoomMemberModifyEvent OnJoin;
        /// <summary>
        /// 有使用者離開房間時
        /// </summary>
        public GameRoomMemberModifyEvent OnLeave;
        #endregion

        /// <summary>
        ///  房間ID(應該要是伺服器中唯一的)
        /// </summary>
        public string RoomID { get; set; }

        /// <summary>
        ///  遊戲房間人數上限
        /// </summary>
        public int MaxMemberCount { get; set; }

        /// <summary>
        ///  遊戲房間的額外資訊(應由外部實現)
        /// </summary>
        public string RoomInfo { get; set; }

        /// <summary>
        /// 創建房間的使用者ID
        /// </summary>
        public string HostUserID { get; set; }

        /// <summary>
        ///  房間成員的使用者ID列表(0應為房主，順序不應隨便更動)
        /// </summary>
        public ConcurrentList<string> MemberIDs { get; private set; } = new ConcurrentList<string>();

        /// <summary>
        ///  建構子
        /// </summary>
        public GameRoom(string roomID, int maxMemberCount, string hostUserID, string roomInfo = null)
        {
            RoomID = roomID;
            MaxMemberCount = maxMemberCount;
            HostUserID = hostUserID;
            RoomInfo = roomInfo;
        }

        /// <summary>
        ///  使用者加入房間
        /// </summary>
        public bool AddUser(string userID)
        {
            if (!MemberIDs.Contains(userID) && MemberIDs.Count < MaxMemberCount)
            {
                MemberIDs.Add(userID);
                OnJoin?.Invoke(this, userID);
                return true;
            }
            return false;
        }

        /// <summary>
        ///  使用者離開房間
        /// </summary>
        public bool RemoveUser(string userID)
        {
            if (MemberIDs.Contains(userID))
            {
                MemberIDs.Remove(userID);
                OnLeave?.Invoke(this, userID);
                return true;
            }
            return false;
        }

    }
}
