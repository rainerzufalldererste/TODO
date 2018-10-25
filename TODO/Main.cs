using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LamestWebserver;
using LamestWebserver.UI;
using LamestWebserver.Serialization;
using LamestWebserver.Core;

namespace TODO
{
  public class Main : ElementResponse
  {
    [Serializable]
    public class Config
    {
      public string GitPath;

      public Config()
      {
        GitPath = "git";
      }
    }

    public static Config Configuration = new Config();

    public static void LoadConfig()
    {
      const string configFileName = "../config.json";

      if (!File.Exists(configFileName))
      {
        try
        {
          Serializer.WriteJsonData(Configuration, configFileName);
          Logger.LogInformation($"Wrote configuration file to '{configFileName}'");
        }
        catch { }

        return;
      }

      try
      {
        Configuration = Serializer.ReadJsonData<Config>(configFileName);
      }
      catch (Exception e)
      {
        Logger.LogError($"Failed to load config. ({e.SafeMessage()})");
      }
    }

    public Main() : base("/")
    {
    }

    protected override HElement GetElement(SessionData sessionData)
    {
      return GetPage("TODO", GetElements(sessionData));
    }

    private IEnumerable<HElement> GetElements(SessionData sessionData)
    {
      if (!sessionData.KnownUser)
      {
        if (sessionData.HttpPostVariables.ContainsKey("username") && sessionData.HttpPostVariables.ContainsKey("password"))
        {
          string username = sessionData.GetHttpPostValue("username");
          string password = sessionData.GetHttpPostValue("password");

          if (User.Users.ContainsKey(username))
          {
            User u = User.Users[username];

            if (u.Password.IsValid(password))
            {
              (sessionData as HttpSessionData).RegisterUser(username);
              sessionData.SetUserVariable(nameof(User), u);
              yield return new HScript(ScriptCollection.GetPageReloadInMilliseconds, 0);
              yield break;
            }
          }

          yield return new HText("The credentials were invalid.");
        }

        yield return new HForm("/")
        {
          Elements =
          {
            new HText("Username"),
            new HTextInput("username", "", "Username"),
            new HNewLine(),
            new HText("Password"),
            new HPasswordInput("password", "password"),
            new HNewLine(),
            new HButton("Login", HButton.EButtonType.submit)
          }
        };
        yield break;
      }
      else
      {
        User user = sessionData.GetUserVariable<User>(nameof(User));

        if (user.IsAdmin)
        {
          yield return new HButton("Create new project", nameof(CreateProject));
          yield return new HLine();
        }

        yield return new HButton("Manage User Preferences", nameof(ViewUser));

        foreach (var p in Project.Projects)
        {
          if (p.Value.IsAccessible(user))
          {
            yield return new HLink(p.Key, $"{nameof(ViewProject)}?id={p.Value.Name}");
            yield return new HNewLine();
          }
        }
      }
    }

    public static HElement GetPage(string title, IEnumerable<HElement> elements)
    {
      return new PageBuilder(title) { Elements = { new HContainer { Class = "main", Elements = { new HHeadline(title), new HContainer(elements) { Class = "inner" } } } }, StylesheetLinks = { "style.css" } };
    }
  }
}
