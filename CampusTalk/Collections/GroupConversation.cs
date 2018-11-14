using CampusTalk.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CampusTalk.Collections
{
    public class GroupConversation : Conversation
    {
        public GroupConversation()
        {
            groupUsers = new UserCollection();
        }

        private UserCollection groupUsers;

        public UserCollection GroupUsers
        {
            get { return groupUsers; }
            set { groupUsers = value; }
        }

        public override User.Status GetMessageUserStatus()
        {
            foreach (User u in groupUsers)
            {
                if (u.CurrentStatus == User.Status.Online || u.CurrentStatus == User.Status.Busy)
                    return User.Status.Online;
            }

            return User.Status.Offline;
        }
    }
}
