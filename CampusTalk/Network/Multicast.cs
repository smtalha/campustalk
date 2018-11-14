using CampusTalk.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using CampusTalk;
using CampusTalk.Collections;
using Windows.UI.Xaml;
using Windows.ApplicationModel.Core;
using Windows.Networking.Connectivity;
using System.Collections.ObjectModel;
using CampusTalk.Encryption;
using Windows.UI.Xaml.Controls;
using Windows.Storage;
using Newtonsoft.Json;

namespace CampusTalk.Network
{
    public class Multicast
    {
        #region CLASS ATTRIBUTES

        Unicast unicast;
        MulticastPacket packet;


        MediaElement messageSound;
        ListView recentMessageList;

        private DatagramSocket socket;

        private HostName multicastIP;
        private string multicastPort;

        private bool socketIsConnected;

        StorageFolder localFolder;

        #endregion

        #region CLASS CONSTRUCTOR
        public Multicast(Unicast uni, MediaElement msgPop, ListView recentList)
        {
            multicastIP = new HostName(ChatScreen.applicationData.Multicast_ip);
            multicastPort = ChatScreen.applicationData.Multicast_port;
            unicast = uni;
            recentMessageList = recentList;
            messageSound = msgPop;
            socketIsConnected = false;
            packet = new MulticastPacket(ChatScreen.loggedInUser, true);
            SetUpLocalFolder();
            JoinMulticastGroup();
        }

        #endregion

        #region INITIALIZE SOCKETS AND JOIN GROUP MULTICAST GROUP
        public async Task JoinMulticastGroup()
        {
            await StartMulticastListenerAsync(multicastPort);
            await SendMessage(Parse(packet), multicastPort);
        }

        private async Task<DatagramSocket> StartMulticastListenerAsync(string port)
        {
            if (socketIsConnected)
                return socket;

            socket = new DatagramSocket();
            // You must register handlers before calling BindXxxAsync  
            socket.MessageReceived += SocketOnMessageReceived;
            await socket.BindServiceNameAsync(port, NetworkInformation.GetInternetConnectionProfile().NetworkAdapter); // Listen on desired port
            socket.JoinMulticastGroup(multicastIP); // Join socket to the multicast IP address
            socketIsConnected = true;
            return socket;

        }

        #endregion

