using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[ShutdownDotNetAfterServerBuild]
partial class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.CoreTests);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion] readonly GitVersion GitVersion;

    AbsolutePath OutputTests => RootDirectory / "TestResults";

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath OutputDirectory => RootDirectory / "output";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                //.SetAssemblyVersion(GitVersion.AssemblySemVer)
                //.SetFileVersion(GitVersion.AssemblySemFileVer)
                //.SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore());
        });

    Target ApiTests => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var pr = Solution.GetProject("Akka.API.Tests");
            foreach (var fw in pr.GetTargetFrameworks())
            {
                Information($"Running tests from {fw}");
                DotNetTest(c => c
                       .SetProjectFile(pr)
                       .SetConfiguration(Configuration.ToString())
                       .SetFramework(fw)
                       .SetResultsDirectory(OutputTests)
                       .SetProcessWorkingDirectory(Directory.GetParent(pr).FullName)
                       .SetLoggers("trx")
                       .SetVerbosity(verbosity: DotNetVerbosity.Normal)
                       .EnableNoBuild());
            }
        });
    Target ClusterTests => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var projects = RootDirectory
             .GlobFiles("src/**/Akka.Cluster.Tests.csproj", "src/**/Akka.Cluster.*.Tests.csproj");
            
            foreach (var project in projects)
            {
                Information($"Running tests from {project}");
                DotNetTest(c => c
                       .SetProjectFile(project)
                       .SetConfiguration(Configuration.ToString())
                       .SetFramework("net6.0")
                       .SetResultsDirectory(OutputTests)
                       .SetProcessWorkingDirectory(Directory.GetParent(project).FullName)
                       .SetLoggers("trx")
                       .SetVerbosity(verbosity: DotNetVerbosity.Normal)
                       .EnableNoBuild());
            }
        });
    Target MultiNodeTests => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var projects = RootDirectory
             .GlobFiles("src/**/*.Tests.MultiNode.csproj");
            
            foreach (var project in projects)
            {
                Information($"Running tests from {project}");
                DotNetTest(c => c
                       .SetProjectFile(project)
                       .SetConfiguration(Configuration.ToString())
                       .SetFramework("net6.0")
                       .SetResultsDirectory(OutputTests)
                       .SetProcessWorkingDirectory(Directory.GetParent(project).FullName)
                       .SetLoggers("trx")
                       .SetVerbosity(verbosity: DotNetVerbosity.Normal)
                       .EnableNoBuild());
            }
        });
    Target CoreTestKitTests => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var project = Solution.GetProject("Akka.TestKit.Tests");
            Information($"Running tests from {project}");
            DotNetTest(c => c
                   .SetProjectFile(project)
                   .SetConfiguration(Configuration.ToString())
                   .SetFramework("net6.0")
                   .SetResultsDirectory(OutputTests)
                   .SetProcessWorkingDirectory(Directory.GetParent(project).FullName)
                   .SetLoggers("trx")
                   .SetVerbosity(verbosity: DotNetVerbosity.Normal)
                   .EnableNoBuild());
        });
    Target CoreTests => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var project = Solution.GetProject("Akka.Tests");
            Information($"Running tests from {project}");
            DotNetTest(c => c
                   .SetProjectFile(project)
                   .SetConfiguration(Configuration.ToString())
                   .SetFramework("net6.0")
                   .SetResultsDirectory(OutputTests)
                   .SetProcessWorkingDirectory(Directory.GetParent(project).FullName)
                   .SetLoggers("trx")
                   .SetVerbosity(verbosity: DotNetVerbosity.Normal)
                   .EnableNoBuild());
        });
    Target PersistenceTests => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var projects = RootDirectory
             .GlobFiles("src/**/Akka.Persistence.*Tests.csproj");
            foreach (var project in projects)
            {
                Information($"Running tests from {project}");
                DotNetTest(c => c
                       .SetProjectFile(project)
                       .SetConfiguration(Configuration.ToString())
                       .SetFramework("net6.0")
                       .SetResultsDirectory(OutputTests)
                       .SetProcessWorkingDirectory(Directory.GetParent(project).FullName)
                       .SetLoggers("trx")
                       .SetVerbosity(verbosity: DotNetVerbosity.Normal)
                       .EnableNoBuild());
            }
        });
    
    Target RemoteTests => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var projects = RootDirectory
             .GlobFiles("src/**/Akka.Remote.*Tests*.csproj").
             Where(x=> !x.Name.Contains("Performance") && !x.Name.Contains("MultiNode"));
            foreach (var project in projects)
            {
                Information($"Running tests from {project}");
                DotNetTest(c => c
                       .SetProjectFile(project)
                       .SetConfiguration(Configuration.ToString())
                       .SetFramework("net6.0")
                       .SetResultsDirectory(OutputTests)
                       .SetProcessWorkingDirectory(Directory.GetParent(project).FullName)
                       .SetLoggers("trx")
                       .SetVerbosity(verbosity: DotNetVerbosity.Normal)
                       .EnableNoBuild());
            }
        });
    
    Target DiscoveryTests => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var project = Solution.GetProject("Akka.Discovery.Tests"); 
            Information($"Running tests from {project}");
            DotNetTest(c => c
                   .SetProjectFile(project)
                   .SetConfiguration(Configuration.ToString())
                   .SetFramework("net6.0")
                   .SetResultsDirectory(OutputTests)
                   .SetProcessWorkingDirectory(Directory.GetParent(project).FullName)
                   .SetLoggers("trx")
                   .SetVerbosity(verbosity: DotNetVerbosity.Normal)
                   .EnableNoBuild());
        });
    
    Target DocsTests => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var project = Solution.GetProject("Akka.Docs.Tests"); 
            Information($"Running tests from {project}");
            DotNetTest(c => c
                   .SetProjectFile(project)
                   .SetConfiguration(Configuration.ToString())
                   .SetFramework("net6.0")
                   .SetResultsDirectory(OutputTests)
                   .SetProcessWorkingDirectory(Directory.GetParent(project).FullName)
                   .SetLoggers("trx")
                   .SetVerbosity(verbosity: DotNetVerbosity.Normal)
                   .EnableNoBuild());
        });
    
    Target CoordinationTests => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var project = Solution.GetProject("Akka.Coordination.Tests"); 
            Information($"Running tests from {project}");
            DotNetTest(c => c
                   .SetProjectFile(project)
                   .SetConfiguration(Configuration.ToString())
                   .SetFramework("net6.0")
                   .SetResultsDirectory(OutputTests)
                   .SetProcessWorkingDirectory(Directory.GetParent(project).FullName)
                   .SetLoggers("trx")
                   .SetVerbosity(verbosity: DotNetVerbosity.Normal)
                   .EnableNoBuild());
        });
    
    Target StreamTests => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var projects = RootDirectory
             .GlobFiles("src/**/Akka.Streams.*Tests*.csproj").
             Where(x=> !x.Name.Contains("Performance"));
            foreach (var project in projects)
            {
                Information($"Running tests from {project}");
                DotNetTest(c => c
                       .SetProjectFile(project)
                       .SetConfiguration(Configuration.ToString())
                       .SetFramework("net6.0")
                       .SetResultsDirectory(OutputTests)
                       .SetProcessWorkingDirectory(Directory.GetParent(project).FullName)
                       .SetLoggers("trx")
                       .SetVerbosity(verbosity: DotNetVerbosity.Normal)
                       .EnableNoBuild());
            }
        });

    static void Information(string info)
    {
        Serilog.Log.Information(info);
    }
}
