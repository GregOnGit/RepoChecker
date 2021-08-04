using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using LibGit2Sharp;
using Docker.DotNet;
using System.Diagnostics;
using Docker.DotNet.Models;

namespace RepoChecker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Checker.Init();

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}

public class Checker
{
    // The thread that will constantly check for the changes in the background
    public static Thread s_thread;

    // The local copy of the repo that we will be comparing to the online version (Both)
    public static Repository s_repoASP;
    public static Repository s_repoAPK;

    // Current status of the building process
    public static RepoChecker.Pages.UpdateStatus s_updateStatus = RepoChecker.Pages.UpdateStatus.UpToDate;

    // Are we updating right now?
    public static bool s_updatingNow = false;

    // Storage for create task
    public static Task< CreateContainerResponse > s_createTask;

    public static bool s_keepChecking = true;

    public static DockerClient s_dockerClient;

    public static void Init()
    {
        // Init Repo with absolute path to existing local repo
        s_repoASP = new LibGit2Sharp.Repository( @"/home/greg/SyncThing/Personal/Projects/OS/WebDocker/ShowcaseServer" );

        // Create a thread that constantly checks in the background
        /*s_thread = new Thread( new ThreadStart( ThreadedCheck ) );
        s_thread.Name = "CheckerThread";
        s_thread.Start();*/

        // Going local for now
        s_dockerClient = new DockerClientConfiguration( new Uri( "http://127.0.0.1:4243" ) ).CreateClient();

        // s_dockerClient.Containers
    }

    private static void ThreadedCheck()
    {
        // Branch l_mainBranch = s_repo.Branches["master"];
        
        /*foreach( Branch l_tmpBranch in s_repo.Branches )
        {
            Console.WriteLine( "---" );
            Console.WriteLine( l_tmpBranch.RemoteName );
            Console.WriteLine( l_tmpBranch.FriendlyName );
            Console.WriteLine( l_tmpBranch.CanonicalName );
            Console.WriteLine( l_tmpBranch.UpstreamBranchCanonicalName );
            Console.WriteLine( "---" );
        }*/

        /*if( l_mainBranch.IsCurrentRepositoryHead )
        {
            Console.WriteLine( "IS CURRENT REPO HEAD" );
        }
        else
        {
            Console.WriteLine( "IS NOT" );
        }*/

        /*FetchOptions l_options = new FetchOptions();

        IEnumerable< string > refSpecs = new RefSpec();
        Commands.Fetch( s_repo, "https://github.com/GregOnGit/ShowcaseServer", );*/
        /*while( s_keepChecking )
        {
            
        }*/

        return;
    }

    public static string Bash( string l_cmd )
    {
        string l_escapedArgs = l_cmd.Replace( "\"", "\\\"" );

        Process l_process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{l_escapedArgs}\"", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
            }
        };

        l_process.Start();
        string l_result = l_process.StandardOutput.ReadToEnd();
        l_process.WaitForExit();

        return l_result;
    }
}
