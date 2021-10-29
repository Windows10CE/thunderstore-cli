using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ThunderstoreCLI.API.Models;
using static Crayon.Output;

namespace ThunderstoreCLI.Commands
{
    public static class PublishCommand
    {
        public static int Run(PublishOptions options, Config.Config config)
        {
            var configPath = config.GetProjectConfigPath();
            if (!File.Exists(configPath))
            {
                Console.WriteLine(Red($"ERROR: Configuration file not found, looked from: {White(Dim(configPath))}"));
                Console.WriteLine(Red("A project configuration file is required for the publish command."));
                Console.WriteLine(Red("You can initialize one with the 'init' command."));
                Console.WriteLine(Red("Exiting"));
                return 1;
            }

            string packagePath = "";
            if (!string.IsNullOrWhiteSpace(options.File))
            {
                var filePath = Path.GetFullPath(options.File);
                if (!File.Exists(filePath))
                {
                    Console.WriteLine(Red($"ERROR: The provided file does not exist."));
                    Console.WriteLine(Red($"Searched path: {White(Dim(filePath))}"));
                    Console.WriteLine(Red("Exiting"));
                    return 1;
                }
                packagePath = filePath;
            }
            else
            {
                var exitCode = BuildCommand.DoBuild(config);
                if (exitCode > 0)
                {
                    return exitCode;
                }
                packagePath = config.GetBuildOutputFile();
            }

            return PublishFile(options, config, packagePath);
        }

        public static int PublishFile(PublishOptions options, Config.Config config, string filepath)
        {
            Console.WriteLine();
            Console.WriteLine($"Publishing {Cyan(filepath)}");
            Console.WriteLine();

            if (!File.Exists(filepath))
            {
                Console.WriteLine(Red($"ERROR: File selected for publish was not found"));
                Console.WriteLine(Red($"Looked from: {White(Dim(filepath))}"));
                Console.WriteLine(Red("Exiting"));
                return 1;
            }
            
            using var client = new HttpClient();

            var fileUuid = config.Api.FullUploadMedia(filepath, client);
            
            var publishResponse = client.Send(config.Api.SubmitPackage(fileUuid));
            
            if (publishResponse.StatusCode == HttpStatusCode.OK)
            {
                Console.WriteLine(Blue($"Successfully published {config.PackageMeta.Namespace}-{config.PackageMeta.Name}"));
                return 0;
            }
            else
            {
                Console.WriteLine(Red($"ERROR: Unexpected response from the server"));
                using var responseReader = new StreamReader(publishResponse.Content.ReadAsStream());
                Console.WriteLine(Red($"Details:"));
                Console.WriteLine($"Status code: {publishResponse.StatusCode:D} {publishResponse.StatusCode}");
                Console.WriteLine(Dim(responseReader.ReadToEnd()));
                return 1;
            }
        }
    }
}
