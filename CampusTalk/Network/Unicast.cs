using CampusTalk.Collections;
using CampusTalk.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using System.IO;
using Windows.ApplicationModel;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml.Media.Animation;
using CampusTalk.Encryption;
using Newtonsoft.Json;

namespace CampusTalk.Network
{
    public class Unicast
    {
        #region CLASS ATTRIBUTES


        ListView recentMessageList;


        DatagramSocket textMessageSocket;

        StreamSocketListener fileTransferListener;
        StreamSocketListener profilePictureListner;

        User receivedAttachmentUser;
        string[] receivedAttachmentGroupUsers;


        string unicastServicePort;
        string fileTransferServicePort;
        string profilePictureServicePort;

        bool textSocketIsConnected;
        bool attachmentSocketIsConnected;

        MediaElement messagePop;
        MediaElement buzzSound;

        Storyboard BuzzAnimation;
        #endregion

        #region CLASS CONSTRUCTOR
        public Unicast(MediaElement messageSound, MediaElement buzz, Storyboard buzzAnim, ListView recentList)
        {
            unicastServicePort = ChatScreen.applicationData.Unicast_msg_port;
            fileTransferServicePort = ChatScreen.applicationData.Unicast_file_port;
            profilePictureServicePort = ChatScreen.applicationData.Unicast_picture_port;

            recentMessageList = recentList;
            messagePop = messageSound;
            buzzSound = buzz;
            BuzzAnimation = buzzAnim;
            textSocketIsConnected = false;
            attachmentSocketIsConnected = false;
            InitializeSockets();
        }

        #endregion

        #region INITIALIZE SOCKETS
        private async void InitializeSockets()
        {

            fileTransferListener = new StreamSocketListener();
            fileTransferListener.ConnectionReceived += fileTransferListener_ConnectionReceived;

            profilePictureListner = new StreamSocketListener();
            profilePictureListner.ConnectionReceived += profilePictureListner_ConnectionReceived;

            textMessageSocket = new DatagramSocket();
            textMessageSocket.MessageReceived += textMessageSocket_MessageReceived;
            try
            {
                await textMessageSocket.BindServiceNameAsync(unicastServicePort, NetworkInformation.GetInternetConnectionProfile().NetworkAdapter);
                await fileTransferListener.BindServiceNameAsync(fileTransferServicePort, SocketProtectionLevel.PlainSocket, NetworkInformation.GetInternetConnectionProfile().NetworkAdapter);
                await profilePictureListner.BindServiceNameAsync(profilePictureServicePort, SocketProtectionLevel.PlainSocket, NetworkInformation.GetInternetConnectionProfile().NetworkAdapter);
            }
            catch (Exception ex)
            {
                if (SocketError.GetStatus(ex.HResult) == SocketErrorStatus.Unknown)
                {
                    throw;
                }


                return;
            }

        }

        #endregion

