﻿// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Globalization;
using Windows.Data;
using System.Text;
using Windows.UI.Xaml.Data;

namespace CampusTalk.Converters
{
    /// <summary>
    /// Date and time converter for hourly feeds.
    /// </summary>
    /// <QualityBand>Preview</QualityBand>
    public class ConversationDateTimeConverter : IValueConverter
    {
        /// <summary>
        /// Converts a
        /// <see cref="T:System.DateTime"/>
        /// object into a string appropriately formatted for hourly feeds.
        /// This format can be found in messaging.
        /// </summary>
        /// <param name="value">The given date and time.</param>
        /// <param name="targetType">
        /// The type corresponding to the binding property, which must be of
        /// <see cref="T:System.String"/>.
        /// </param>
        /// <param name="parameter">(Not used).</param>
        /// <param name="culture">(Not used).</param>
        /// <returns>The given date and time as a string.</returns>
             

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // Target value must be a System.DateTime object.
            if (!(value is DateTime))
            {
                throw new ArgumentException();
            }

            StringBuilder result = new StringBuilder(string.Empty);

            DateTime given = (DateTime)value;

            DateTime current = DateTime.Now;

            if (DateTimeFormatHelper.IsFutureDateTime(current, given))
            {
                // Future dates and times are not supported.
                throw new NotSupportedException();
            }

            if (DateTimeFormatHelper.IsAnOlderYear(current, given))
            {
                result.AppendFormat(CultureInfo.CurrentCulture, "{0}, {1}",
                                            DateTimeFormatHelper.GetShortDate(given),
                                            DateTimeFormatHelper.GetSuperShortTime(given));
            }
            else if (DateTimeFormatHelper.IsAnOlderWeek(current, given))
            {
                result.AppendFormat(CultureInfo.CurrentCulture, "{0}, {1}",
                                            DateTimeFormatHelper.GetMonthAndDay(given),
                                            DateTimeFormatHelper.GetSuperShortTime(given));
            }
            else if (DateTimeFormatHelper.IsPastDayOfWeekWithWindow(current, given))
            {
                result.AppendFormat(CultureInfo.CurrentCulture, "{0}, {1}",
                                            DateTimeFormatHelper.GetAbbreviatedDay(given),
                                            DateTimeFormatHelper.GetSuperShortTime(given));
            }
            else
            {
                // Given day time is today.
                result.Append(DateTimeFormatHelper.GetSuperShortTime(given));
            }

            if (DateTimeFormatHelper.IsCurrentUICultureFrench())
            {
                result.Replace(",", string.Empty);
            }

            return result.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}