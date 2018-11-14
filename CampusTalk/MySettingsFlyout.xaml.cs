using CampusTalk.Collections;
using CampusTalk.Encryption;
using CampusTalk.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


namespace CampusTalk
{
    public sealed partial class MySettingsFlyout : SettingsFlyout
    {
        #region CLASS ATTRIBUTES

        string UPDATE_USER_URL = "http://campustalk.pk/db-admin/update_user.php";
        string CHANGE_PASSWORD_URL = "http://campustalk.pk/db-admin/cp.php";

        #endregion

        #region CLASS CONSTRUCTOR
        public MySettingsFlyout()
        {
            this.InitializeComponent();
            userDetailsSection.DataContext = ChatScreen.loggedInUser;
            appSection.DataContext = ChatScreen.applicationData;
            favouriteUserSection.DataContext = ChatScreen.favouriteUsers;
            blockedUserSection.DataContext = ChatScreen.blockedUsers;
        }
        #endregion

        #region PROFILE PICTURE
        private async void profile_picture_tapped(object sender, TappedRoutedEventArgs e)
        {
            await SelectFile();    
        }

        private async Task SelectFile()
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".png");
            openPicker.CommitButtonText = "Select";


            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                ChatScreen.loggedInUser.ProfilePicture = null;
                ChatScreen.loggedInUser.ProfilePicture = "ms-appx:///Assets/default_profile_picture.png";
                StorageFile pp = await file.CopyAsync(ChatScreen.localFolder, ChatScreen.loggedInUser.Username + file.FileType, NameCollisionOption.ReplaceExisting);
                ChatScreen.loggedInUser.ProfilePicture = pp.Path;
                await StoreUser();
                await ChatScreen.multicast.update();
            }

        }

        #endregion


        #region FAVOURITES AND BLOCKED
        private void favouriteUsersButton_Click(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)b);
        }
        private void blockedUsersButton_Click(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)b);
        }

        private void favouriteRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            User usr = b.DataContext as User;

            if (ChatScreen.favouriteUsers.Contains(usr))
                ChatScreen.favouriteUsers.Remove(usr);

        }

        private async void unblockButton_Click(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            User usr = b.DataContext as User;

            if (ChatScreen.blockedUsers.Contains(usr))
                ChatScreen.blockedUsers.Remove(usr);

            await ChatScreen.multicast.update();
        }

        #endregion

        #region LOG OUT
        private async void logoutButton_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                var file = await ChatScreen.localFolder.GetFileAsync("logged_in_user.json");
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                this.Hide();
                Frame rootFrame = Window.Current.Content as Frame;
                rootFrame.Navigate(typeof(LoginScreen));
            }
            catch (FileNotFoundException ex)
            {
                this.Hide();
                Frame rootFrame = Window.Current.Content as Frame;
                rootFrame.Navigate(typeof(LoginScreen));
            }
        }
        #endregion

        #region SAVE CHANGES
        private async void saveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            saveProgress.Visibility = Windows.UI.Xaml.Visibility.Visible;

            if (nameBox.Text.Length > 2)
                ChatScreen.loggedInUser.Name = nameBox.Text;

            //check for email conditions
            ChatScreen.loggedInUser.Email = emailBox.Text;

            await ChatScreen.multicast.update();

            await StoreUser();
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
            
            saveProgress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        #endregion

        #region LOCAL STORAGE
        private async Task StoreUser()
        {
            // Serialize our Product class into a string             
            string jsonContents = JsonConvert.SerializeObject(ChatScreen.loggedInUser);
            StorageFile textFile = await ChatScreen.localFolder.CreateFileAsync("logged_in_user.json",
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

        public static bool IsNetworkAvailable(NetworkConnectivityLevel minimumLevelRequired = NetworkConnectivityLevel.InternetAccess)
        {
            ConnectionProfile profile =
                NetworkInformation.GetInternetConnectionProfile();

            NetworkConnectivityLevel level =
                profile.GetNetworkConnectivityLevel();

            return level >= minimumLevelRequired;
        }

    }
}
