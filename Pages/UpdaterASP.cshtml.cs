using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using LibGit2Sharp;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Threading;
using System.IO;

namespace RepoChecker.Pages
{
    // Enum for determining the state of the update
    public enum UpdateStatus
    {
        PreStage,
        Pulling,
        RemovingOldContainer,
        CleaningProject,
        BuildingProject,
        BuildingImage,
        CreatingContainer,
        StartingContainer,
        UpToDate,
    }

    public class UpdaterASPModel : PageModel
    {
        
        private readonly ILogger<PrivacyModel> _logger;

        public UpdaterASPModel(ILogger<PrivacyModel> logger)
        {
            _logger = logger;
        }

        public void Refresh_Page()
        {
            Response.Redirect( "UpdaterASP" );
        }

        public void OnGet()
        {
            switch( Checker.s_updateStatus )
            {
                case UpdateStatus.UpToDate:
                    // Nothing needs to happen
                    Checker.s_updatingNow = false;
                    return;
                case UpdateStatus.PreStage:
                    // To set up next stage
                    break;
                case UpdateStatus.Pulling:
                    Stage_Pulling();
                    break;
                case UpdateStatus.RemovingOldContainer:
                    Stage_RemovingOldContainer();
                    break;
                case UpdateStatus.CleaningProject:
                    Stage_CleaningProject();
                    break;
                case UpdateStatus.BuildingProject:
                    Stage_BuildingProject();
                    break;
                case UpdateStatus.BuildingImage:
                    Stage_BuildingImage();
                    break;
                case UpdateStatus.CreatingContainer:
                    Stage_CreatingContainer();
                    break;
                case UpdateStatus.StartingContainer:
                    Stage_StartingContainer();
                    break;
                // If the enum is larger than all other cases, than go back to 0 ( UpToDate )
                default:
                    Checker.s_updateStatus = UpdateStatus.UpToDate;
                    Checker.s_updatingNow = false;
                    Refresh_Page();
                    return;
            }

            // Increase to each stage
            if( Checker.s_updatingNow )
            {
                Checker.s_updateStatus++;
            }
        }

        public void OnPost()
        {
            Checker.s_updateStatus = UpdateStatus.PreStage;

            Checker.s_updatingNow = true;

            // Start the whole staging process
            Refresh_Page();
        }

        public void Stage_Pulling()
        {
            // RefSpecs for args on Fetch
            Remote l_remote = Checker.s_repoASP.Network.Remotes.FirstOrDefault();
            IEnumerable< string > l_refSepcs = l_remote.FetchRefSpecs.Select( x => x.Specification );

            // Update the local branch to match remote
            Branch l_branch = Checker.s_repoASP.Branches[ "main" ];
            PullOptions l_pullOptions = new PullOptions();
            l_pullOptions.FetchOptions = new FetchOptions();
            l_pullOptions.MergeOptions = new MergeOptions();
            Signature l_signature = new Signature ( "Greg", "gregoryonbusiness@gmail.com", new DateTimeOffset() );
            Commands.Pull( Checker.s_repoASP, l_signature, l_pullOptions );
        }

        public void Stage_RemovingOldContainer()
        {
            // Get the container list
            Task < IList< ContainerListResponse > > l_cTask = Checker.s_dockerClient.Containers.ListContainersAsync( new ContainersListParameters(){ Limit = 10, } );
            IList< ContainerListResponse > l_containerList = l_cTask.Result;
            
            // Kill all containers
            foreach( ContainerListResponse l_clr in l_containerList )
            {
                Checker.s_dockerClient.Containers.StopContainerAsync( l_clr.ID, new ContainerStopParameters(), CancellationToken.None ).Wait();
                Checker.s_dockerClient.Containers.RemoveContainerAsync( l_clr.ID, new ContainerRemoveParameters(){ Force = true }, CancellationToken.None ).Wait();
            }
        }

