using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using LamestWebserver.Core;
using LamestWebserver.Collections;
using LamestWebserver.Core.Parsing;
using LamestWebserver.Serialization;
using LibGit2Sharp;

namespace TODO
{
  public struct Comment
  {
    public string User;
    public string Message;
    public DateTime Time;
  }

  public struct FilePosition
  {
    public string File;
    public int Line;
    public string Version;
  }

  public class UsableProjectTask : IEquatable<ProjectTask>
  {
    public bool Used;
    public bool LooselyMatched;
    public ProjectTask Task;

    public UsableProjectTask(ProjectTask task)
    {
      Used = false;
      LooselyMatched = false;
      Task = task;
    }

    public bool Equals(ProjectTask other)
    {
      return ReferenceEquals(this, other);
    }
  }

  public enum ETaskState
  {
    Open, Assigned, MaybeResolved, Resolved, Closed, Dummy
  }

  [Serializable]
  public class ProjectTask
  {
    public string File;
    public int Line;
    public string Text;
    public string LineText;
    public string Version;
    public string Region;
    public bool SourceTask;
    public string Gone;
    public DateTime DetectedTime;
    public ETaskState TaskState = ETaskState.Open;
    public string AssignedTo = null;
    public int Index = 0;

    public List<FilePosition> FilePositions;
    public List<Comment> Comments;

    public ProjectTask()
    {

    }

    public ProjectTask(string file, int lineIndex, string[] lines, string version)
    {
      DetectedTime = DateTime.UtcNow;
      Gone = null;
      SourceTask = true;
      File = file;
      Line = lineIndex - 1;
      LineText = lines[Line];
      int startIndex;
      LineText.FindString("// TODO: ", out startIndex);

      if (startIndex < LineText.Length - "// TODO: ".Length - 2)
        Text = LineText.Substring(startIndex + "// TODO: ".Length);
      else
        Text = LineText;

      for (int i = Line + 1; i < lines.Length; i++)
      {
        string line = lines[i].TrimStart(' ', '\t');

        if (line.StartsWith("//") && !line.StartsWith("// TODO: "))
        {
          Text += "\n";

          if (line.Length > 2)
            Text += line.Substring(2);
        }
        else
        {
          break;
        }
      }

      Region = "";

      for (int i = Line - 2; i > 0; i--)
      {
        if (lines[i].Length > 0)
        {
          if (lines[i][0] == '}')
          {
            break;
          }
          else if (lines[i].Length > 0 && lines[i][0] != ' ' && lines[i][0] != '\t' && lines[i][0] != '#' && lines[i][0] != '{')
          {
            Region = lines[i];
            break;
          }
        }
      }

      Version = version;

      FilePositions = new List<FilePosition>() { new FilePosition() { File = File, Line = Line, Version = Version } };
      Comments = new List<Comment>();
    }

    public void Consume(ProjectTask task)
    {
      FilePositions.Add(new FilePosition() { File = task.File, Line = task.Line, Version = task.Version });
      File = task.File;
      Line = task.Line;
      Version = task.Version;
    }
  }

  public enum EAccessibility
  {
    InviteOnly,
    Public
  }

  [Serializable]
  public class Project
  {
    public string Name;
    public string URL;
    public string Folder;
    public EAccessibility Accessibility;
    public bool IncludeSubModules = false;

    public List<ProjectTask> Tasks = new List<ProjectTask>();
    public List<string> IgnoredPaths;

    public static AVLHashMap<string, Project> Projects = new AVLHashMap<string, Project>();

    internal AVLHashMap<string, string[]> Files = new AVLHashMap<string, string[]>();

    public Project()
    {
      IgnoredPaths = new List<string>() { "3rdParty" };
    }

    public Project(string name, string url, EAccessibility accessibility) : this()
    {
      Name = name;
      URL = url;
      Accessibility = accessibility;
      Folder = Directory.GetParent(Environment.CurrentDirectory).FullName + "\\projects\\" + Name;

      Projects.Add(Name, this);

      new Thread(() =>
      {
        try
        {
          Directory.CreateDirectory(Folder);
          ExecuteInPath(Folder, Main.Configuration.GitPath, $"clone {URL} {Folder}");
          UpdateRepository();
          UpdateTasks();
        }
        catch (Exception e)
        {
          Logger.LogError(e.SafeToString());
        }
      }).Start();
    }

    private void ExecuteInPath(string path, string application, string args)
    {
      System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(application, args);
      psi.WorkingDirectory = path;
      psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
      System.Diagnostics.Process.Start(psi).WaitForExit();
    }

    public void UpdateRepository()
    {
      Repository repo = new Repository(Folder);
      repo.Reset(ResetMode.Hard);
      ExecuteInPath(Folder, Main.Configuration.GitPath, $"fetch --force");
      Commands.Checkout(repo, "origin/master", new CheckoutOptions() { CheckoutModifiers = CheckoutModifiers.Force });

      if (IncludeSubModules)
        ExecuteInPath(Folder, Main.Configuration.GitPath, $"submodule update --init --recursive");
    }

