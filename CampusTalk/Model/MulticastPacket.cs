using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;

namespace CampusTalk.Model
{
    class MulticastPacket
    {
        public MulticastPacket(User user,bool _ack)
        {
            userIP = new HostName(user.IPAddress);
            username = user.Username;
            status = user.CurrentStatus.ToString();
            ack = _ack;
            userFullName = user.Name;
            userEmail = user.Email;
        }

        private HostName userIP;

	public HostName UserIP
	{
		get { return userIP;}
		set { userIP = value;}
	}

    private string username;

    public string Username
    {
        get { return username; }
        set { username = value; }
    }

    private string userFullName;

    public string UserFullName
    {
        get { return userFullName; }
        set { userFullName = value; }
    }

    private string userEmail;

    public string UserEmail
    {
        get { return userEmail; }
        set { userEmail = value; }
    }
    

    private string status;

    public string Status
    {
        get { return status; }
        set { status = value; }
    }
    

        private bool ack;

	public bool Ack
	{
		get { return ack;}
		set { ack = value;}
	}
	
        
        
    }
}
