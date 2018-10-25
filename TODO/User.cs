using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LamestWebserver.Collections;
using LamestWebserver.Security;
using LamestWebserver.Core;

namespace TODO
{
  [Serializable]
  public class User
  {
    public string UserName;
    public bool IsAdmin;
    public Password Password;
    public List<string> ProjectIds = new List<string>();

    public User()
    {

    }

    public User(string username, string password)
    {
      UserName = username;
      Password = new Password(password);
      IsAdmin = false;

      Users.Add(UserName, this);
      SerializeUsers();
    }
    
    public static AVLTree<string, User> Users = new AVLTree<string, User>();

    public static void SerializeUsers()
    {
      try
      {
        LamestWebserver.Serialization.Serializer.WriteXmlData(Users, "../userData.xml");
      }
      catch (Exception e)
      {
        Logger.LogError(e.Message);
      }
    }

    public static void DeserializeUsers()
    {
      try
      {
        Users = LamestWebserver.Serialization.Serializer.ReadXmlData<AVLTree<string, User>>("../userData.xml");
      }
      catch (Exception e)
      {
        Logger.LogError(e.Message);
      }
    }
  }
}
