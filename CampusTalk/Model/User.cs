using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace CampusTalk.Model
{
    public class User : INotifyPropertyChanged
    {


        public event PropertyChangedEventHandler PropertyChanged;
        //private string imageURL;

        public User() {
            //imageURL = "ms-appx:///Assets/UserImages/" + this.username;
            
        }

        private string name;

        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                Notify("Name");
            }
        }

        private string username;

        public string Username
        {
            get { return username; }
            set { username = value; }
        }

        private string email;

        public string Email
        {
            get { return email; }
            set { email = value; }
        }


        private Status currentStatus;

        public Status CurrentStatus
        {
            get { return currentStatus; }
            set
            {
                currentStatus = value;
                Notify("CurrentStatus");
            }
        }

        public enum Status
        {
            Offline,
            Online,
            Busy
        }

        private string ipAddress;

        public string IPAddress
        {
            get { return ipAddress; }
            set { ipAddress = value; }
        }

        private string profilePicture = "ms-appx:///Assets/default_profile_picture.png";

        public string ProfilePicture
        {
            get { return profilePicture; }
            set 
            { 
                profilePicture = value;
                Notify("ProfilePicture");                
            }
        }

        private bool isGroupUser;

        public bool IsGroupUser
        {
            get { return isGroupUser; }
            set { isGroupUser = value; }
        }
        
                
        
        private void Notify(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public static Status StringToStatus(string s)
        {
            switch(s)
            {
                case "Online":
                    return Status.Online;
                case "Busy":
                    return Status.Busy;
                case "Offline":
                    return Status.Offline;
                default:
                    return Status.Online;
            }
        }
    }
}