        #region TEXT MESSAGE
        private async void textMessageSocket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            Conversation WasSelected = null;
            var myDisp = CoreApplication.MainView.CoreWindow.Dispatcher;
            await myDisp.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (recentMessageList.SelectedItem != null)
                    WasSelected = recentMessageList.SelectedItem as Conversation;
            });

            try
            {
                uint StringLength = args.GetDataReader().UnconsumedBufferLength;
                string encryptedMessage = args.GetDataReader().ReadString(StringLength);
                string _Message = StringCipher.Decrypt(encryptedMessage);

                if (_Message.StartsWith("buzz"))
                {
                    string buzzUsername = _Message.Split(':')[1];
                    User buzzUser = null;
                    Message rMessage = new Message() { Type = Message.MessageType.Buzz, ReceivedSide = true, Text = "Sent a buzz!", UnRead = true };


                    var disp = CoreApplication.MainView.CoreWindow.Dispatcher;
                    await disp.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        foreach (User u in ChatScreen.availableUsers)
                        {
                            if (u.Username.Equals(buzzUsername))
                                buzzUser = u;
                        }
                        if (buzzUser == null)
                            return; // change this to request the user later
                        rMessage.MsgUser = buzzUser;
                        foreach (Conversation c in ChatScreen.recentMessages)
                        {
                            if (c.MessageUser.Username.Equals(buzzUser.Username))
                            {
                                if (WasSelected == c)
                                    rMessage.UnRead = false;
                                c.Messages.Add(rMessage);
                                c.LastMessage = rMessage;
                                if (ChatScreen.recentMessages.IndexOf(c) != 0)
                                {
                                    ChatScreen.recentMessages.Move(ChatScreen.recentMessages.IndexOf(c), 0);
                                }
                                BuzzAnimation.Begin();
                                buzzSound.Play();
                                if (WasSelected != null)
                                    recentMessageList.SelectedItem = WasSelected;
                                return;
                            }
                        }

                        Conversation newCon = new Conversation();
                        newCon.MessageUser = buzzUser;
                        newCon.Messages.Add(rMessage);
                        newCon.LastMessage = rMessage;
                        ChatScreen.recentMessages.Add(newCon);

                        if (ChatScreen.recentMessages.IndexOf(newCon) != 0)
                        {
                            ChatScreen.recentMessages.Move(ChatScreen.recentMessages.IndexOf(newCon), 0);
                            if (WasSelected != null)
                                recentMessageList.SelectedItem = WasSelected;
                        }
                        BuzzAnimation.Begin();
                        buzzSound.Play();
                        StoreConversations();
                    });

                    return;
                }

                if (_Message.StartsWith("groupBuzz"))
                {
                    Message rMsg = new Message() { Text = "Sent a buzz!", ReceivedSide = true, Type = Message.MessageType.Buzz, UnRead = true };

                    string[] splitUsers = _Message.Split(';');
                    var disp2 = CoreApplication.MainView.CoreWindow.Dispatcher;
                    await disp2.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        BuzzAnimation.Begin();
                        buzzSound.Play();
                    });
                    await ReceiveGroupTextMessage(rMsg, splitUsers);
                    return;
                }

                string[] arr = GetTextMessage(_Message);

                string _messageType = arr[0];
                string _userName = arr[3];
                string _textMessage = arr[6];

                if (_textMessage.StartsWith("ProfilePictureRequest12345"))
                {
                    if (!ChatScreen.loggedInUser.ProfilePicture.Equals("ms-appx:///Assets/default_profile_picture.png"))
                        await SendProfilePicture(new User() { IPAddress = args.RemoteAddress.DisplayName });
                    return;
                }

                if (_userName.StartsWith("group"))
                {
                    Message rMsg = new Message();
                    rMsg.ReceivedSide = true;
                    rMsg.Text = _textMessage;
                    rMsg.Type = Message.MessageType.Text;
                    rMsg.UnRead = true;

                    string[] splitUsers = _userName.Split(';');

                    await ReceiveGroupTextMessage(rMsg, splitUsers);
                    return;
                }

                Message receivedMessage = new Message() { UnRead = true };
                User msgUser = null;

                if (_messageType.Equals(Message.MessageType.Text.ToString()))
                    receivedMessage.Type = Message.MessageType.Text;
                else
                    receivedMessage.Type = Message.MessageType.Buzz;

                receivedMessage.ReceivedSide = true;
                receivedMessage.Text = _textMessage;




                var d = CoreApplication.MainView.CoreWindow.Dispatcher;
                await d.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {

                    foreach (User u in ChatScreen.availableUsers)
                    {
                        if (u.Username.Equals(_userName))
                            msgUser = u;
                    }

                    if (msgUser == null)
                        return; // change this to request the user later
                    receivedMessage.MsgUser = msgUser;
                    foreach (Conversation c in ChatScreen.recentMessages)
                    {
                        if (c.MessageUser.Username.Equals(msgUser.Username))
                        {
                            if (WasSelected == c)
                                receivedMessage.UnRead = false;
                            c.Messages.Add(receivedMessage);
                            c.LastMessage = receivedMessage;
                            if (ChatScreen.recentMessages.IndexOf(c) != 0)
                            {
                                ChatScreen.recentMessages.Move(ChatScreen.recentMessages.IndexOf(c), 0);
                            }
                            messagePop.Play();
                            if (WasSelected != null)
                                recentMessageList.SelectedItem = WasSelected;
                            StoreConversations();
                            return;
                        }
                    }

                    Conversation newConvo = new Conversation();
                    newConvo.MessageUser = msgUser;
                    newConvo.Messages.Add(receivedMessage);
                    newConvo.LastMessage = receivedMessage;
                    ChatScreen.recentMessages.Add(newConvo);

                    if (ChatScreen.recentMessages.IndexOf(newConvo) != 0)
                    {
                        ChatScreen.recentMessages.Move(ChatScreen.recentMessages.IndexOf(newConvo), 0);

                    }
                    messagePop.Play();
                    if (WasSelected != null)
                        recentMessageList.SelectedItem = WasSelected;

                    StoreConversations();
                });

            }

            catch (Exception ex)
            {
                if (SocketError.GetStatus(ex.HResult) == SocketErrorStatus.Unknown)
                {
                    throw;
                }
            }


        }

        private async Task ReceiveGroupTextMessage(Message rMsg, string[] splitUsers)
        {
            Conversation WasSelected = null;
            var myDisp = CoreApplication.MainView.CoreWindow.Dispatcher;
            await myDisp.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (recentMessageList.SelectedItem != null)
                    WasSelected = recentMessageList.SelectedItem as Conversation;
            });

            string grp_pp = "ms-appx:///Assets/default_group_profile_picture.png";

            var disp = CoreApplication.MainView.CoreWindow.Dispatcher;
            await disp.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                foreach (User u in ChatScreen.availableUsers)
                {
                    if (u.Username.Equals(splitUsers[1]))
                    {
                        rMsg.MsgUser = u;
                        break;
                    }
                }
                if (rMsg.MsgUser == null)
                    return;

                List<User> groupMessageUsers = new List<User>();
                for (int i = 1; i < splitUsers.Length; i++)
                {
                    if (!ChatScreen.loggedInUser.Username.Equals(splitUsers[i]))
                    {
                        User tempU = null;
                        foreach (User u in ChatScreen.availableUsers)
                        {
                            if (u.Username.Equals(splitUsers[i]))
                            {
                                tempU = u;
                                break;
                            }
                        }
                        if (tempU != null)
                            groupMessageUsers.Add(tempU);
                        else
                            groupMessageUsers.Add(new User() { Username = splitUsers[i] });
                    }
                }

                foreach (Conversation c in ChatScreen.recentMessages)
                {
                    if (c is GroupConversation)
                    {
                        int count = 0;

                        foreach (User usr in groupMessageUsers)
                        {
                            foreach (User u in ((GroupConversation)c).GroupUsers)
                            {
                                if (usr.Username.Equals(u.Username))
                                {
                                    count++;
                                    break;
                                }
                            }
                        }

                        if ((count == ((GroupConversation)c).GroupUsers.Count) && (count == groupMessageUsers.Count))
                        {

                            if (WasSelected == c)
                                rMsg.UnRead = false;
                            c.Messages.Add(rMsg);
                            c.LastMessage = rMsg;
                            if (ChatScreen.recentMessages.IndexOf(c) != 0)
                            {
                                ChatScreen.recentMessages.Move(ChatScreen.recentMessages.IndexOf(c), 0);
                            }
                            messagePop.Play();
                            if (WasSelected != null)
                                recentMessageList.SelectedItem = WasSelected;
                            StoreConversations();
                            return;
                        }
                    }
                }

                GroupConversation newGroupConvo = new GroupConversation();

                string displayName = "";
                string userNames = "";
                string emails = "";
                foreach (User u in groupMessageUsers)
                {
                    displayName += u.Name;
                    userNames += u.Username;
                    emails += u.Email;

                    if (groupMessageUsers.Last() != u)
                    {
                        displayName += ", ";
                        userNames += ", ";
                        emails += ", ";
                    }

                    newGroupConvo.GroupUsers.Add(u);
                }
                User grpMsgUser = new User() { Username = userNames, Email = emails, Name = displayName, ProfilePicture = grp_pp, IsGroupUser = true };
                newGroupConvo.MessageUser = grpMsgUser;
                newGroupConvo.Messages.Add(rMsg);
                newGroupConvo.LastMessage = rMsg;
                ChatScreen.recentMessages.Add(newGroupConvo);

                if (ChatScreen.recentMessages.IndexOf(newGroupConvo) != 0)
                {
                    ChatScreen.recentMessages.Move(ChatScreen.recentMessages.IndexOf(newGroupConvo), 0);
                }
                messagePop.Play();
                if (WasSelected != null)
                    recentMessageList.SelectedItem = WasSelected;
                StoreConversations();
            });

        }

        public async Task SendTextMessage(User rec, Message msg, List<User> groupMessageUsers = null)
        {
            string toSend;
            if (msg.Type == Message.MessageType.Buzz && groupMessageUsers == null)
            {
                toSend = "buzz:" + ChatScreen.loggedInUser.Username;
            }
            else if (msg.Type == Message.MessageType.Buzz && groupMessageUsers != null)
            {
                //send buzz to group
                toSend = "groupBuzz;";
                toSend += ChatScreen.loggedInUser.Username + ";";
                foreach (User u in groupMessageUsers)
                {
                    toSend += u.Username;
                    if (u != groupMessageUsers.Last())
                        toSend += ";";
                }
            }
            else
            {
                if (groupMessageUsers != null)
                    toSend = ParseGroupTextMessage(groupMessageUsers, msg);
                else
                    toSend = ParseTextMessage(msg);
            }

            try
            {
                IOutputStream stream = await textMessageSocket.GetOutputStreamAsync(new HostName(rec.IPAddress), unicastServicePort);
                using (var MyDataWriter = new DataWriter(stream))
                {
                    string encryptedMessage = StringCipher.Encrypt(toSend);
                    MyDataWriter.WriteString(encryptedMessage);
                    await MyDataWriter.StoreAsync();
                    MyDataWriter.DetachStream();
                }
                stream.Dispose();
            }
            catch (Exception ex)
            {
                if (SocketError.GetStatus(ex.HResult) == SocketErrorStatus.Unknown)
                {
                    throw;
                }
            }
        }

        public async Task SendTextMessageToGroup(List<User> users, Message msg)
        {
            foreach (User u in users)
            {
                if (u.CurrentStatus == User.Status.Online || u.CurrentStatus == User.Status.Busy)
                    await SendTextMessage(u, msg, users);
            }
        }

        #endregion

        #region ATTACHMENT MESSAGE
        public async Task SendAttachment(User rec, StorageFile fileToSend, List<User> groupMessageUsers = null)
        {
            StreamSocket fileTransferSocket = new StreamSocket();
            await fileTransferSocket.ConnectAsync(new HostName(rec.IPAddress), fileTransferServicePort);


            byte[] buff = new byte[1024];
            var prop = await fileToSend.GetBasicPropertiesAsync();
            using (var dataOutputStream = new DataWriter(fileTransferSocket.OutputStream))
            {
                if (groupMessageUsers != null)
                {
                    string usrs = "";
                    usrs += "group;";
                    usrs += ChatScreen.loggedInUser.Username + ";";
                    foreach (User u in groupMessageUsers)
                    {
                        usrs += u.Username;
                        if (u != groupMessageUsers.Last())
                            usrs += ";";
                    }
                    dataOutputStream.WriteInt32(usrs.Length);
                    dataOutputStream.WriteString(usrs);
                }
                else
                {
                    dataOutputStream.WriteInt32(ChatScreen.loggedInUser.Username.Length);
                    dataOutputStream.WriteString(ChatScreen.loggedInUser.Username);
                }

                dataOutputStream.WriteInt32(fileToSend.Name.Length);
                dataOutputStream.WriteString(fileToSend.Name);
                dataOutputStream.WriteUInt64(prop.Size);
                var fileStream = await fileToSend.OpenStreamForReadAsync();
                while (fileStream.Position < (long)prop.Size)
                {
                    var rlen = await fileStream.ReadAsync(buff, 0, buff.Length);
                    dataOutputStream.WriteBytes(buff);
                }
                await dataOutputStream.FlushAsync();
                await dataOutputStream.StoreAsync();
                await fileTransferSocket.OutputStream.FlushAsync();

            }
            fileTransferSocket.Dispose();
        }

        public async Task SendAttachmentToGroup(List<User> users, StorageFile fileToSend)
        {
            foreach (User u in users)
            {
                if (u.CurrentStatus == User.Status.Online || u.CurrentStatus == User.Status.Busy)
                    await SendAttachment(u, fileToSend, users);
            }
        }

        private async void fileTransferListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            Conversation WasSelected = null;
            var myDisp = CoreApplication.MainView.CoreWindow.Dispatcher;
            await myDisp.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (recentMessageList.SelectedItem != null)
                    WasSelected = recentMessageList.SelectedItem as Conversation;
            });

            StreamSocket receiver = args.Socket;
            StorageFile fileSaved = await ReceiveFileFomPeer(receiver, ChatScreen.localFolder, false);

            User msgUser = null;

            if (receivedAttachmentUser == null)
                return;


            msgUser = receivedAttachmentUser;

            if (msgUser == null)
                return;

            Message msg = new Message() { ReceivedSide = true, Type = Message.MessageType.Attachment, MsgUser = msgUser, Attachment = fileSaved.Path };

            if (await msg.IsImage())
            {
                msg.Text = "Sent a photo";
                msg.IsImageProp = true;
            }
            else if (await msg.IsVoiceMsg())
            {
                msg.Text = "Sent a voice message    ";
                msg.IsVoiceMsgProp = true;
            }

            else
                msg.Text = "Attachment: " + fileSaved.DisplayName;

            if (receivedAttachmentGroupUsers != null)
            {
                msg.IsGroupMessage = true;
                await ReceiveGroupAttachmentMessage(msg);
            }
            else
            {
                var d = CoreApplication.MainView.CoreWindow.Dispatcher;
                await d.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    foreach (Conversation c in ChatScreen.recentMessages)
                    {
                        if (c.MessageUser.Username.Equals(msgUser.Username))
                        {
                            c.Messages.Add(msg);
                            c.LastMessage = msg;
                            if (ChatScreen.recentMessages.IndexOf(c) != 0)
                            {
                                ChatScreen.recentMessages.Move(ChatScreen.recentMessages.IndexOf(c), 0);
                            }
                            receivedAttachmentUser = null;
                            messagePop.Play();
                            if (WasSelected != null)
                                recentMessageList.SelectedItem = WasSelected;
                            StoreConversations();
                            return;
                        }
                    }

                    Conversation newConvo = new Conversation();
                    newConvo.MessageUser = msgUser;
                    newConvo.Messages.Add(msg);
                    newConvo.LastMessage = msg;
                    ChatScreen.recentMessages.Add(newConvo);

                    if (ChatScreen.recentMessages.IndexOf(newConvo) != 0)
                    {
                        ChatScreen.recentMessages.Move(ChatScreen.recentMessages.IndexOf(newConvo), 0);
                    }
                    receivedAttachmentUser = null;
                    messagePop.Play();
                    if (WasSelected != null)
                        recentMessageList.SelectedItem = WasSelected;

                    StoreConversations();
                });
            }




        }

        private async Task ReceiveGroupAttachmentMessage(Message rMsg)
        {
            Conversation WasSelected = null;
            var myDisp = CoreApplication.MainView.CoreWindow.Dispatcher;
            await myDisp.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (recentMessageList.SelectedItem != null)
                    WasSelected = recentMessageList.SelectedItem as Conversation;
            });

            string grp_pp = "ms-appx:///Assets/default_group_profile_picture.png";

            var disp = CoreApplication.MainView.CoreWindow.Dispatcher;
            await disp.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                List<User> groupMessageUsers = new List<User>();
                for (int i = 1; i < receivedAttachmentGroupUsers.Length; i++)
                {
                    if (!ChatScreen.loggedInUser.Username.Equals(receivedAttachmentGroupUsers[i]))
                    {
                        User tempU = null;
                        foreach (User u in ChatScreen.availableUsers)
                        {
                            if (u.Username.Equals(receivedAttachmentGroupUsers[i]))
                            {
                                tempU = u;
                                break;
                            }
                        }
                        if (tempU != null)
                            groupMessageUsers.Add(tempU);
                        else
                            groupMessageUsers.Add(new User() { Username = receivedAttachmentGroupUsers[i] });
                    }
                }

                foreach (Conversation c in ChatScreen.recentMessages)
                {
                    if (c is GroupConversation)
                    {
                        int count = 0;

                        foreach (User usr in groupMessageUsers)
                        {
                            foreach (User u in ((GroupConversation)c).GroupUsers)
                            {
                                if (usr.Username.Equals(u.Username))
                                {
                                    count++;
                                    break;
                                }
                            }
                        }

                        if ((count == ((GroupConversation)c).GroupUsers.Count) && (count == groupMessageUsers.Count))
                        {
                            c.Messages.Add(rMsg);
                            c.LastMessage = rMsg;
                            if (ChatScreen.recentMessages.IndexOf(c) != 0)
                            {
                                ChatScreen.recentMessages.Move(ChatScreen.recentMessages.IndexOf(c), 0);
                            }
                            messagePop.Play();
                            if (WasSelected != null)
                                recentMessageList.SelectedItem = WasSelected;
                            StoreConversations();
                            return;
                        }
                    }
                }

                GroupConversation newGroupConvo = new GroupConversation();

                string displayName = "";
                string userNames = "";
                string emails = "";
                foreach (User u in groupMessageUsers)
                {
                    displayName += u.Name;
                    userNames += u.Username;
                    emails += u.Email;

                    if (groupMessageUsers.Last() != u)
                    {
                        displayName += ", ";
                        userNames += ", ";
                        emails += ", ";
                    }

                    newGroupConvo.GroupUsers.Add(u);
                }
                User grpMsgUser = new User() { Username = userNames, Email = emails, Name = displayName, ProfilePicture = grp_pp, IsGroupUser = true };
                newGroupConvo.MessageUser = grpMsgUser;
                newGroupConvo.Messages.Add(rMsg);
                newGroupConvo.LastMessage = rMsg;
                ChatScreen.recentMessages.Add(newGroupConvo);

                if (ChatScreen.recentMessages.IndexOf(newGroupConvo) != 0)
                {
                    ChatScreen.recentMessages.Move(ChatScreen.recentMessages.IndexOf(newGroupConvo), 0);
                }
                messagePop.Play();
                if (WasSelected != null)
                    recentMessageList.SelectedItem = WasSelected;
                StoreConversations();
            });
        }

        private async Task<StorageFile> ReceiveFileFomPeer(StreamSocket socket, StorageFolder folder, bool replaceExisting, string outputFilename = null)
        {

            StorageFile file;
            using (var dataInputStream = new DataReader(socket.InputStream))
            {
                await dataInputStream.LoadAsync(sizeof(Int32));
                var usernameLength = (uint)dataInputStream.ReadInt32();
                await dataInputStream.LoadAsync(usernameLength);
                var username = dataInputStream.ReadString(usernameLength);

                var d = CoreApplication.MainView.CoreWindow.Dispatcher;
                await d.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    if (username.StartsWith("group"))
                    {
                        string[] splitUsers = username.Split(';');
                        foreach (User u in ChatScreen.availableUsers)
                        {
                            if (u.Username.Equals(splitUsers[1]))
                                receivedAttachmentUser = u;
                        }

                        if (receivedAttachmentUser == null)
                            return;

                        receivedAttachmentGroupUsers = splitUsers;

                    }
                    else
                    {
                        foreach (User u in ChatScreen.availableUsers)
                        {
                            if (username.Equals(u.Username))
                                receivedAttachmentUser = u;
                        }

                        if (receivedAttachmentUser == null)
                            return;
                    }

                });

                await dataInputStream.LoadAsync(sizeof(Int32));
                var filenameLength = (uint)dataInputStream.ReadInt32();
                await dataInputStream.LoadAsync(filenameLength);
                var originalFilename = dataInputStream.ReadString(filenameLength);
                if (outputFilename == null)
                {
                    outputFilename = originalFilename;
                }
                await dataInputStream.LoadAsync(sizeof(UInt64));
                var fileLength = dataInputStream.ReadUInt64();
                using (var memStream = await DownloadFile(dataInputStream, fileLength))
                {
                    
                        try
                        {
                            StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);
                            if (replaceExisting == true)
                                file = await folder.CreateFileAsync(outputFilename,
                                    CreationCollisionOption.ReplaceExisting);
                            else
                                file = await folder.CreateFileAsync(outputFilename,
                                    CreationCollisionOption.GenerateUniqueName);
                        }
                        catch (Exception e)
                        {
                            return null;
                        }
                        using (var fileStream1 = await file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            await RandomAccessStream.CopyAndCloseAsync(
                                memStream.GetInputStreamAt(0), fileStream1.GetOutputStreamAt(0));
                        }

                        dataInputStream.DetachStream();
                    
                }
                return file;
            }
        }

        private async Task<InMemoryRandomAccessStream> DownloadFile(DataReader rw, ulong fileLength)
        {
            var memStream = new InMemoryRandomAccessStream();
            // Download the file
            while (memStream.Position < fileLength)
            {
                var lenToRead = Math.Min(1024, fileLength - memStream.Position);
                await rw.LoadAsync((uint)lenToRead);
                var tempBuff = rw.ReadBuffer((uint)lenToRead);
                await memStream.WriteAsync(tempBuff);
            }

            return memStream;
        }

        #endregion

        #region PROFILE PICTURE
        public async Task SendProfilePicture(User rec)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            StreamSocket pictureSendSocket = new StreamSocket();
            await pictureSendSocket.ConnectAsync(new HostName(rec.IPAddress), profilePictureServicePort);

            StorageFile fileToSend = await StorageFile.GetFileFromPathAsync(ChatScreen.loggedInUser.ProfilePicture);

            byte[] buff = new byte[1024];
            var prop = await fileToSend.GetBasicPropertiesAsync();
            using (var dataOutputStream = new DataWriter(pictureSendSocket.OutputStream))
            {
                dataOutputStream.WriteInt32(ChatScreen.loggedInUser.Username.Length);
                dataOutputStream.WriteString(ChatScreen.loggedInUser.Username);
                dataOutputStream.WriteInt32(fileToSend.Name.Length);
                dataOutputStream.WriteString(fileToSend.Name);
                dataOutputStream.WriteUInt64(prop.Size);
                var fileStream = await fileToSend.OpenStreamForReadAsync();
                while (fileStream.Position < (long)prop.Size)
                {
                    var rlen = await fileStream.ReadAsync(buff, 0, buff.Length);
                    dataOutputStream.WriteBytes(buff);
                }
                await dataOutputStream.FlushAsync();
                await dataOutputStream.StoreAsync();
                await pictureSendSocket.OutputStream.FlushAsync();

            }
            pictureSendSocket.Dispose();
        }

        private async void profilePictureListner_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            StreamSocket receiver = args.Socket;
            StorageFolder profilePicturesFolder = ApplicationData.Current.TemporaryFolder;
            StorageFile image = await ReceiveFileFomPeer(receiver, profilePicturesFolder, true);

            if (receivedAttachmentUser == null)
                return;

            var dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;
            await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                foreach (User u in ChatScreen.availableUsers)
                {
                    if (u.Username.Equals(receivedAttachmentUser.Username))
                    {

                        if (image != null)
                            u.ProfilePicture = image.Path;

                    }
                }
            });
            receivedAttachmentUser = null;
        }

        public async Task RequestPicture(User rec)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            await SendTextMessage(rec, new Message() { Text = "ProfilePictureRequest12345" });
        }

        #endregion

        #region BUZZ
        public async Task Buzz(User rec)
        {
            await SendTextMessage(rec, new Message() { Type = Message.MessageType.Buzz });
        }

        public async Task BuzzToGroup(List<User> users)
        {
            foreach (User u in users)
            {
                if (u.CurrentStatus == User.Status.Online || u.CurrentStatus == User.Status.Busy)
                    await SendTextMessage(u, new Message() { Type = Message.MessageType.Buzz }, users);
            }
        }

        #endregion

        #region SUPPORTING METHODS
        public void TearDownTextSocket()
        {
            if (textMessageSocket != null)
            {
                textMessageSocket.Dispose();
                textMessageSocket = null;
            }
        }

        private string ParseTextMessage(Message msg)
        {

            return msg.Type.ToString() + "~|*" + ChatScreen.loggedInUser.Username + "~|*" + msg.Text;
        }

        private string ParseGroupTextMessage(List<User> users, Message msg)
        {
            string s = "";
            s += msg.Type.ToString() + "~|*";
            s += "group;";
            s += ChatScreen.loggedInUser.Username + ";";
            foreach (User u in users)
            {
                s += u.Username;
                if (u != users.Last())
                    s += ";";
            }
            s += "~|*";
            s += msg.Text;

            return s;
        }

        private string[] GetTextMessage(string msg)
        {
            return msg.Split(new char[] { '~', '|', '*' });

        }

        private string[] GetGroupTextMessage(string msg)
        {
            return msg.Split(new char[] { '~', '|', '*' });
        }

        #endregion

        #region LOCAL STORAGE
        
        private List<JsonConversation> GetRecentMessagesAsList(ObservableCollection<Conversation> convos)
        {
            List<JsonConversation> result = new List<JsonConversation>();
            foreach (Conversation c in convos)
            {
                if (c is GroupConversation)
                    result.Add(new JsonConversation() { LastMessage = c.LastMessage, Messages = c.Messages.ToList(), MessageUser = c.MessageUser, SelectedOpacity = c.SelectedOpacity, GroupUsers = ((GroupConversation)c).GroupUsers.ToList(), IsGroupConvo = true });
                else
                    result.Add(new JsonConversation() { LastMessage = c.LastMessage, Messages = c.Messages.ToList(), MessageUser = c.MessageUser, SelectedOpacity = c.SelectedOpacity, IsGroupConvo = false });
            }

            return result;
        }

        private async Task StoreConversations()
        {
            try
            {
                StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", ChatScreen.localFolder);
                // Serialize our Product class into a string             
                string jsonContents = JsonConvert.SerializeObject(GetRecentMessagesAsList(ChatScreen.recentMessages));
                StorageFile textFile = await ChatScreen.localFolder.CreateFileAsync(ChatScreen.loggedInUser.Username + ".json",
                                             CreationCollisionOption.ReplaceExisting);
                // Open the file...      
                using (IRandomAccessStream textStream = await textFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    // write the JSON string!
                    using (DataWriter textWriter = new DataWriter(textStream))
                    {
                        textWriter.WriteString(jsonContents);
                        await textWriter.StoreAsync();
                    }
                }
            }
            catch (Exception ex)
            {

            }

        }

        #endregion
    }
}
