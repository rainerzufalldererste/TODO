using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LamestWebserver;
using LamestWebserver.UI;
using LamestWebserver.Core;
using LamestWebserver.JScriptBuilder;

namespace TODO
{
  public class ViewProject : ElementResponse
  {
    public ViewProject() : base(nameof(ViewProject))
    {
    }

    protected override HElement GetElement(SessionData sessionData) => Main.GetPage(nameof(ViewProject), GetElements(sessionData));

    private IEnumerable<HElement> GetElements(SessionData sessionData)
    {
      if (!sessionData.KnownUser || !sessionData.HttpHeadVariables.ContainsKey("id"))
      {
        yield return new HScript(ScriptCollection.GetPageReferalToX, "/");
        yield break;
      }

      User user = sessionData.GetUserVariable<User>(nameof(User));
      Project project = Project.Projects[sessionData.HttpHeadVariables["id"]];

      if (project == null || !project.IsAccessible(user))
      {
        yield return new HString("Invalid Project ID");
        yield return new HButton("Back", "/");
        yield break;
      }

      if (user.IsAdmin)
      {
        yield return new HButton("Update Tasks", 
          InstantPageResponse.AddOneTimeRedirectWithCode(nameof(ViewProject) + "?id=" + sessionData.HttpHeadVariables["id"], true, sessData => new Thread(() =>
          {
            try
            {
              Project.Projects[sessionData.HttpHeadVariables["id"]].UpdateRepository();
              Project.Projects[sessionData.HttpHeadVariables["id"]].UpdateTasks();
            }
            catch (Exception e)
            {
              Logger.LogError(e.Message);
            }
          }).Start()));
      }

      yield return new HButton("Back", "/");

      foreach (var l in project.Tasks)
      {
        if (l.TaskState == ETaskState.Closed || l.TaskState == ETaskState.Resolved || l.TaskState == ETaskState.Dummy)
          continue;

        string hash = Hash.GetComplexHash();

        switch (l.TaskState)
        {
          case ETaskState.MaybeResolved:
            {
              HMultipleElements start = new HMultipleElements(new HText("Resolved?") { Class = "mayberesolved" });

              if (l.AssignedTo != null && l.AssignedTo == user.UserName)
                start += new HText("This task is assigned to you!") { Class = "assigned" };

              yield return new HContainer()
              {
                Elements =
                {
                  start,
                  new HText(l.Text),
                  new HText(l.Region) { Class = "region" },
                  new HText(l.File + " Line: " + l.Line) { Class = "file" },
                  new HText(l.Version) { Class = "version" },
                  new JSButton("Show Context")
                  {
                    ID = hash + "button",
                    DescriptionTags = $"onclick=\"document.getElementById(document.getElementById('{hash}').id).style.display =         'block';document.getElementById(document.getElementById('{hash}button').id).style.display = 'none';\"",
                    Class = "showcontext"
                  },
                  new HPlainText($"<p id='{hash}' class='source'>{project.GetLines(l.File, l.Line)}</p>")
                },
                Class = "task"
              };

              break;
            }

          default:
            yield return new HContainer()
            {
              Elements =
              {
                new HText(l.Text),
                new HText(l.Region) { Class = "region" },
                new HText(l.File + " Line: " + l.Line) { Class = "file" },
                new HText(l.Version) { Class = "version" },
                new JSButton("Show Context")
                {
                  ID = hash + "button",
                  DescriptionTags = $"onclick=\"document.getElementById(document.getElementById('{hash}').id).style.display =       'block';document.getElementById(document.getElementById('{hash}button').id).style.display = 'none';\"",
                  Class = "showcontext"
                },
                new HPlainText($"<p id='{hash}' class='source'>{project.GetLines(l.File, l.Line)}</p>")
              },
              Class = "task"
            };
            break;
        }
        
      }
    }
  }
}
