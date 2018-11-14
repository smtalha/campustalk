using CampusTalk.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace CampusTalk.Converters
{
    public sealed class StatusToIndexConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var status = (User.Status)value;
            switch (status)
            {
                case User.Status.Online:
                    return 0;
                case User.Status.Busy:
                    return 1;
                //case User.Status.Offline:
                    //return 2;
                default:
                    return 0;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is User.Status && (User.Status)value == User.Status.Online;
        }
    }

}
