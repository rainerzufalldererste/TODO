using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LamestWebserver;

namespace TODO
{
  class Program
  {
    static void Main(string[] args)
    {
      TODO.Main.LoadConfig();
      User.DeserializeUsers();

      if (User.Users.Count == 0)
      {
        new User("Admin", "Password") { IsAdmin = true };
        User.SerializeUsers();
      }

      Project.DeserializeProjects();

      foreach (var p in Project.Projects)
      {
        p.Value.UpdateRepository();
        p.Value.UpdateTasks();
      }

      using (WebServer webserver = new WebServer(7070, "./web"))
      {
        Master.DiscoverPages();

        string input;

        Console.WriteLine("Enter 'exit' to quit.");

        do
        {
          switch (input = Console.ReadLine())
          {
            case "help":
              Console.WriteLine("exit, add-user, add-to-project, projects, users, make-admin, make-no-admin, load-config");
              break;

            case "exit":
              break;

            case "add-user":
              Console.WriteLine("Enter username (password will be 'password')");
              new User(Console.ReadLine(), "password");
              break;

            case "add-to-project":
              Console.WriteLine("Enter username, then project name");
              try
              {
                User.Users[Console.ReadLine()].ProjectIds.Add(Console.ReadLine());
              }
              catch (Exception e)
              {
                Console.WriteLine($"Failed. ({e.Message})");
              }
              break;

            case "make-admin":
              Console.WriteLine("Enter username");
              try
              {
                User.Users[Console.ReadLine()].IsAdmin = true;
                User.SerializeUsers();
              }
              catch (Exception e)
              {
                Console.WriteLine($"Failed. ({e.Message})");
              }
              break;

            case "make-no-admin":
              Console.WriteLine("Enter username");
              try
              {
                User.Users[Console.ReadLine()].IsAdmin = false;
                User.SerializeUsers();
              }
              catch (Exception e)
              {
                Console.WriteLine($"Failed. ({e.Message})");
              }
              break;

            case "projects":
              foreach (var p in Project.Projects)
                Console.WriteLine(p.Value.Name);
              break;

            case "users":
              foreach (var u in User.Users)
                Console.WriteLine(u.Value.UserName);
              break;

            case "load-config":
              TODO.Main.LoadConfig();
              break;

            default:
              Console.WriteLine($"Unrecognized command '{input}'. Enter 'exit' to quit.");
              break;
          }
        } while (input != "exit");
      }
    }
  }
}
