using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace CampusTalk
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ExtendedSplash : Page
    {
        public ExtendedSplash()
        {
            this.InitializeComponent();
            LoadingAnimation.Begin();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            

            //--- put the startup code here
            
            //--- navigate to the chat screen
            //this.Frame.Navigate(typeof(MainPage), data);
         
            //--- navigate to login screen              
            //this.Frame.Navigate(typeof(LoginScreen));
        }
 

    }
}
