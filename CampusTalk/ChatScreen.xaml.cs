#region IMPORTS
using CampusTalk.Collections;
using CampusTalk.Model;
using CampusTalk.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
#endregion

namespace CampusTalk
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>

    public sealed partial class ChatScreen : Page
    {

        #region CLASS ATTRIBUTES


        public static StorageFolder localFolder;
        public static StorageFolder profilePicturesFolder;

        MediaCapture _mediaCaptureManager;
        StorageFile _recordStorageFile;
        bool _recording;
        bool _suspended;
        bool _userRequestedRaw;
        bool _rawAudioSupported;

        DispatcherTimer recordingTimer;
        int recordingTimeCounter = 15;

        public static User loggedInUser;
        public static Conversation activeConversation;
        public static UserCollection availableUsers;
        public static ObservableCollection<Conversation> recentMessages;

        public static UserCollection favouriteUsers;
        public static UserCollection blockedUsers;

        User selectedUser;

        public static Multicast multicast;
        public static Unicast unicast;

        User clickedUser;

        UserCollection groupMessagePopupSelectedUsers;

        bool newMessageFlyoutOpen;
        User newMessageFlyoutOpenUser;

        string APP_DATA_URL = "http://campustalk.pk/db-admin/app_data.php";
        public static AppData applicationData = new AppData();

        #endregion

        #region CLASS CONSTRUCTOR
        public ChatScreen()
        {
            this.InitializeComponent();
            SizeChanged += ChatScreen_SizeChanged;

            try
            {
                if (NetworkInformation.GetInternetConnectionProfile() == null)
                {
                    new MessageDialog("Please connect to a network and restart the app.").ShowAsync();
                }
            }
            catch (Exception ex)
            {

            }
        }

        void ChatScreen_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width < 800)
            {
                mainGrid.ColumnDefinitions[0].Width = new GridLength(0);
                mainGrid.ColumnDefinitions[2].Width = new GridLength(0);
            }
            else if (e.NewSize.Width < 1024 && e.NewSize.Width > 800)
            {
                //mainGrid.ColumnDefinitions[0].Width = new GridLength(0);
                mainGrid.ColumnDefinitions[2].Width = new GridLength(0);
            }
            else
            {
                mainGrid.ColumnDefinitions[0].Width = new GridLength(340);
                mainGrid.ColumnDefinitions[2].Width = new GridLength(340);
            }
        }


        #endregion

        #region ON NAVIGATED TO
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {

            loading.Visibility = Windows.UI.Xaml.Visibility.Visible;

            Application.Current.Resuming += Current_Resuming;

            NetworkInformation.NetworkStatusChanged += NetworkInformation_NetworkStatusChanged;

            await SetUpLocalFolders();
            loggedInUser = e.Parameter as User;
            loggedInUser.CurrentStatus = User.Status.Online;
            loggedInUser.IPAddress = GetCurrentIPAddress();
            await SetProfilePicture();

            try
            {
                await GetRemoteAppData();
            }
            catch (Exception ex)
            {

            }

            activeConversation = new Conversation();
            availableUsers = new UserCollection();

            await LoadData();

            if (recentMessages == null)
                recentMessages = new ObservableCollection<Conversation>();
            if (favouriteUsers == null)
                favouriteUsers = new UserCollection();
            if (blockedUsers == null)
                blockedUsers = new UserCollection();

            await AddTrendingConvo();
            availableUsers.CollectionChanged += availableUsers_CollectionChanged;

            favouriteUsers.CollectionChanged += favouriteUsers_CollectionChanged;
            blockedUsers.CollectionChanged += blockedUsers_CollectionChanged;

            availableUsersList.DataContext = availableUsers;
            groupMessagePopupAvailableUserList.DataContext = availableUsers;
            loggedInUserActions.DataContext = loggedInUser;

            recentMessageList.DataContext = recentMessages;
                        
            MakeAllMessageUsersOffline();
            
            if (unicast == null)
                unicast = new Unicast(messagePop, buzzSound, BuzzAnimation, recentMessageList);

            if (multicast == null)
                multicast = new Multicast(unicast, messagePop, recentMessageList);

            await Task.Delay(TimeSpan.FromSeconds(5));

            if (availableUsers.Count == 0)
                noneOnlineText.Visibility = Windows.UI.Xaml.Visibility.Visible;


            await StoreUser();

            recordingTimer = new DispatcherTimer();
            recordingTimer.Interval = TimeSpan.FromSeconds(1);
            recordingTimer.Tick += recordingTimer_Tick;

            await InitializeAudioRecording();

            if (recentMessages.Count > 0)
                recentMessageList.SelectedIndex = 0;
            //await Dummy();
            
            loading.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

        }

        async void Current_Resuming(object sender, object e)
        {
            if (loggedInUser == null)
                return;

            loggedInUser.CurrentStatus = User.Status.Online;

            if (multicast != null)
            {
                await multicast.update();
                userStatus.SelectedIndex = 0;
            }
        }


        async void NetworkInformation_NetworkStatusChanged(object sender)
        {
            try
            {
                if (NetworkInformation.GetInternetConnectionProfile() == null)
                {
                    await new MessageDialog("Please connect to a network and try again.").ShowAsync();
                    Application.Current.Exit();
                }
            }
            catch (Exception ex)
            {

            }
        }


        #endregion

        #region RECENT CONVERSATIONS SELECTION CHANGED
        //Conversation Selected
        private void recentMessageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (recentMessageList.SelectedItem != null)
            {
                activeConversation.SelectedOpacity = 0.15;
                noConvoSelectedText.Visibility = Visibility.Collapsed;
                selectedUserDetails.Visibility = Visibility.Visible;
                activeConversation = (Conversation)e.AddedItems[0];
                selectedUserDetails.DataContext = activeConversation;
                conversationView.Visibility = Visibility.Visible;
                conversationView.DataContext = activeConversation.Messages;
                messageBox.IsReadOnly = false;
                messageBox.PlaceholderText = "Type a message ...";
                activeConversation.Messages.CollectionChanged += Messages_CollectionChanged;

                ConversationScrollView.Measure(ConversationScrollView.RenderSize);
                ConversationScrollView.ChangeView(0.0, ConversationScrollView.ScrollableHeight, 1, true);

                selectedUser = activeConversation.MessageUser;

                if (activeConversation.MessageUser.CurrentStatus == User.Status.Offline)
                {
                    messageBox.IsReadOnly = true;
                    messageBox.PlaceholderText = "User is offline";
                }

                activeConversation.SelectedOpacity = 0.0;
                if (activeConversation.LastMessage != null)
                    activeConversation.LastMessage.UnRead = false;
            }
            else
            {
                selectedUserDetails.Visibility = Visibility.Collapsed;
                conversationView.Visibility = Visibility.Collapsed;
                noConvoSelectedText.Visibility = Visibility.Visible;
                messageBox.IsReadOnly = true;
                messageBox.PlaceholderText = "";
                activeConversation.Messages.CollectionChanged += Messages_CollectionChanged;

                selectedUser = null;

                activeConversation.SelectedOpacity = 0.15;
            }

        }
        #endregion

        #region MESSAGES OF ACTIVE CONVERSATION CHANGES
        // Messages of active conversation changes
        void Messages_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ConversationScrollView.Measure(ConversationScrollView.RenderSize);
            ConversationScrollView.ChangeView(0.0, ConversationScrollView.ScrollableHeight, 1, false);

            if (recentMessages.IndexOf(activeConversation) != 0)
            {
                recentMessages.Move(recentMessages.IndexOf(activeConversation), 0);
                recentMessageList.SelectedItem = activeConversation;
            }

        }
        #endregion

        #region SELECTED USER'S ACTIONS
        // Selected User Details Button Tapped
        private void contactButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            selectedUserDetailsPopup.DataContext = selectedUser;
            if (selectedUser.IsGroupUser == true)
                selectedUserDetailsPopupActionButtons.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            selectedUserDetailsPopup.IsOpen = true;
        }
        #endregion

        #region SEND TEXT MESSAGE
        //Send Message (Button)
        private async void messageSendButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {

            if (messageBox.Text.Length > 0)
            {
                if (activeConversation.MessageUser.Username.Equals("trending"))
                {
                    Message msgT = new Message();
                    msgT.Type = Message.MessageType.Text;
                    msgT.Text = messageBox.Text;
                    msgT.SentSide = true;
                    msgT.MsgUser = loggedInUser;

                    activeConversation.Messages.Add(msgT);
                    activeConversation.LastMessage = msgT;

                    messageBox.Text = "";
                    messagePop.Play();

                    await multicast.SendTrending(msgT);
                    await StoreConversations();
                    return;
                }

                Message msg = new Message();
                msg.Type = Message.MessageType.Text;
                msg.Text = messageBox.Text;
                msg.SentSide = true;
                if (activeConversation is GroupConversation)
                {
                    msg.IsGroupMessage = true;
                    msg.SentToGroupUsers = "Sent to: ";
                    foreach (User u in ((GroupConversation)activeConversation).GroupUsers)
                    {
                        if (u.CurrentStatus == User.Status.Online || u.CurrentStatus == User.Status.Busy)
                        {
                            msg.SentToGroupUsers += u.Username;
                            if (u != ((GroupConversation)activeConversation).GroupUsers.Last())
                                msg.SentToGroupUsers += ", ";
                        }
                    }
                }

                activeConversation.Messages.Add(msg);
                activeConversation.LastMessage = msg;

                messageBox.Text = "";
                messagePop.Play();

                if (msg.IsGroupMessage == true)
                    await unicast.SendTextMessageToGroup(((GroupConversation)activeConversation).GroupUsers.ToList(), msg);
                else
                    await unicast.SendTextMessage(activeConversation.MessageUser, msg);

                await StoreConversations();
            }

        }

        //Send Message (Enter Key)
        private void messageBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                messageSendButtonMask_Tapped(sender, new TappedRoutedEventArgs());
            }
        }
        #endregion

        #region SEND ATTACHMENT MESSAGE
        // Attachment Button Tapped
        private async void attachmentsButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {

            StorageFile file = null;
            if (selectedUser != null)
            {
                if (selectedUser.Username.Equals("trending"))
                    return;

                if (selectedUser.CurrentStatus != User.Status.Offline)
                {
                    file = await SelectFile();
                    if (file == null)
                        return;

                    StorageFile temp = await file.CopyAsync(profilePicturesFolder, file.DisplayName + file.FileType, NameCollisionOption.GenerateUniqueName);

                    Message msg = new Message() { SentSide = true, Type = Message.MessageType.Attachment, MsgUser = loggedInUser, Attachment = temp.Path };

                    if (await msg.IsImage())
                    {
                        msg.Text = "You sent a photo";
                        msg.IsImageProp = true;
                    }
                    else
                        msg.Text = "Attachment: " + file.DisplayName;

                    activeConversation.Messages.Add(msg);
                    activeConversation.LastMessage = msg;
                }
            }

            if (file != null)
            {
                if (activeConversation is GroupConversation)
                    await unicast.SendAttachmentToGroup(((GroupConversation)activeConversation).GroupUsers.ToList(), file);
                else
                    await unicast.SendAttachment(selectedUser, file);

                messagePop.Play();

                await StoreConversations();
            }
        }

        // Open file picker to select file
        private async System.Threading.Tasks.Task<StorageFile> SelectFile()
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            openPicker.FileTypeFilter.Add("*");
            openPicker.CommitButtonText = "Send";

            return await openPicker.PickSingleFileAsync();
        }
        #endregion

        #region LIST OF AVAILABLE USERS CHANGES
        // List of available users changes
        async void availableUsers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {

            await Task.Delay(TimeSpan.FromSeconds(1));

            if (recentMessageList.SelectedItem != null)
            {
                if (activeConversation.MessageUser.CurrentStatus == User.Status.Offline)
                {
                    messageBox.IsReadOnly = true;
                    messageBox.PlaceholderText = "User is offline";
                }
                else
                {
                    messageBox.IsReadOnly = false;
                    messageBox.PlaceholderText = "Type a message ...";
                }
            }


            if (availableUsers.Count == 0)
            {
                noneOnlineText.Text = "No one's available :(";
                noneOnlineText.Visibility = Windows.UI.Xaml.Visibility.Visible;
                foreach (Conversation c in recentMessages)
                {
                    if (c.MessageUser.Username.Equals("trending"))
                    {
                        c.MessageUser.CurrentStatus = User.Status.Offline;
                        break;
                    }
                }
            }
            else
            {
                noneOnlineText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                foreach (Conversation c in recentMessages)
                {
                    if (c.MessageUser.Username.Equals("trending"))
                    {
                        c.MessageUser.CurrentStatus = User.Status.Online;
                        break;
                    }
                }
            }


        }
        #endregion

        #region AVAILABLE USERS FILTER BOX TEXT CHANGED
        //Filter Available Users
        private void availableUsersFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox filterBox = (TextBox)sender;

            if (filterBox.Text != "")
            {
                UserCollection filteredList = new UserCollection();
                availableUsersList.DataContext = filteredList;

                foreach (User u in availableUsers)
                {
                    string name = u.Name.ToLower();
                    if (name.Contains(filterBox.Text.ToLower()))
                        filteredList.Add(u);
                }

                if (filteredList.Count == 0)
                {
                    noneOnlineText.Text = "No matches";
                    noneOnlineText.Visibility = Windows.UI.Xaml.Visibility.Visible;
                }
                else
                {
                    noneOnlineText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
            }
            else
            {
                availableUsersList.DataContext = availableUsers;
                noneOnlineText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }
        #endregion

        #region AVAILABLE USERS LIST ITEM CLICKED
        //Available Users List Item Clicked
        private void availableUsersList_ItemClick(object sender, ItemClickEventArgs e)
        {
            clickedUser = (User)e.ClickedItem;
            selectedUserDetailsPopup.DataContext = clickedUser;
            selectedUserDetailsPopup.IsOpen = true;
        }
        #endregion

        #region START BY ALL MESSAGE USERS OFFLINE
        private void MakeAllMessageUsersOffline()
        {
            foreach (Conversation c in recentMessages)
            {
                if (c is GroupConversation)
                {
                    foreach (User u in ((GroupConversation)c).GroupUsers)
                    {
                        u.CurrentStatus = User.Status.Offline;
                    }

                    c.MessageUser.CurrentStatus = c.GetMessageUserStatus();
                }
                else
                {
                    c.MessageUser.CurrentStatus = User.Status.Offline;
                }
            }
        }

        #endregion

        #region WHEN LOGGED IN USER CHANGES HIS STATUS
        // When logged-in user changes his status
        private async void userStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (applicationData.Multicast_ip == null)
                await GetRemoteAppData();

            string status = (string)e.AddedItems.Single();
            if (loggedInUser.ProfilePicture.Equals("ms-appx:///Assets/default_profile_picture.png"))
                await SetProfilePicture();

            if (unicast == null)
                unicast = new Unicast(messagePop, buzzSound, BuzzAnimation, recentMessageList);

            if (multicast == null)
            {
                multicast = new Multicast(unicast, messagePop, recentMessageList);
                return;
            }

            switch (status)
            {
                case "Online":
                    loggedInUser.CurrentStatus = User.Status.Online;
                    await multicast.update();
                    break;
                case "Busy":
                    loggedInUser.CurrentStatus = User.Status.Busy;
                    await multicast.update();
                    break;
            }
        }
        #endregion

        #region USER DETAILS POPUP
        // Selected/Clicked User Details Popup Opened
        private void selectedUserDetailsPopup_Opened(object sender, object e)
        {
            opacityOver.Visibility = Windows.UI.Xaml.Visibility.Visible;
            groupMessagePopupFilterBox.Text = "";
            groupMessagePopupAvailableUserList.SelectedItems.Clear();
            groupMessagePopupSelectedUsers = new UserCollection();
            groupMessagePopupAvailableUserList.DataContext = availableUsers;

            newMessageFlyoutOpenUser = null;
            newMessageFlyoutUserBox.IsReadOnly = false;
            newMessageFlyoutUserBox.Text = "";
            newMessageFlyoutMessageBox.Text = "";
            newMessageFlyoutList.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            newMessageFlyoutMessageBox.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            newMessageFlyoutActionButtons.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            trendingBox.Text = "";

            recordingTimeCounter = 15;
            recordingTimerText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            voiceRecordButtonGrid.Visibility = Windows.UI.Xaml.Visibility.Visible;
            stopButtonGrid.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            sendAudioButtonGrid.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            recordingProgressRing.IsActive = false;
            _recordStorageFile = null;
        }

        // Selected/Clicked User Details Popup Closed
        private void selectedUserDetailsPopup_Closed(object sender, object e)
        {
            opacityOver.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            selectedUserDetailsPopupActionButtons.Visibility = Windows.UI.Xaml.Visibility.Visible;

            newMessageFlyoutOpen = false;

        }

        // Selected/Cliked User Details Popup New Message Button Tapped
        private void popupNewMessageButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            selectedUserDetailsPopup.IsOpen = false;

            User u = (User)selectedUserDetailsPopup.DataContext;

            foreach (Conversation c in recentMessages)
            {
                if (c.MessageUser.Username.Equals(u.Username))
                {
                    recentMessageList.SelectedItem = c;
                    return;
                }
            }

            Conversation con = new Conversation() { MessageUser = u };
            recentMessages.Add(con);
            recentMessages.Move(recentMessages.IndexOf(con), 0);
            recentMessageList.SelectedItem = con;
            messageBox.Focus(FocusState.Programmatic);
        }

        private async void popupFavButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            User usr = (User)selectedUserDetailsPopup.DataContext;
            bool isAlreadyThere = false;
            foreach (User u in favouriteUsers)
            {
                if (u.Username.Equals(usr.Username))
                {
                    isAlreadyThere = true;
                    break;
                }
            }
            if (isAlreadyThere)
            {
                await new MessageDialog("" + usr.Name + "(" + usr.Username + ") is already in your favourites.").ShowAsync();
            }
            else
            {
                favouriteUsers.Add(usr);
                await new MessageDialog("" + usr.Name + "(" + usr.Username + ") has been added to favourites.\nTo revert goto Settings > Favourites.").ShowAsync();
                await StoreFavourites();
            }

        }

        private void popupBlockButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            User usr = (User)selectedUserDetailsPopup.DataContext;
            confirmBlockUserText.Text = "Are you sure you want to block " + usr.Name + "(" + usr.Username + ") ?";
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }


        private async void confirmBlockUserButton_Click(object sender, RoutedEventArgs e)
        {
            User usr = (User)selectedUserDetailsPopup.DataContext;
            blockedUsers.Add(usr);
            await new MessageDialog("You just blocked " + usr.Name + "(" + usr.Username + ").\nTo revert goto Settings > Blocked Users.").ShowAsync();
            await StoreBlocked();
        }


        private void TextBlock_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Message m = ((TextBlock)(sender)).DataContext as Message;
            selectedUserDetailsPopup.DataContext = m.MsgUser;
            selectedUserDetailsPopup.IsOpen = true;
        }

        #endregion

        #region SET LOGGED IN USER'S PROFILE PICTURE
        // Set logged-in user's profile picture
        private async Task SetProfilePicture()
        {
            StorageFile default_pp = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/default_profile_picture.png"));

            // Set query options with filter and sort order for results
            List<string> fileTypeFilter = new List<string>();
            fileTypeFilter.Add(".jpg");
            fileTypeFilter.Add(".jpeg");
            fileTypeFilter.Add(".png");
            var queryOptions = new QueryOptions(CommonFileQuery.OrderByName, fileTypeFilter);
            queryOptions.UserSearchFilter = loggedInUser.Username;
            // Create query and retrieve files
            var query = localFolder.CreateFileQueryWithOptions(queryOptions);
            IReadOnlyList<StorageFile> fileList = await query.GetFilesAsync();
            if (fileList.Count != 0)
            {
                if (fileList.First().DisplayName.Equals(loggedInUser.Username))
                {
                    loggedInUser.ProfilePicture = fileList.First().Path;
                    return;
                }
            }

            //loggedInUser.ProfilePicture = default_pp;

        }
        #endregion

        #region SAVE RECEIVED ATTACHMENT
        // Save Received Attachment Button Clicked
        private async void attachmentReceivedSaveButton_Click(object sender, RoutedEventArgs e)
        {
            StorageFile fileToSave = await StorageFile.GetFileFromPathAsync(((Message)(((Button)sender).DataContext)).Attachment);

            if (fileToSave != null)
            {
                FolderPicker folderPicker = new FolderPicker();
                folderPicker.SuggestedStartLocation = PickerLocationId.Downloads;
                folderPicker.FileTypeFilter.Add("*");
                folderPicker.CommitButtonText = "Save";

                StorageFolder folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    // Application now has read/write access to all contents in the picked folder (including other sub-folder contents)
                    StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);
                    await fileToSave.CopyAsync(folder, fileToSave.DisplayName + fileToSave.FileType, NameCollisionOption.GenerateUniqueName);
                }
                else
                {
                    return;
                }
            }
        }
        #endregion

        #region PHOTO VIEWER POPUP
        // Received photo is tapped
        private void MessagePhotoTapped(object sender, TappedRoutedEventArgs e)
        {
            Message msg = ((Message)(((Image)sender).DataContext));
            photoViwerPopup.DataContext = msg.Attachment;
            photoViwerPopup.IsOpen = true;
        }

        // photoviewer popup is tapped
        private void PhotoViewerImageTapped(object sender, TappedRoutedEventArgs e)
        {
            photoViwerPopup.IsOpen = false;
        }

        // recent messages selecteed user's photo is tapped
        private void SelectedUserPhotoTapped(object sender, TappedRoutedEventArgs e)
        {
            Conversation c = ((Ellipse)sender).DataContext as Conversation;
            if (c.MessageUser.ProfilePicture == null || c.MessageUser.IsGroupUser == true)
                return;
            photoViwerPopup.DataContext = c.MessageUser.ProfilePicture;
            photoViwerPopup.IsOpen = true;
        }

        // logged in user's photo is tapped
        private void LoggedInUserPhotoTapped(object sender, TappedRoutedEventArgs e)
        {
            User u = ((Ellipse)sender).DataContext as User;
            if (u.ProfilePicture == null)
                return;
            photoViwerPopup.DataContext = u.ProfilePicture;
            photoViwerPopup.IsOpen = true;
        }

        // user details popup photo is tapped
        private void UserDetailsPhotoTapped(object sender, TappedRoutedEventArgs e)
        {
            User u = ((Ellipse)sender).DataContext as User;
            if (u.ProfilePicture == null || u.IsGroupUser == true)
                return;
            selectedUserDetailsPopup.IsOpen = false;
            photoViwerPopup.DataContext = u.ProfilePicture;
            photoViwerPopup.IsOpen = true;
        }

        #endregion

        #region NEW GROUP MESSAGE
        // new group message popup's filter text is changed
        private void groupMessagePopupFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox filterBox = (TextBox)sender;

            UserCollection filteredList = new UserCollection();
            groupMessagePopupAvailableUserList.DataContext = filteredList;

            foreach (User u in availableUsers)
            {
                string name = u.Name.ToLower();
                if (name.Contains(filterBox.Text.ToLower()))
                {
                    filteredList.Add(u);
                    if (groupMessagePopupSelectedUsers.Contains(u))
                        groupMessagePopupAvailableUserList.SelectedItems.Add(u);
                }
            }

        }

        // new group message popup's add selected users button is clicked
        private async void groupMessagePopupAddButton_Click(object sender, RoutedEventArgs e)
        {
            if (groupMessagePopupSelectedUsers.Count == 0)
                return;

            if (groupMessagePopupSelectedUsers.Count == 1)
            {
                await new MessageDialog("Please select two or more users to start a group conversation.").ShowAsync();
                return;
            }

            string displayName = "";
            string userNames = "";
            string emails = "";
            StorageFile pp = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/default_group_profile_picture.png"));
            UserCollection grpUsrs = new UserCollection();
            foreach (User u in groupMessagePopupSelectedUsers)
            {
                displayName += u.Name;
                userNames += u.Username;
                emails += u.Email;

                if (groupMessagePopupSelectedUsers.Last() != u)
                {
                    displayName += ", ";
                    userNames += ", ";
                    emails += ", ";
                }

                grpUsrs.Add(u);
            }

            GroupConversation groupConvo = new GroupConversation() { MessageUser = new User() { Name = displayName, ProfilePicture = pp.Path, CurrentStatus = User.Status.Online, Username = userNames, Email = emails, IsGroupUser = true }, GroupUsers = grpUsrs };

            foreach (Conversation c in recentMessages)
            {
                if (c is GroupConversation)
                {
                    int count = 0;

                    foreach (User usr in groupMessagePopupSelectedUsers)
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

                    if ((count == ((GroupConversation)c).GroupUsers.Count) && (count == groupMessagePopupSelectedUsers.Count))
                    {
                        recentMessageList.SelectedItem = c;
                        FlyoutBase.GetAttachedFlyout((FrameworkElement)newGroupButtonMask).Hide();
                        return;
                    }

                }

            }

            recentMessages.Add(groupConvo);
            recentMessages.Move(recentMessages.IndexOf(groupConvo), 0);
            recentMessageList.SelectedItem = groupConvo;

            FlyoutBase.GetAttachedFlyout((FrameworkElement)newGroupButtonMask).Hide();
        }

        // new group message button is tapped
        private void newGroupButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }

        // new group message popup's user list's selection is changed
        private void groupMessagePopupAvailableUserList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (User u in groupMessagePopupAvailableUserList.SelectedItems)
            {
                if (!groupMessagePopupSelectedUsers.Contains(u))
                    groupMessagePopupSelectedUsers.Add(u);
            }

            foreach (User u in groupMessagePopupAvailableUserList.Items)
            {
                if (!groupMessagePopupAvailableUserList.SelectedItems.Contains(u) && groupMessagePopupSelectedUsers.Contains(u))
                    groupMessagePopupSelectedUsers.Remove(u);
            }

            groupMessagePopupFilterBox.Focus(FocusState.Keyboard);
        }
        #endregion

        #region GET DEVICE'S IP ADDRESS
        //Get current IP Address
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

        #region SET UP LOCAL AND TEMPORARY FOLDERS
        // Set up local storage folder
        private async Task SetUpLocalFolders()
        {
            localFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("CampusTalk", CreationCollisionOption.OpenIfExists);
            profilePicturesFolder = ApplicationData.Current.TemporaryFolder;
        }

        #endregion

        #region BUZZ
        // BUZZ BUTTON TAPPED FROM SELECTED USER ACTIONS
        private async void buzzButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (activeConversation.MessageUser.Username.Equals("trending"))
            {
                await new MessageDialog("Can't Buzz all!").ShowAsync();
                return;
            }

            Message msg = new Message() { Type = Message.MessageType.Buzz, Text = "You sent a buzz!", SentSide = true, MsgUser = selectedUser };

            if (activeConversation is GroupConversation)
            {
                msg.IsGroupMessage = true;
                msg.SentToGroupUsers = "Sent to: ";
                foreach (User u in ((GroupConversation)activeConversation).GroupUsers)
                {
                    if (u.CurrentStatus == User.Status.Online || u.CurrentStatus == User.Status.Busy)
                    {
                        msg.SentToGroupUsers += u.Username;
                        if (u != ((GroupConversation)activeConversation).GroupUsers.Last())
                            msg.SentToGroupUsers += ", ";
                    }
                }
            }
            activeConversation.Messages.Add(msg);
            activeConversation.LastMessage = msg;

            if (msg.IsGroupMessage == true)
                await unicast.BuzzToGroup(((GroupConversation)activeConversation).GroupUsers.ToList());
            else
                await unicast.Buzz(selectedUser);
            BuzzAnimation.Begin();
            buzzSound.Play();
        }

        // BUZZ BUTTON TAPPED FROM USER DETAILS POPUP
        private async void popupBuzzButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            selectedUserDetailsPopup.IsOpen = false;

            User u = (User)selectedUserDetailsPopup.DataContext;
            Message msg = new Message() { Type = Message.MessageType.Buzz, Text = "You sent a buzz!", SentSide = true, MsgUser = u };

            foreach (Conversation c in recentMessages)
            {
                if (c.MessageUser.Username.Equals(u.Username))
                {
                    recentMessageList.SelectedItem = c;
                    activeConversation.Messages.Add(msg);
                    activeConversation.LastMessage = msg;
                    await unicast.Buzz(msg.MsgUser);
                    BuzzAnimation.Begin();
                    buzzSound.Play();
                    return;
                }
            }

            Conversation con = new Conversation() { MessageUser = u };
            recentMessages.Add(con);
            recentMessages.Move(recentMessages.IndexOf(con), 0);
            recentMessageList.SelectedItem = con;
            activeConversation.Messages.Add(msg);
            activeConversation.LastMessage = msg;
            await unicast.Buzz(msg.MsgUser);
            BuzzAnimation.Begin();
            buzzSound.Play();
        }

        #endregion

        #region NEW MESSAGE FLYOUT

        private void newMessageButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)newMessageButtonMask);
        }

        private void newMessageFlyoutUserBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox filterBox = (TextBox)sender;

            if (filterBox.IsReadOnly == true)
                return;

            UserCollection filteredList = new UserCollection();
            if (filterBox.Text != "")
            {
                newMessageFlyoutList.DataContext = filteredList;

                foreach (User u in availableUsers)
                {
                    string name = u.Name.ToLower();
                    if (name.Contains(filterBox.Text.ToLower()))
                        filteredList.Add(u);
                }

                if (filteredList.Count != 0)
                {
                    newMessageFlyoutList.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    newMessageFlyoutUserBox.Focus(FocusState.Programmatic);
                }
                else
                {
                    newMessageFlyoutList.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    newMessageFlyoutUserBox.Focus(FocusState.Programmatic);
                }
            }
            else
            {
                newMessageFlyoutList.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        private void newMessageFlyoutList_ItemClick(object sender, ItemClickEventArgs e)
        {
            string username = ((User)e.ClickedItem).Username;

            foreach (User u in availableUsers)
            {
                if (u.Username.Equals(username))
                    newMessageFlyoutOpenUser = u;
            }

            newMessageFlyoutUserBox.Text = newMessageFlyoutOpenUser.Name;
            newMessageFlyoutUserBox.IsReadOnly = true;

            newMessageFlyoutList.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            newMessageFlyoutMessageBox.Visibility = Windows.UI.Xaml.Visibility.Visible;
            newMessageFlyoutActionButtons.Visibility = Windows.UI.Xaml.Visibility.Visible;
        }

        private async void messageSendPopupButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (newMessageFlyoutMessageBox.Text.Length > 0)
            {
                Message msg = new Message();
                msg.Type = Message.MessageType.Text;
                msg.Text = newMessageFlyoutMessageBox.Text;
                msg.SentSide = true;

                foreach (Conversation c in recentMessages)
                {
                    if (c.MessageUser.Username.Equals(newMessageFlyoutOpenUser.Username))
                    {
                        c.Messages.Add(msg);
                        c.LastMessage = msg;
                        if (recentMessages.IndexOf(c) != 0)
                            recentMessages.Move(recentMessages.IndexOf(c), 0);
                        recentMessageList.SelectedItem = c;
                        messagePop.Play();
                        await unicast.SendTextMessage(newMessageFlyoutOpenUser, msg);
                        FlyoutBase.GetAttachedFlyout((FrameworkElement)newMessageButtonMask).Hide();
                        await StoreConversations();
                        return;
                    }
                }

                Conversation newConvo = new Conversation() { MessageUser = newMessageFlyoutOpenUser };
                newConvo.Messages.Add(msg);
                newConvo.LastMessage = msg;
                recentMessages.Add(newConvo);
                recentMessages.Move(recentMessages.IndexOf(newConvo), 0);
                recentMessageList.SelectedItem = newConvo;

                messagePop.Play();

                await unicast.SendTextMessage(newMessageFlyoutOpenUser, msg);

                FlyoutBase.GetAttachedFlyout((FrameworkElement)newMessageButtonMask).Hide();

                await StoreConversations();

            }
        }

        private async void attachmentsPopupButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            StorageFile file = null;
            if (newMessageFlyoutOpenUser != null)
            {
                if (newMessageFlyoutOpenUser.CurrentStatus != User.Status.Offline)
                {
                    file = await SelectFile();
                    if (file == null)
                        return;

                    StorageFile temp = await file.CopyAsync(profilePicturesFolder, file.DisplayName + file.FileType, NameCollisionOption.GenerateUniqueName);

                    Message msg = new Message() { SentSide = true, Type = Message.MessageType.Attachment, MsgUser = loggedInUser, Attachment = temp.Path };

                    if (await msg.IsImage())
                    {
                        msg.Text = "You sent a photo";
                        msg.IsImageProp = true;
                    }
                    else
                        msg.Text = "Attachment: " + file.DisplayName;

                    foreach (Conversation c in recentMessages)
                    {
                        if (c.MessageUser.Username.Equals(newMessageFlyoutOpenUser.Username))
                        {
                            c.Messages.Add(msg);
                            c.LastMessage = msg;
                            if (recentMessages.IndexOf(c) != 0)
                                recentMessages.Move(recentMessages.IndexOf(c), 0);
                            recentMessageList.SelectedItem = c;
                            messagePop.Play();
                            if (file != null)
                                await unicast.SendAttachment(newMessageFlyoutOpenUser, file);
                            FlyoutBase.GetAttachedFlyout((FrameworkElement)newMessageButtonMask).Hide();
                            StoreConversations();
                            return;
                        }
                    }

                    Conversation newConvo = new Conversation() { MessageUser = newMessageFlyoutOpenUser };
                    newConvo.Messages.Add(msg);
                    newConvo.LastMessage = msg;
                    recentMessages.Add(newConvo);
                    recentMessages.Move(recentMessages.IndexOf(newConvo), 0);
                    recentMessageList.SelectedItem = newConvo;

                    if (file != null)
                    {
                        messagePop.Play();
                        await unicast.SendAttachment(newMessageFlyoutOpenUser, file);
                        StoreConversations();
                    }

                    FlyoutBase.GetAttachedFlyout((FrameworkElement)newMessageButtonMask).Hide();
                }
            }


        }

        #endregion

        #region FAVOURITE AND BLOCKED COLLECTION CHANGED

        async void favouriteUsers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                User addedUser = (User)e.NewItems[0];

                foreach (User u in availableUsers.ToList())
                {
                    if (u.Username.Equals(addedUser.Username))
                        availableUsers.Move(availableUsers.IndexOf(u), 0);
                }

                await StoreFavourites();
            }

        }

        async void blockedUsers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                User justBlocked = (User)e.NewItems[0];

                foreach (Conversation c in recentMessages.ToList())
                {
                    if (c.MessageUser.Username.Equals(justBlocked.Username))
                        recentMessages.Remove(c);
                }

                foreach (User u in availableUsers.ToList())
                {
                    if (u.Username.Equals(justBlocked.Username))
                        availableUsers.Remove(u);
                }

                foreach (User u in favouriteUsers.ToList())
                {
                    if (u.Username.Equals(justBlocked.Username))
                        favouriteUsers.Remove(u);
                }

                if (multicast == null)
                    multicast = new Multicast(unicast, messagePop, recentMessageList);

                await multicast.update();
                await StoreFavourites();
            }
        }

        #endregion

        #region DELETE CONVERSATION

        private async void confirmConvoDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            FlyoutBase.GetAttachedFlyout((FrameworkElement)convoDeleteButtonMask).Hide();

            if (activeConversation.MessageUser.Username.Equals("trending"))
            {
                activeConversation.Messages.Clear();
                activeConversation.LastMessage = new Message();
                await StoreConversations();
                return;
            }
            recentMessages.Remove(activeConversation);
            if (recentMessages.Count > 0)
                recentMessageList.SelectedIndex = 0;
            await StoreConversations();
        }

        Ellipse convoDeleteButtonMask;

        private async void deleteConvoButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {

            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
            convoDeleteButtonMask = (Ellipse)sender;
        }

        #endregion

        #region SETTINGS BUTTON
        private async void settingsButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            MySettingsFlyout sf = new MySettingsFlyout();
            sf.ShowIndependent();

        }
        #endregion

        #region TRENDING

        private void trendingBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                trendingSendButtonMask_Tapped(sender, new TappedRoutedEventArgs());
            }
        }

        private async void trendingSendButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (trendingBox.Text != "")
            {
                FlyoutBase.GetAttachedFlyout((FrameworkElement)TrendingButtonMask).Hide();
                foreach (Conversation c in recentMessages)
                {
                    if (c.MessageUser.Username.Equals("trending"))
                    {
                        recentMessageList.SelectedItem = c;
                        break;
                    }
                }

                if (activeConversation.MessageUser.CurrentStatus != User.Status.Offline)
                {
                    Message msg = new Message();
                    msg.Type = Message.MessageType.Text;
                    msg.Text = trendingBox.Text;
                    msg.SentSide = true;
                    msg.MsgUser = loggedInUser;

                    activeConversation.Messages.Add(msg);
                    activeConversation.LastMessage = msg;

                    messagePop.Play();

                    await multicast.SendTrending(msg);

                    await StoreConversations();

                }
            }
        }

        Ellipse TrendingButtonMask;
        private void trendingButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            TrendingButtonMask = sender as Ellipse;
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)TrendingButtonMask);
            trendingBox.Focus(FocusState.Programmatic);
        }

        private async Task AddTrendingConvo()
        {
            Conversation trendingConvo = new Conversation() { MessageUser = new User() { Username = "trending", Name = "What's trending?", Email = "trending@campustalk.pk", CurrentStatus = User.Status.Offline, IsGroupUser = true, ProfilePicture = "ms-appx:///Assets/default_trending_profile_picture.png" } };
            bool b = false;

            foreach (Conversation c in recentMessages)
            {
                if (c.MessageUser.Username.Equals("trending"))
                {
                    b = true;
                    break;
                }
            }

            if (b == false)
            {
                recentMessages.Add(trendingConvo);
                await StoreConversations();
            }
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

        private ObservableCollection<Conversation> GetRecentListAsObservableCollection(List<JsonConversation> convos)
        {
            ObservableCollection<Conversation> result = new ObservableCollection<Conversation>();
            foreach (JsonConversation c in convos)
            {
                if (c.IsGroupConvo == true)
                {
                    UserCollection grpUsrs = new UserCollection();
                    foreach (User u in c.GroupUsers)
                    {
                        grpUsrs.Add(u);
                    }
                    result.Add(new GroupConversation() { LastMessage = c.LastMessage, Messages = new ObservableCollection<Message>(c.Messages), MessageUser = c.MessageUser, SelectedOpacity = c.SelectedOpacity, GroupUsers = grpUsrs });
                }
                else
                    result.Add(new Conversation() { LastMessage = c.LastMessage, Messages = new ObservableCollection<Message>(c.Messages), MessageUser = c.MessageUser, SelectedOpacity = c.SelectedOpacity });
            }

            return result;
        }

        private async Task StoreConversations()
        {
            try
            {

                // Serialize our Product class into a string             
                string jsonContents = JsonConvert.SerializeObject(GetRecentMessagesAsList(recentMessages));
                StorageFile textFile = await localFolder.CreateFileAsync(loggedInUser.Username + ".json",
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

        private async Task LoadStoredConversations()
        {
            Conversation trendingConvo = new Conversation() { MessageUser = new User() { Username = "trending", Name = "What's trending?", Email = "trending@campustalk.pk", CurrentStatus = User.Status.Offline, IsGroupUser = true, ProfilePicture = "ms-appx:///Assets/default_trending_profile_picture.png" } };

            try
            {
                // Getting JSON from file if it exists, or file not found exception if it does not  
                StorageFile textFile = await localFolder.GetFileAsync(loggedInUser.Username + ".json");
                using (IRandomAccessStream textStream = await textFile.OpenReadAsync())
                {
                    // Read text stream     
                    using (DataReader textReader = new DataReader(textStream))
                    {
                        //get size                       
                        uint textLength = (uint)textStream.Size;
                        await textReader.LoadAsync(textLength);
                        // read it                    
                        string jsonContents = textReader.ReadString(textLength);
                        // deserialize back to our product!
                        var obj = JsonConvert.DeserializeObject<List<JsonConversation>>(jsonContents);
                        if (obj != null)
                            recentMessages = GetRecentListAsObservableCollection(obj);
                        else
                        {
                            recentMessages = new ObservableCollection<Conversation>();
                            recentMessages.Add(trendingConvo);
                        }
                    }
                }
            }
            catch (FileNotFoundException e)
            {
                recentMessages = new ObservableCollection<Conversation>();
                recentMessages.Add(trendingConvo);
            }

        }

        private async Task StoreFavourites()
        {
            // Serialize our Product class into a string             
            string jsonContents = JsonConvert.SerializeObject(favouriteUsers.ToList());
            StorageFile textFile = await localFolder.CreateFileAsync(loggedInUser.Username + "-favourites" + ".json",
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

        private async Task LoadStoredFavourites()
        {

            try
            {
                // Getting JSON from file if it exists, or file not found exception if it does not  
                StorageFile textFile = await localFolder.GetFileAsync(loggedInUser.Username + "-favourites" + ".json");
                using (IRandomAccessStream textStream = await textFile.OpenReadAsync())
                {
                    // Read text stream     
                    using (DataReader textReader = new DataReader(textStream))
                    {
                        //get size                       
                        uint textLength = (uint)textStream.Size;
                        await textReader.LoadAsync(textLength);
                        // read it                    
                        string jsonContents = textReader.ReadString(textLength);
                        // deserialize back to our product!  
                        var obj = JsonConvert.DeserializeObject<List<User>>(jsonContents);
                        if (obj != null)
                            favouriteUsers = new ObservableCollection<User>(obj) as UserCollection;
                        else
                            favouriteUsers = new UserCollection();
                    }
                }
            }
            catch (FileNotFoundException e)
            {
                favouriteUsers = new UserCollection();
            }

        }

        private async Task StoreBlocked()
        {
            // Serialize our Product class into a string             
            string jsonContents = JsonConvert.SerializeObject(blockedUsers.ToList());
            StorageFile textFile = await localFolder.CreateFileAsync(loggedInUser.Username + "-blocked" + ".json",
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

        private async Task LoadStoredBlocked()
        {

            try
            {
                // Getting JSON from file if it exists, or file not found exception if it does not  
                StorageFile textFile = await localFolder.GetFileAsync(loggedInUser.Username + "-blocked" + ".json");
                using (IRandomAccessStream textStream = await textFile.OpenReadAsync())
                {
                    // Read text stream     
                    using (DataReader textReader = new DataReader(textStream))
                    {
                        //get size                       
                        uint textLength = (uint)textStream.Size;
                        await textReader.LoadAsync(textLength);
                        // read it                    
                        string jsonContents = textReader.ReadString(textLength);
                        // deserialize back to our product!
                        var obj = JsonConvert.DeserializeObject<List<User>>(jsonContents);
                        if (obj != null)
                            blockedUsers = new ObservableCollection<User>(obj) as UserCollection;
                        else
                            blockedUsers = new UserCollection();

                    }
                }
            }
            catch (FileNotFoundException e)
            {
                blockedUsers = new UserCollection();
            }

        }

        private async Task LoadData()
        {
            await LoadStoredConversations();
            await LoadStoredFavourites();
            await LoadStoredBlocked();
        }

        private async Task StoreUser()
        {
            // Serialize our Product class into a string             
            string jsonContents = JsonConvert.SerializeObject(loggedInUser);
            StorageFile textFile = await localFolder.CreateFileAsync("logged_in_user.json",
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

        #region GET APP DATA FROM SERVER
        private async Task GetRemoteAppData()
        {
            applicationData.About = "A local network chat app.";
            applicationData.Multicast_ip = "224.0.1.142";
            applicationData.Multicast_port = "3526";
            applicationData.Unicast_msg_port = "5246";
            applicationData.Unicast_file_port = "5145";
            applicationData.Unicast_picture_port = "5032";
        }

        #endregion

        #region VOICE MESSAGE

        public async Task InitializeAudioRecording()
        {
            _mediaCaptureManager = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings();
            settings.StreamingCaptureMode = StreamingCaptureMode.Audio;
            settings.MediaCategory = MediaCategory.Other;
            settings.AudioProcessing = (_rawAudioSupported && _userRequestedRaw) ? AudioProcessing.Raw : AudioProcessing.Default;

            await _mediaCaptureManager.InitializeAsync(settings);

            _mediaCaptureManager.Failed += new MediaCaptureFailedEventHandler(Failed);
        }

        private void voiceButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (selectedUser != null)
            {
                if (selectedUser.Username.Equals("trending"))
                    return;
                if (selectedUser.CurrentStatus != User.Status.Offline)
                    voiceMessagePopup.IsOpen = true;
            }
        }

        async void recordingTimer_Tick(object sender, object e)
        {
            if (recordingTimeCounter >= 0)
            {
                recordingTimeCounter--;
                recordingTimerText.Text = recordingTimeCounter.ToString();
            }

            if (recordingTimeCounter == 0)
            {
                recordingTimer.Stop();
                recordingTimerText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                recordingProgressRing.IsActive = false;
                stopButtonGrid.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                sendAudioButtonGrid.Visibility = Windows.UI.Xaml.Visibility.Visible;
                await StopCapture();
            }
        }

        private async void voiceRecordButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            voiceRecordButtonGrid.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            stopButtonGrid.Visibility = Windows.UI.Xaml.Visibility.Visible;
            recordingProgressRing.IsActive = true;
            recordingTimerText.Visibility = Windows.UI.Xaml.Visibility.Visible;
            recordingTimer.Start();
            await CaptureAudio();
        }

        private async void stopButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            recordingTimer.Stop();
            recordingTimerText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            recordingProgressRing.IsActive = false;
            stopButtonGrid.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            sendAudioButtonGrid.Visibility = Windows.UI.Xaml.Visibility.Visible;
            await StopCapture();
        }

        private async void sendAudioButtonMask_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (selectedUser != null)
            {
                if (selectedUser.Username.Equals("trending"))
                    return;

                if (selectedUser.CurrentStatus != User.Status.Offline)
                {
                    if (_recordStorageFile == null)
                        return;

                    Message msg = new Message() { Text = "You sent a voice message", SentSide = true, Type = Message.MessageType.Attachment, MsgUser = loggedInUser, Attachment = _recordStorageFile.Path, IsVoiceMsgProp = true };


                    activeConversation.Messages.Add(msg);
                    activeConversation.LastMessage = msg;
                }
            }

            if (_recordStorageFile != null)
            {
                if (activeConversation is GroupConversation)
                    await unicast.SendAttachmentToGroup(((GroupConversation)activeConversation).GroupUsers.ToList(), _recordStorageFile);
                else
                    await unicast.SendAttachment(selectedUser, _recordStorageFile);

                messagePop.Play();

                voiceMessagePopup.IsOpen = false;

                await StoreConversations();
            }
        }

        private async void Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            await new MessageDialog("Error capturing audio.").ShowAsync();
        }

        public async Task CaptureAudio()
        {
            try
            {
                String fileName = "Audio.wav";

                _recordStorageFile = await profilePicturesFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                MediaEncodingProfile recordProfile = MediaEncodingProfile.CreateM4a(AudioEncodingQuality.Auto);

                await _mediaCaptureManager.StartRecordToStorageFileAsync(recordProfile, this._recordStorageFile);

            }
            catch (Exception ex)
            {

            }
        }

        public async Task StopCapture()
        {
            await _mediaCaptureManager.StopRecordAsync();

            var stream = await _recordStorageFile.OpenAsync(FileAccessMode.Read);

            voiceMessagePlayer.AutoPlay = true;
            voiceMessagePlayer.SetSource(stream, _recordStorageFile.FileType);
            voiceMessagePlayer.Play();

        }

        private async void voiceMessageReceivedPlayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Message msg = ((Button)sender).DataContext as Message;
                var audioFile = await StorageFile.GetFileFromPathAsync(msg.Attachment);
                var stream = await audioFile.OpenAsync(FileAccessMode.Read);
                voiceMessagePlayer.SetSource(stream, audioFile.FileType);
                voiceMessagePlayer.Play();
            }
            catch (Exception ex)
            {

            }
        }

        private async void voiceMessageSentPlayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Message msg = ((Button)sender).DataContext as Message;
                var audioFile = await StorageFile.GetFileFromPathAsync(msg.Attachment);
                var stream = await audioFile.OpenAsync(FileAccessMode.Read);
                voiceMessagePlayer.SetSource(stream, audioFile.FileType);
                voiceMessagePlayer.Play();
            }
            catch (Exception ex)
            {

            }
        }

        #endregion

        private async Task Dummy()
        {
            loggedInUser = new User() { Name = "Katie Stewart", CurrentStatus = User.Status.Online, Email = "k.stewart1@harvard.edu", IPAddress = GetCurrentIPAddress(), Username = "katie.stewart" };

            User u1 = new User() { Name = "Jack Wilson", CurrentStatus = User.Status.Online, Email = "jack.wilson@durham.edu", Username = "jack.wilson", ProfilePicture = (await profilePicturesFolder.GetFileAsync("jack.wilson.jpg")).Path };
            User u2 = new User() { Name = "Miranda Boyd", CurrentStatus = User.Status.Online, ProfilePicture = (await profilePicturesFolder.GetFileAsync("miranda.boyd.jpg")).Path };
            User u3 = new User() { Name = "Jacob Palvadeau", CurrentStatus = User.Status.Online, ProfilePicture = (await profilePicturesFolder.GetFileAsync("jacob.palvadeau.jpg")).Path };
            User u4 = new User() { Name = "Maison Zurbriggen", CurrentStatus = User.Status.Online, ProfilePicture = (await profilePicturesFolder.GetFileAsync("mason.zurbriggen.jpg")).Path };
            User u5 = new User() { Name = "Maizee Hayez", CurrentStatus = User.Status.Busy, ProfilePicture = (await profilePicturesFolder.GetFileAsync("maizee.hayez.jpg")).Path };
            User u6 = new User() { Name = "Matt McKinney", CurrentStatus = User.Status.Online, ProfilePicture = (await profilePicturesFolder.GetFileAsync("matt.mckinney.jpg")).Path };
            User u7 = new User() { Name = "Roman Adonis", CurrentStatus = User.Status.Busy, ProfilePicture = (await profilePicturesFolder.GetFileAsync("roman.adonis.jpg")).Path };
            User u8 = new User() { Name = "Sarah Licina", CurrentStatus = User.Status.Online, ProfilePicture = (await profilePicturesFolder.GetFileAsync("sarah.licina.jpg")).Path };
            User u9 = new User() { Name = "Stephen Stark", CurrentStatus = User.Status.Online, ProfilePicture = (await profilePicturesFolder.GetFileAsync("stephen.stark.jpg")).Path };
            User u10 = new User() { Name = "Lilly Jasmine", CurrentStatus = User.Status.Busy, ProfilePicture = (await profilePicturesFolder.GetFileAsync("lilly.jasmine.jpg")).Path };
            User u11 = new User() { Name = "Sarah Puhar", CurrentStatus = User.Status.Online, ProfilePicture = (await profilePicturesFolder.GetFileAsync("sara.puhar.jpg")).Path };
            User u12 = new User() { Name = "Cassie Adams", CurrentStatus = User.Status.Online, ProfilePicture = (await profilePicturesFolder.GetFileAsync("cassie.adams.jpg")).Path };

            availableUsers.Add(u1);
            availableUsers.Add(u2);
            availableUsers.Add(u3);
            availableUsers.Add(u4);
            availableUsers.Add(u5);
            availableUsers.Add(u6);
            availableUsers.Add(u7);
            availableUsers.Add(u8);
            availableUsers.Add(u9);
            availableUsers.Add(u10);
            availableUsers.Add(u11);
            availableUsers.Add(u12);

            Conversation c1 = new Conversation() { MessageUser = u1 };
            c1.Messages.Add(new Message() { Text = "Where are you?", ReceivedSide = true, MsgUser = u1, Type = Message.MessageType.Text });
            c1.Messages.Add(new Message() { Text = "Library", SentSide = true, Type = Message.MessageType.Text });
            c1.Messages.Add(new Message() { Text = "Are you done with your draft?", ReceivedSide = true, MsgUser = u1, Type = Message.MessageType.Text });
            c1.Messages.Add(new Message() { Text = "I need some help. Come over", SentSide = true, Type = Message.MessageType.Text });
            c1.Messages.Add(new Message() { Text = "On my way", ReceivedSide = true, MsgUser = u1, Type = Message.MessageType.Text });
            c1.LastMessage = new Message() { Text = "On my way", ReceivedSide = true, MsgUser = u1, Type = Message.MessageType.Text };

            Conversation c2 = new Conversation() { MessageUser = u2 };
            c2.LastMessage = new Message() { Text = "It's pretty cool :D", ReceivedSide = true, MsgUser = u2, Type = Message.MessageType.Text };

            Conversation c3 = new Conversation() { MessageUser = u9 };
            c3.LastMessage = new Message() { Text = "Come over to the cafetaria", ReceivedSide = true, MsgUser = u9, Type = Message.MessageType.Text };


            GroupConversation gc1 = new GroupConversation();
            gc1.GroupUsers.Add(u6);
            gc1.GroupUsers.Add(u12);
            gc1.MessageUser = new User() { IsGroupUser = true, Name = u6.Name + ", " + u12.Name, ProfilePicture = "ms-appx:///Assets/default_group_profile_picture.png", CurrentStatus = User.Status.Online };

            Message gm1 = new Message() { Type = Message.MessageType.Attachment, IsImageProp = true, MsgUser = u6, IsGroupMessage = true, Text = "Happening now...", ReceivedSide = true, Attachment = (await localFolder.GetFileAsync("sing.jpg")).Path };
            Message gm2 = new Message() { Type = Message.MessageType.Attachment, IsVoiceMsgProp = true, MsgUser = u12, IsGroupMessage = true, Text = "Sent a voice message", ReceivedSide = true };
            Message gm3 = new Message() { Text = "Hang on I'm coming", SentSide = true, IsGroupMessage = true, SentToGroupUsers = "Sent to: " + u6.Name + ", " + u12.Name, Type = Message.MessageType.Text };

            gc1.Messages.Add(gm1);
            gc1.Messages.Add(gm2);
            gc1.Messages.Add(gm3);
            gc1.LastMessage = gm3;

            recentMessages.Add(c2);
            recentMessages.Add(c3);
            recentMessages.Add(c1);
            recentMessages.Add(gc1);

        }
    }
}
