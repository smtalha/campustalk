using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CampusTalk.Validation
{
    class Validator
    {

        public bool IsValidEmail(string email)
        {
            // Return true if strIn is in valid e-mail format. 
            try
            {
                return Regex.IsMatch(email,
                      @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
                      @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
                      RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }


        public bool isValidUsername(string username)
        {
            return Regex.Match(username, "^[a-z0-9.]{3,30}$").Success;
        }

        public bool isValidPassword(string password)
        {
            return Regex.Match(password, "^[A-Za-z0-9.!@#$%^&*-_+]{8,30}$").Success;
        }
    }
}