        public void Stage_CleaningProject()
        {
            // Delete all old files to refresh the build
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ && rm -rf ToCompileComplexWeb/* " ) );
            
            // Copy everything from the updated local repo
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ && cp -rT ShowcaseServer ToCompileComplexWeb/" ) );

            // Clean out old object files from last build
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ && rm -rf ToCompileComplexWeb/bin/* && rm -rf ToCompileComplexWeb/obj/*" ) );

            // Clean from last build
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ToCompileComplexWeb && dotnet clean" ) );
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ToCompileComplexWeb && dotnet restore" ) );

        }

        public void Stage_BuildingProject()
        {
            // Rebuild the whole project
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ToCompileComplexWeb && dotnet build -c Release" ) );

            // Build and Publish a release version of the project so all dependencies are included with the binary
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ToCompileComplexWeb && dotnet publish -c Release" ) );

            // Put the whole folder including the Dockerfile inside a tar. This is what the API understands
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ && rm showcaseserver.tar" ) ); // Remove old archive
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ToCompileComplexWeb && tar -cvf ../showcaseserver.tar *" ) );

        }

        public void Stage_BuildingImage()
        {
            // Filestream from the tar ball
            FileStream l_tarStream = new FileStream( @"/home/greg/SyncThing/Personal/Projects/OS/WebDocker/showcaseserver.tar", FileMode.Open );

            // Delete the old image
            Checker.s_dockerClient.Images.DeleteImageAsync( "showcase", new ImageDeleteParameters() { Force = true }, CancellationToken.None ).Wait();

            // Build parameters for the image
            List< string > l_tags = new List< string >{ "showcase" };
            Dictionary< string, string > l_buildArgs = new Dictionary< string, string >{};
            ImageBuildParameters l_imageBuildParams = new ImageBuildParameters()
            {
                Tags = l_tags,
                BuildArgs = l_buildArgs,
            };

            // Build the image
            Checker.s_dockerClient.Images.BuildImageFromDockerfileAsync( l_tarStream, l_imageBuildParams, CancellationToken.None ).Wait();

            // Close the file stream
            l_tarStream.Close();

            // Checker.s_dockerClient.Images.TagImageAsync( "greg:complexweb", new ImageTagParameters(), CancellationToken.None );

            Thread.Sleep( 500 );
        }

        public void Stage_CreatingContainer()
        {
            // Creation parameters for the container
            CreateContainerParameters l_createParams = new CreateContainerParameters()
            {
                // Expose port 5000 for the webserver
                ExposedPorts = new Dictionary< string, EmptyStruct >
                {
                    {
                        "5000", default( EmptyStruct )
                    },
                    // 8888 is for sending commands to our client
                    {
                        "8888", default( EmptyStruct )
                    },
                },
                HostConfig = new HostConfig()
                {
                    PortBindings = new Dictionary< string, IList< PortBinding > >
                    {
                        // Bring that internal 5000 port out to the 8080 external port
                        { "5000", new List< PortBinding >{ new PortBinding{ HostPort = "8090" } } },
                        { "8888", new List< PortBinding >{ new PortBinding{ HostPort = "8888" } } }
                    },
                },

                // The image that was just built, use that!
                Image = "showcase"
            };

            // Create the container
            Checker.s_createTask = Checker.s_dockerClient.Containers.CreateContainerAsync( l_createParams, CancellationToken.None );
            Checker.s_createTask.Wait();
        }
        
        public void Stage_StartingContainer()
        {
            // Get the result from the last stage (Creating the container) - This will allow us to get the ID later
            CreateContainerResponse l_response = Checker.s_createTask.Result;

            // Start the container using the ID from the container that we created before
            Checker.s_dockerClient.Containers.StartContainerAsync( l_response.ID, new ContainerStartParameters() );

            // Done!
            Console.WriteLine ( "-----\n-----\n-----\nCOMPLETED" );
        }

        public void OldOnPost()
        {
            // RefSpecs for args on Fetch
            Remote l_remote = Checker.s_repoASP.Network.Remotes.FirstOrDefault();
            IEnumerable< string > l_refSepcs = l_remote.FetchRefSpecs.Select( x => x.Specification );

            // Fetch any changes that have occured on the remote repo
            // string l_logMsg = "";
            // Commands.Fetch( Checker.s_repoASP, l_remote.Name, l_refSepcs, null, l_logMsg );
            // .Network.Fetch( "https://github.com/GregOnGit/ShowcaseServer", l_refSepcs );

            // Update the local branch to match remote
            Branch l_branch = Checker.s_repoASP.Branches[ "main" ];
            // Commands.Checkout( Checker.s_repoASP, l_branch );
            PullOptions l_pullOptions = new PullOptions();
            l_pullOptions.FetchOptions = new FetchOptions();
            l_pullOptions.MergeOptions = new MergeOptions()
            {

            };
            Signature l_signature = new Signature ( "Greg", "gregoryonbusiness@gmail.com", new DateTimeOffset() );
            Commands.Pull( Checker.s_repoASP, l_signature, l_pullOptions );
        
            // Now that is all pulled, rebuild the project and restart the container
            Task < IList< ContainerListResponse > > l_cTask = Checker.s_dockerClient.Containers.ListContainersAsync( new ContainersListParameters(){ Limit = 10, } );
        
            // Wait until task is completed before proceeding
            /*if( !l_cTask.IsCompleted )
            {
                while( !l_cTask.IsCompleted )
                {
                    Thread.Sleep( 5 );
                }
            }*/

            IList< ContainerListResponse > l_containerList = l_cTask.Result;
            
            // Kill all web image containers
            // string l_cID = "";
            foreach( ContainerListResponse l_clr in l_containerList )
            {
                /*if( l_clr.Image == "greg:complexweb" )
                {
                    // l_cID = l_clr.ImageID;
                    Checker.s_dockerClient.Containers.StopContainerAsync( l_clr.ID, new ContainerStopParameters(), CancellationToken.None ).Wait();
                    Checker.s_dockerClient.Containers.RemoveContainerAsync( l_clr.ID, new ContainerRemoveParameters(){ Force = true }, CancellationToken.None ).Wait();
                }*/

                Checker.s_dockerClient.Containers.StopContainerAsync( l_clr.ID, new ContainerStopParameters(), CancellationToken.None ).Wait();
                Checker.s_dockerClient.Containers.RemoveContainerAsync( l_clr.ID, new ContainerRemoveParameters(){ Force = true }, CancellationToken.None ).Wait();
            }

            // Now onto the rebuilding part
            // ---

            // Delete all old files to refresh the build
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ && rm -rf ToCompileComplexWeb/* " ) );
            
            // Copy everything from the updated local repo
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ && cp -rT ShowcaseServer ToCompileComplexWeb/" ) );

            // Clean out old object files from last build
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ && rm -rf ToCompileComplexWeb/bin/* && rm -rf ToCompileComplexWeb/obj/*" ) );

            // Clean from last build
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ToCompileComplexWeb && dotnet clean" ) );
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ToCompileComplexWeb && dotnet restore" ) );

            // Rebuild the whole project
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ToCompileComplexWeb && dotnet build -c Release" ) );

            // Build and Publish a release version of the project so all dependencies are included with the binary
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ToCompileComplexWeb && dotnet publish -c Release" ) );

            // Put the whole folder including the Dockerfile inside a tar. This is what the API understands
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ && rm showcaseserver.tar" ) ); // Remove old archive
            Console.WriteLine( Checker.Bash( "cd /home/greg/SyncThing/Personal/Projects/OS/WebDocker/ToCompileComplexWeb && tar -cvf ../showcaseserver.tar *" ) );

            // Now build a docker image
            // ---

            // Filestream from the tar ball
            FileStream l_tarStream = new FileStream( @"/home/greg/SyncThing/Personal/Projects/OS/WebDocker/showcaseserver.tar", FileMode.Open );

            // Delete the old image
            Checker.s_dockerClient.Images.DeleteImageAsync( "showcase", new ImageDeleteParameters() { Force = true }, CancellationToken.None ).Wait();
            

            Task<IList<ImagesListResponse>> l_imageListTask = Checker.s_dockerClient.Images.ListImagesAsync( new ImagesListParameters() { All = true }, CancellationToken.None );
            l_imageListTask.Wait();

            /*foreach( ImagesListResponse l_imageInfo in l_imageListTask.Result )
            {
                Checker.s_dockerClient.Images.DeleteImageAsync( l_imageInfo.ID, new ImageDeleteParameters(), CancellationToken.None ).Wait();
            }*/

            // Build parameters for the image
            List< string > l_tags = new List< string >{ "showcase" };
            Dictionary< string, string > l_buildArgs = new Dictionary< string, string >
            { 
                // { "-d", "" }, // Run detached
                // { "-p", "8080:5000" } // publish the internal port 5000 to external port 8080, so it can be accessed from the "ouside world"
            };
            ImageBuildParameters l_imageBuildParams = new ImageBuildParameters()
            {
                Tags = l_tags,
                BuildArgs = l_buildArgs,
                // Dockerfile = @"/home/greg/SyncThing/Personal/Projects/OS/WebDocker/ComplexWeb/Dockerfile",
                // Labels = new Dictionary< string, string>(){ { "greg", "greg" } },
            };

            // Build the image
            Checker.s_dockerClient.Images.BuildImageFromDockerfileAsync( l_tarStream, l_imageBuildParams, CancellationToken.None ).Wait();

            // Close the file stream
            l_tarStream.Close();

            // Checker.s_dockerClient.Images.TagImageAsync( "greg:complexweb", new ImageTagParameters(), CancellationToken.None );

            Thread.Sleep( 500 );

            // Update the image list
            // Checker.s_dockerClient.Images,
            // Checker.s_dockerClient = new DockerClientConfiguration( new Uri( "http://127.0.0.1:4243" ) ).CreateClient();

            // Creation parameters for the container
            CreateContainerParameters l_createParams = new CreateContainerParameters()
            {
                // ExposedPorts = new IDictionary<string, EmptyStruct>
                ExposedPorts = new Dictionary< string, EmptyStruct >
                {
                    {
                        "5000", default( EmptyStruct )
                    }
                },
                HostConfig = new HostConfig()
                {
                    PortBindings = new Dictionary< string, IList< PortBinding > >
                    {
                        { "5000", new List< PortBinding >{ new PortBinding{ HostPort = "8080" } } }
                    },
                    // PublishAllPorts = true
                },
                // Image = "greg:complexweb",
                Image = "showcase"
            };
            
            // Checker.s_dockerClient.Images.TagImageAsync( )

            // Create the container
            Task< CreateContainerResponse> l_createTask = Checker.s_dockerClient.Containers.CreateContainerAsync( l_createParams, CancellationToken.None );
            l_createTask.Wait();

            CreateContainerResponse l_response = l_createTask.Result;
            // Start Parameters for the container
            ContainerStartParameters l_startParams = new ContainerStartParameters()
            {
                
            };

            // Start the container
            Checker.s_dockerClient.Containers.StartContainerAsync( l_response.ID, l_startParams );

            Console.WriteLine ( "-----\n-----\n-----\nCOMPLETED" );
        }
    }
}