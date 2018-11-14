using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Data;

namespace CampusTalk.Converters
{
    class FileToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            StorageFile file = (StorageFile)value;

            if (file != null)
                return file.Path;
            else
                return "ms-appx:///Assets/default_profile_picture.png";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return StorageFile.GetFileFromPathAsync((string)value);
        }
    }
}
