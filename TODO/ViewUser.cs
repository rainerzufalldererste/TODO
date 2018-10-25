using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LamestWebserver;
using LamestWebserver.UI;

namespace TODO
{
  public class ViewUser : ElementResponse
  {
    public ViewUser() : base(nameof(ViewUser))
    {
    }

    protected override HElement GetElement(SessionData sessionData) => Main.GetPage(nameof(ViewUser), GetElements(sessionData));

    private IEnumerable<HElement> GetElements(SessionData sessionData)
    {
      if (!sessionData.KnownUser)
      {
        yield return new HScript(ScriptCollection.GetPageReferalToX, "/");
        yield break;
      }

      User user = sessionData.GetUserVariable<User>(nameof(User));

      string password = sessionData.GetHttpPostValue(nameof(password));
      string newPassword = sessionData.GetHttpPostValue(nameof(newPassword));
      string newPassword2 = sessionData.GetHttpPostValue(nameof(newPassword2));

      if (password != null && newPassword != null && newPassword2 != null)
      {
        if (newPassword == newPassword2)
        {
          if (user.Password.IsValid(password))
          {
            user.Password = new LamestWebserver.Security.Password(newPassword);
            User.SerializeUsers();

            yield return new HText("Your password has been changed.");
          }
          else
          {
            yield return new HText("The Password is incorrect.") { Class = "error" };
          }
        }
        else
        {
          yield return new HText("The new Passwords did not match.") { Class = "error" };
        }
      }

      yield return new HButton("Back", "/");
      yield return new HHeadline("Change Password", 2);

      yield return new HForm("")
      {
        Elements =
        {
          new HText("Old Password"),
          new HPasswordInput(nameof(password)),
          new HNewLine(),
          new HText("New Password"),
          new HPasswordInput(nameof(newPassword)),
          new HNewLine(),
          new HText("Repeat New Password"),
          new HPasswordInput(nameof(newPassword2)),
          new HNewLine(),
          new HButton("Change Password", HButton.EButtonType.submit)
        }
      };
    }
  }
}
