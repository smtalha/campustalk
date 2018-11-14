using CampusTalk.Encryption;
using CampusTalk.Model;
using CampusTalk.Validation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace CampusTalk
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LoginScreen : Page
    {

        #region CLASS ATTRIBUTES

        User loggedInUser;
        Validator validator;
        StorageFolder localFolder;

        string SIGNUP_URL = "http://campustalk.pk/db-admin/sign_up.php";
        string SIGNIN_URL = "http://campustalk.pk/db-admin/sign_in.php";

        #endregion

        #region CLASS CONSTRUCTOR
        public LoginScreen()
        {
            this.InitializeComponent();
            validator = new Validator();

        }

        #endregion

        #region ON NAVIGATED TO
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            localFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("CampusTalk", CreationCollisionOption.OpenIfExists);
            await LoadUser();
            if (loggedInUser != null)
            {
                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    this.Frame.Navigate(typeof(ChatScreen), loggedInUser);
                });
            }
            Storyboard1.Begin();
            signUpUsername.Focus(FocusState.Programmatic);

        }

        #endregion

        #region SIGN UP BUTTON
        private async void signUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsNetworkAvailable())
            {
                await new MessageDialog("Internet connectivity required.").ShowAsync();
                return;
            }

            if (signUpName.Text.Length < 2)
            {
                FlyoutBase.ShowAttachedFlyout((FrameworkElement)signUpName);
                return;
            }
            if (!validator.IsValidEmail(signUpEmail.Text))
            {
                FlyoutBase.ShowAttachedFlyout((FrameworkElement)signUpEmail);
                return;
            }
            if (!validator.isValidUsername(signUpUsername.Text))
            {
                FlyoutBase.ShowAttachedFlyout((FrameworkElement)signUpUsername);
                return;
            }

            bool exception = false;

            try
            {
                await Sign_Up(signUpUsername.Text, signUpName.Text, signUpEmail.Text);
            }
            catch (Exception ex)
            {
                exception = true;
                loading.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            if (exception)
                await new MessageDialog("An error occured. Please try again.").ShowAsync();

            signUpUsername.Text = "";
            signUpName.Text = "";
            signUpEmail.Text = "";

        }

        #endregion


        #region GET DEVICE'S IP ADDRESS
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

        #region SIGN IN/ SIGN UP WEBSERVICE CALL
        private async Task Sign_Up(string username, string name, string email)
        {
            loading.Visibility = Windows.UI.Xaml.Visibility.Visible;

            await Task.Delay(3000);

            loading.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            loggedInUser = new User();
            loggedInUser.Username = username;
            loggedInUser.Name = name;
            loggedInUser.Email = email;
            loggedInUser.IPAddress = GetCurrentIPAddress();

            await StoreUser();

            this.Frame.Navigate(typeof(ChatScreen), loggedInUser);

        }

        #endregion

        #region LOCAL STORAGE
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

        private async Task LoadUser()
        {

            try
            {
                // Getting JSON from file if it exists, or file not found exception if it does not  
                StorageFile textFile = await localFolder.GetFileAsync("logged_in_user.json");
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
                        loggedInUser = JsonConvert.DeserializeObject<User>(jsonContents);

                    }
                }
            }
            catch (FileNotFoundException e)
            {
                loggedInUser = null;
                return;
            }

        }

        #endregion

        private static bool IsNetworkAvailable(NetworkConnectivityLevel minimumLevelRequired = NetworkConnectivityLevel.InternetAccess)
        {
            ConnectionProfile profile =
                NetworkInformation.GetInternetConnectionProfile();

            NetworkConnectivityLevel level =
                profile.GetNetworkConnectivityLevel();

            return level >= minimumLevelRequired;
        }

    }

}
