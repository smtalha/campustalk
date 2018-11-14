using CampusTalk.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CampusTalk.Collections
{
    public class Conversation : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public Conversation()
        {
            messages = new ObservableCollection<Message>();
            messageUser = new User();
            lastMessage = new Message();
        }
        private User messageUser;

        public User MessageUser
        {
            get { return messageUser; }
            set 
            { 
                messageUser = value;
                Notify("MessageUser");
            }
        }

        private ObservableCollection<Message> messages;

        public ObservableCollection<Message> Messages
        {
            get { return messages; }
            set
            {
                messages = value; 
                Notify("Messages");
            }
        }

        private Message lastMessage;

        public Message LastMessage
        {
            get { return lastMessage; }
            set 
            { 
                lastMessage = value;
                Notify("LastMessage");
            }
        }

        private double selectedOpacity = 0.15;

        public double SelectedOpacity
        {
            get { return selectedOpacity; }
            set 
            { 
                selectedOpacity = value;
                Notify("SelectedOpacity");
            }
        }
                

        private void Notify(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public virtual User.Status GetMessageUserStatus()
        {
            return messageUser.CurrentStatus;
        }

    }
}
