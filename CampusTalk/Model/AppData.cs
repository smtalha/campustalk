using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CampusTalk.Model
{
    public class AppData
    {
        private static string multicast_ip;

        public string Multicast_ip
        {
            get { return multicast_ip; }
            set { multicast_ip = value; }
        }

        private static string multicast_port;

        public string Multicast_port
        {
            get { return multicast_port; }
            set { multicast_port = value; }
        }

        private static string unicast_msg_port;

        public string Unicast_msg_port
        {
            get { return unicast_msg_port; }
            set { unicast_msg_port = value; }
        }

        private static string unicast_file_port;

        public string Unicast_file_port
        {
            get { return unicast_file_port; }
            set { unicast_file_port = value; }
        }

        private static string unicast_picture_port;

        public string Unicast_picture_port
        {
            get { return unicast_picture_port; }
            set { unicast_picture_port = value; }
        }

        private static string about;

        public string About
        {
            get { return about; }
            set { about = value; }
        }

    }
}