    public void UpdateTasks()
    {
      List<ProjectTask> _tasks = new List<ProjectTask>();
      Files.Clear();

      Repository repo = new Repository(Folder);
      string versionString = repo.Head.Tip.Id.Sha;

      var files = Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories);

      foreach (string file in files)
      {
        bool anyContains = false;

        string fileName = file.Substring(Folder.Length);

        foreach (string ignored in IgnoredPaths)
        {
          if (fileName.Contains(ignored))
          {
            anyContains = true;
            break;
          }
        }

        if (anyContains)
          continue;

        try
        {
          var lines = File.ReadAllLines(file);

          int line = 1;

          foreach (string l in lines)
          {
            if (l.Contains("// TODO: "))
            {
              _tasks.Add(new ProjectTask(fileName, line, lines, versionString));
              Files.Add(fileName, lines);
            }

            line++;
          }
        }
        catch (Exception e)
        {
          Logger.LogError($"Failed to read file '{file}' with error '{e.Message}'.");
        }
      }

      List<UsableProjectTask> usableTasks = (from t in _tasks select new UsableProjectTask(t)).ToList();
      _tasks = null;

      Dictionary<ProjectTask, List<UsableProjectTask>> consumableTasks = new Dictionary<ProjectTask, List<UsableProjectTask>>();
      Dictionary<ProjectTask, bool> matchedTasks = new Dictionary<ProjectTask, bool>();

      foreach (var t in Tasks)
      {
        if (t.TaskState == ETaskState.Closed || t.TaskState == ETaskState.Resolved || t.TaskState == ETaskState.Dummy)
          continue;

        bool nothingMatched = true;

        for (int i = usableTasks.Count - 1; i >= 0; i--)
        {
          if (usableTasks[i].Task.Text == t.Text && usableTasks[i].Task.File == t.File)
          {
            if (!consumableTasks.ContainsKey(t))
              consumableTasks[t] = new List<UsableProjectTask>();

            usableTasks[i].LooselyMatched = true;
            consumableTasks[t].Add(usableTasks[i]);
            nothingMatched = false;
          }
        }

        if (nothingMatched)
        {
          t.Gone = versionString;
          t.TaskState = ETaskState.MaybeResolved;
        }
      }

      foreach (var ct in consumableTasks)
      {
        foreach (var t in ct.Value)
        {
          if (!t.Used && t.Task.Line == ct.Key.Line)
          {
            ct.Key.Consume(t.Task);
            matchedTasks.Add(ct.Key, true);
            t.Used = true;
            break;
          }
          else
          {
            var hunk = repo.Blame(t.Task.File, new BlameOptions() { StoppingAt = ct.Key.Version }).HunkForLine(t.Task.Line);
            
            if (hunk.InitialStartLineNumber <= ct.Key.Line && ct.Key.Line <= hunk.LineCount + hunk.InitialStartLineNumber)
            {
              ct.Key.Consume(t.Task);
              matchedTasks.Add(ct.Key, true);
              t.Used = true;
              break;
            }
          }
        }
      }

      foreach (var ct in consumableTasks)
      {
        if (matchedTasks.ContainsKey(ct.Key))
          continue;

        consumableTasks[ct.Key] = (from c in ct.Value where !c.Used select c).ToList();

        if (consumableTasks.Count == 1)
        {
          ct.Key.Consume(ct.Value[0].Task);
          matchedTasks.Add(ct.Key, true);
          ct.Value[0].Used = true;
        }
        else if (consumableTasks.Count > 1)
        {
          ct.Key.Gone = versionString;
          ct.Key.TaskState = ETaskState.MaybeResolved;
        }
      }

      foreach (var task in usableTasks)
        if (!task.Used)
          Tasks.Add(task.Task);

      SerializeProjects();
    }

    public string GetLines(string file, int line, int room = 15)
    {
      string[] lines = Files[file];

      if (lines == null)
        return "<NOT FOUND>";

      int minLine = System.Math.Max(line - room, 0);
      int maxLine = System.Math.Min(line + room, lines.Length - 1);

      string ret = "";

      for (int i = minLine; i < maxLine; i++)
      {
        if (i == line)
          ret += "<b>";

        ret += $"{i.ToString(new string('0', maxLine.ToString().Length))}: {lines[i].EncodeHtml().Replace("\t", "  ").Replace(" ", "&emsp;")}<br>";

        if (i == line)
          ret += "</b>";
      }

      return ret.Length > 0 ? ret.Substring(0, ret.Length - "<br>".Length) : "";
    }

    internal bool IsAccessible(User user)
    {
      if (user.IsAdmin)
        return true;

      if (Accessibility == EAccessibility.Public)
        return true;

      if (user.ProjectIds.Contains(Name))
        return true;

      return false;
    }

    public static void SerializeProjects()
    {
      try
      {
        Serializer.WriteJsonData(Projects, "../projects.json", true);
      }
      catch (Exception e)
      {
        Logger.LogError(e.SafeToString());
      }
    }

    public static void DeserializeProjects()
    {
      try
      {
        Projects = Serializer.ReadJsonData<AVLHashMap<string, Project>>("../projects.json");
      }
      catch (Exception e)
      {
        Logger.LogError(e.SafeToString());
      }
    }
  }
}
