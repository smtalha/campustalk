using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace CampusTalk.Model
{
    public class Message : INotifyPropertyChanged
    {


        public Message()
        {
            TimeStamp = DateTime.Now;
        }


        public enum MessageType
        {
            Text,
            Buzz,
            Attachment
        }

        private User msgUser;

        public User MsgUser
        {
            get { return msgUser; }
            set { msgUser = value; }
        }
        

        private string text;

        public string Text
        {
            get { return text; }
            set
            {
                text = value;
            }
        }

        private DateTime timeStamp;

        public DateTime TimeStamp
        {
            get { return timeStamp; }
            set
            {
                timeStamp = value;
            }
        }


        private MessageType type;

        public MessageType Type
        {
            get { return type; }
            set 
            { 
                type = value;
                if (value == MessageType.Attachment)
                    isAttachment = true;
            }
        }

        private string attachment;

        public string Attachment
        {
            get { return attachment; }
            set { attachment = value; }
        }

        private bool isAttachment = false;

        public bool IsAttachment
        {
            get { return isAttachment; }
            set { isAttachment = value; }
        }


        private bool unRead = false;

        public bool UnRead
        {
            get { return unRead; }
            set { unRead = value; Notify("UnRead"); }
        }
        
        

        private bool sentSide = false;

        public bool SentSide
        {
            get { return sentSide; }
            set { sentSide = value; }
        }

        private bool receivedSide = false;

        public bool ReceivedSide
        {
            get { return receivedSide; }
            set { receivedSide = value; }
        }

        private bool isGroupMessage = false;

        public bool IsGroupMessage
        {
            get { return isGroupMessage; }
            set { isGroupMessage = value; }
        }

        private string sentToGroupUsers;

        public string SentToGroupUsers
        {
            get { return sentToGroupUsers; }
            set { sentToGroupUsers = value; }
        }
        

        private bool isImageProp = false;

        public bool IsImageProp
        {
            get { return isImageProp; }
            set { isImageProp = value; }
        }

        private bool isVoiceMsgProp;

        public bool IsVoiceMsgProp
        {
            get { return isVoiceMsgProp; }
            set { isVoiceMsgProp = value; }
        }


        public async Task<bool> IsImage()
        {
            if (attachment != null)
            {
                List<string> fileTypeFilter = new List<string>();
                fileTypeFilter.Add(".jpg");
                fileTypeFilter.Add(".jpeg");
                fileTypeFilter.Add(".png");

                if (fileTypeFilter.Contains((await StorageFile.GetFileFromPathAsync(attachment)).FileType))
                    return true;
                else
                    return false;
            }

            return false;
        }
        public async Task<bool> IsVoiceMsg()
        {
            if (attachment != null)
            {
                List<string> fileTypeFilter = new List<string>();
                fileTypeFilter.Add(".wav");

                if (fileTypeFilter.Contains((await StorageFile.GetFileFromPathAsync(attachment)).FileType))
                    return true;
                else
                    return false;
            }

            return false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Notify(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
