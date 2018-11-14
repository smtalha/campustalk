using CampusTalk.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CampusTalk.Collections
{
    public class JsonConversation
    {
        public JsonConversation()
        {

        }

        private User messageUser;

        public User MessageUser
        {
            get { return messageUser; }
            set { messageUser = value; }
        }

        private List<Message> messages;

        public List<Message> Messages
        {
            get { return messages; }
            set { messages = value; }
        }

        private Message lastMessage;

        public Message LastMessage
        {
            get { return lastMessage; }
            set
            {
                lastMessage = value;
            }
        }

        private double selectedOpacity = 0.15;

        public double SelectedOpacity
        {
            get { return selectedOpacity; }
            set
            {
                selectedOpacity = value;
            }
        }

        private bool isGroupConvo;

        public bool IsGroupConvo
        {
            get { return isGroupConvo; }
            set { isGroupConvo = value; }
        }

        private List<User> groupUsers;

        public List<User> GroupUsers
        {
            get { return groupUsers; }
            set { groupUsers = value; }
        }
        
    }
}