        #region MULTICAST MESSAGE RECEIVED LISTENER
        private async void SocketOnMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            Conversation WasSelected = null;
            var myDisp = CoreApplication.MainView.CoreWindow.Dispatcher;
            await myDisp.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (recentMessageList.SelectedItem != null)
                    WasSelected = recentMessageList.SelectedItem as Conversation;
            });

            var result = args.GetDataStream();
            var resultStream = result.AsStreamForRead(1024);

            using (var reader = new StreamReader(resultStream))
            {
                string encryptedString = await reader.ReadToEndAsync();
                string text = StringCipher.Decrypt(encryptedString);

                if (text.StartsWith("trending"))
                {
                    string[] splitted = text.Split(new char[] { '~', '|', '*' });
                    string senderUsername = splitted[3];
                    string trendingMessage = splitted[6];

                    User trendingUser = null;
                    Message rMessage = new Message() { Type = Message.MessageType.Text, ReceivedSide = true, Text = trendingMessage, UnRead = true };

                    var disp = CoreApplication.MainView.CoreWindow.Dispatcher;
                    await disp.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        foreach (User u in ChatScreen.availableUsers)
                        {
                            if (u.Username.Equals(senderUsername))
                                trendingUser = u;
                        }
                        if (trendingUser == null)
                            return; // change this to request the user later

                        if (trendingUser.Username.Equals(ChatScreen.loggedInUser.Username))
                            return;

                        rMessage.MsgUser = trendingUser;
                        foreach (Conversation c in ChatScreen.recentMessages)
                        {
                            if (c.MessageUser.Username.Equals("trending"))
                            {
                                if (c.LastMessage.MsgUser != null)
                                {
                                    if (c.LastMessage.Text.Equals(rMessage.Text) && c.LastMessage.MsgUser.Username.Equals(trendingUser.Username))
                                        return;
                                }

                                if (WasSelected == c)
                                    rMessage.UnRead = false;
                                c.Messages.Add(rMessage);
                                c.LastMessage = rMessage;
                                if (ChatScreen.recentMessages.IndexOf(c) != 0)
                                {
                                    ChatScreen.recentMessages.Move(ChatScreen.recentMessages.IndexOf(c), 0);
                                }

                                if (WasSelected != null)
                                    recentMessageList.SelectedItem = WasSelected;
                                messageSound.Play();
                                StoreConversations();
                                return;
                            }
                        }


                    });

                    return;
                }


                User incomingUser = getUser(text);

                //return if the user is same as the logged-in user                
                if (incomingUser.Username.Equals(ChatScreen.loggedInUser.Username))
                    return;
                

                foreach (User u in ChatScreen.blockedUsers.ToList())
                {
                    if (u.Username.Equals(incomingUser.Username))
                        return;
                }

                foreach (User u in ChatScreen.availableUsers.ToList())
                {
                    if (u.Username.Equals(incomingUser.Username))
                    {
                        var d = CoreApplication.MainView.CoreWindow.Dispatcher;
                        await d.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            //ChatScreen.availableUsers.Remove(u);
                            u.CurrentStatus = incomingUser.CurrentStatus;
                            u.Name = incomingUser.Name;
                            u.Email = incomingUser.Email;
                            u.IPAddress = incomingUser.IPAddress;

                            if (incomingUser.CurrentStatus == User.Status.Offline)
                                ChatScreen.availableUsers.Remove(u);
                        });
                        if (getAck(text))
                        {
                            MulticastPacket p = new MulticastPacket(ChatScreen.loggedInUser, false);
                            await SendMessage(Parse(p), multicastPort);
                            if (!ChatScreen.loggedInUser.ProfilePicture.Equals("ms-appx:///Assets/default_profile_picture.png"))
                                await unicast.SendProfilePicture(incomingUser);
                            await unicast.RequestPicture(incomingUser);
                        }
                        return;
                    }
                }

                var dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;

                await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    ChatScreen.availableUsers.Add(incomingUser);
                    foreach (User u in (ChatScreen.favouriteUsers).ToList())
                    {
                        if (u.Username.Equals(incomingUser.Username))
                            ChatScreen.availableUsers.Move(ChatScreen.availableUsers.IndexOf(incomingUser), 0);
                    }
                    
                });

                await UpdateMessageUser(incomingUser);

                if (getAck(text))
                {
                    MulticastPacket p = new MulticastPacket(ChatScreen.loggedInUser, false);
                    await SendMessage(Parse(p), multicastPort);
                    if (!ChatScreen.loggedInUser.ProfilePicture.Equals("ms-appx:///Assets/default_profile_picture.png"))
                        await unicast.SendProfilePicture(incomingUser);
                    await unicast.RequestPicture(incomingUser);
                }

            }
        }

        #endregion

        #region TO SEND MULTICAST MESSAGE
        private async Task SendMessage(string message, string port)
        {
            using (var stream = await socket.GetOutputStreamAsync(multicastIP, port.ToString()))
            {
                using (var writer = new DataWriter(stream))
                {
                    string encryptedMessage = StringCipher.Encrypt(message);
                    writer.WriteString(encryptedMessage);
                    await writer.StoreAsync();
                }
            }

            if(message.StartsWith("trending"))
                await Task.Delay(TimeSpan.FromSeconds(1));
            else
                await Task.Delay(TimeSpan.FromSeconds(3));

            using (var stream = await socket.GetOutputStreamAsync(new HostName(GetBroadcastAddress()), port.ToString()))
            {
                using (var writer = new DataWriter(stream))
                {
                    string encryptedMessage = StringCipher.Encrypt(message);
                    writer.WriteString(encryptedMessage);
                    await writer.StoreAsync();
                }
            }

            
        }

        #endregion

        #region TO SEND TRENDING MESSAGE
        public async Task SendTrending(Message msg)
        {
            string toSend = "trending~|*" + ChatScreen.loggedInUser.Username + "~|*" + msg.Text;
            await SendMessage(toSend, multicastPort);
        }
        #endregion

        #region MULTICAST UPDATED LOGGED-IN USER'S STATUS
        public async Task update()
        {
            MulticastPacket p = new MulticastPacket(ChatScreen.loggedInUser, true);
            await SendMessage(Parse(p), multicastPort);
        }

        #endregion

        #region SUPPORTING METHODS
        private string Parse(MulticastPacket p)
        {
            string s = "" + p.UserIP + ":" + p.Username + ":" + p.UserFullName + ":" + p.UserEmail + ":" + p.Status + ":" + p.Ack;
            return s;
        }

        private bool getAck(string b)
        {
            //string data = Encoding.Unicode.GetString(b, 0, b.Length);
            string[] array = b.Split(':');
            if (array[5].ToLower().Equals("true"))
                return true;
            else
                return false;
        }

        private User getUser(string b)
        {
            string[] array = b.Split(':');
            return new User() { IPAddress = array[0], Username = array[1], Name = array[2], Email = array[3], CurrentStatus = User.StringToStatus(array[4]) };
        }

        public void TearDownSocket()
        {
            if (socket != null)
            {
                socket.Dispose();
                socket = null;
            }

            socketIsConnected = false;
        }

        #endregion

        #region LOCAL STORAGE

        private async void SetUpLocalFolder()
        {
            localFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("CampusTalk", CreationCollisionOption.OpenIfExists);
        }

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
            // Serialize our Product class into a string             
            string jsonContents = JsonConvert.SerializeObject(GetRecentMessagesAsList(ChatScreen.recentMessages));
            StorageFile textFile = await localFolder.CreateFileAsync(ChatScreen.loggedInUser.Username + ".json",
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

        #endregion

        #region UPDATE MESSAGE USER

        private async Task UpdateMessageUser(User user)
        {
            var dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;

            await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                for (int k = 0; k < ChatScreen.recentMessages.Count; k++)
                {

                    if (ChatScreen.recentMessages[k] is GroupConversation)
                    {
                        UserCollection grpUsers = ((GroupConversation)ChatScreen.recentMessages[k]).GroupUsers;
                        for (int i = 0; i < grpUsers.Count; i++)
                        {
                            if (grpUsers[i].Username.Equals(user.Username))
                            {
                                grpUsers[i] = user;
                                break;
                            }
                        }
                        ChatScreen.recentMessages[k].MessageUser.CurrentStatus = ChatScreen.recentMessages[k].GetMessageUserStatus();
                    }
                    else
                    {
                        if (ChatScreen.recentMessages[k].MessageUser.Username.Equals(user.Username))
                        {
                            ChatScreen.recentMessages[k].MessageUser = user;
                            break;
                        }
                    }
                }
            });

        }

        #endregion

        #region IP and BROADCAST ADDRESSES
        public string GetBroadcastAddress()
        {
            var cidr = (int)(new HostName(GetCurrentIPAddress()).IPInformation.PrefixLength);

            string mask = "";
            string invertedMask = "";
            string broadcastBinary = "";

            for (int i = 0; i < cidr; i++)
            {
                mask += "1";
            }
            for (int i = 0; i < (32 - cidr); i++)
            {
                mask += "0";
            }


            for (int i = 0; i < mask.Length; i++)
            {
                if ((mask.ToCharArray())[i] == '1')
                    invertedMask += "0";
                else
                    invertedMask += "1";
            }

            string[] ipArray = GetCurrentIPAddress().Split('.');
            string ipBinary = Convert.ToString(Convert.ToInt32(ipArray[0], 10), 2).PadLeft(8, '0') + Convert.ToString(Convert.ToInt32(ipArray[1], 10), 2).PadLeft(8, '0') + Convert.ToString(Convert.ToInt32(ipArray[2], 10), 2).PadLeft(8, '0') + Convert.ToString(Convert.ToInt32(ipArray[3], 10), 2).PadLeft(8, '0');

            // IP (OR) INVERTED MASK = BROADCAST ADDRESS
            for (int i = 0; i < mask.Length; i++)
            {
                broadcastBinary += (Int32.Parse((ipBinary.ToCharArray())[i].ToString()) | Int32.Parse((invertedMask.ToCharArray())[i].ToString())).ToString();
            }

            string broadcastAddress = Convert.ToInt32(broadcastBinary.Substring(0, 8), 2) + "." + Convert.ToInt32(broadcastBinary.Substring(8, 8), 2) + "." + Convert.ToInt32(broadcastBinary.Substring(16, 8), 2) + "." + Convert.ToInt32(broadcastBinary.Substring(24, 8), 2);
            return broadcastAddress;

        }

        public string GetCurrentIPAddress()
        {
            var icp = NetworkInformation.GetInternetConnectionProfile();

            if (icp != null && icp.NetworkAdapter != null)
            {
                var hostname =
                    NetworkInformation.GetHostNames()
                        .SingleOrDefault(
                            hn =>
                            hn.IPInformation != null && hn.IPInformation.NetworkAdapter != null
                            && hn.IPInformation.NetworkAdapter.NetworkAdapterId
                            == icp.NetworkAdapter.NetworkAdapterId);

                if (hostname != null)
                {
                    // the ip address
                    return hostname.CanonicalName;
                }
            }

            return string.Empty;
        }

        #endregion

    }
}