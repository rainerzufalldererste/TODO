using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LamestWebserver;
using LamestWebserver.UI;

namespace TODO
{
  public class CreateProject : ElementResponse
  {
    public CreateProject() : base(nameof(CreateProject))
    {
    }

    protected override HElement GetElement(SessionData sessionData) => Main.GetPage(nameof(CreateProject), GetElements(sessionData));

    private IEnumerable<HElement> GetElements(SessionData sessionData)
    {
      if (!sessionData.KnownUser || !sessionData.GetUserVariable<User>(nameof(User)).IsAdmin)
      {
        yield return new HScript(ScriptCollection.GetPageReferalToX, "/");
        yield break;
      }

      string title = sessionData.GetHttpPostValue(nameof(title));
      string url = sessionData.GetHttpPostValue(nameof(url));
      string access = sessionData.GetHttpPostValue(nameof(access));

      if (title != null && url != null && access != null)
      {
        new Project(title, url, access.StartsWith("u") ? EAccessibility.Public : EAccessibility.InviteOnly);
        yield return new HScript(ScriptCollection.GetPageReferalToX, $"/{nameof(ViewProject)}?id={title}");
        yield break;
      }

      yield return new HForm("")
      {
        Elements =
        {
          new HText("Title"),
          new HTextInput(nameof(title), "", "Title"),
          new HNewLine(),
          new HText("URL"),
          new HTextInput(nameof(url), "", "http://git@gitlab.git//git/git.git"),
          new HNewLine(),
          new HText("Accessibility"),
          new HSingleSelector(nameof(access), new List<Tuple<string, string>> { Tuple.Create("Invite Only", "r"), Tuple.Create("Public", "u") }),
          new HNewLine(),
          new HButton("Create Project", HButton.EButtonType.submit),
          new HButton("Cancel", "/"),
        }
      };
    }
  }
}
