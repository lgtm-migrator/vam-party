﻿using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Party.Shared;
using Party.Shared.Exceptions;
using Party.Shared.Results;
using Party.Shared.Serializers;

namespace Party.CLI.Commands
{
    public class PublishCommand : CommandBase
    {
        public static Command CreateCommand(IRenderer renderer, PartyConfiguration config, IPartyController controller)
        {
            var command = new Command("publish", "Prepares files for publishing");
            AddCommonOptions(command);
            command.AddArgument(new Argument<string>("package-path", null));
            command.AddOption(new Option("--package-name", "The name of your package") { Argument = new Argument<string>() });
            command.AddOption(new Option("--package-version", "The version of your package") { Argument = new Argument<string>() });
            command.AddOption(new Option("--registry", "Path the the index.json file of your locally cloned registry") { Argument = new Argument<FileInfo>().ExistingOnly() });
            // TODO: Add the different fields too (author, name, etc.)

            command.Handler = CommandHandler.Create(async (DirectoryInfo saves, string packagePath, string packageName, string packageVersion, FileInfo registry) =>
            {
                await new PublishCommand(renderer, config, saves, controller).ExecuteAsync(packagePath, packageName, packageVersion, registry);
            });
            return command;
        }

        public PublishCommand(IRenderer renderer, PartyConfiguration config, DirectoryInfo saves, IPartyController controller) : base(renderer, config, saves, controller)
        {
        }

        private async Task ExecuteAsync(string input, string packageName, string packageVersion, FileInfo registryJson)
        {
            Registry registry;
            if (registryJson != null)
            {
                if (registryJson.Name != "index.json")
                    throw new UserInputException("Please specify the path to your locally cloned index.json file");

                registry = await Controller.GetRegistryAsync(registryJson.FullName);
            }
            else
            {
                registry = await Controller.GetRegistryAsync();
            }

            var name = (packageName ?? await Renderer.AskAsync("Package Name: ")).ToLowerInvariant();
            // TODO: Validate
            var script = registry.Scripts?.FirstOrDefault(s => s.Name?.Equals(name, StringComparison.InvariantCultureIgnoreCase) ?? false);
            if (script != null)
            {
                Renderer.WriteLine($"This package already exists (by {script.Author?.Name ?? "Anonymous User"}), a new version will be added to it.");
                if (script.Versions != null)
                {
                    Renderer.WriteLine("Existing versions:");
                    foreach (var existingVersion in script.Versions)
                    {
                        Renderer.WriteLine($"- {existingVersion.Version}");
                    }
                }
            }
            else
            {
                Renderer.WriteLine("Looks like a new package in the registry! Please provide some information about this new package, or press CTRL+C if you want to abort.");
                var author = new RegistryScriptAuthor
                {
                    Name = await Renderer.AskAsync("Author Name: ")
                };
                var existingAuthor = registry.Scripts.Where(s => s.Author != null).Select(s => s.Author).FirstOrDefault(a => a.Name.Equals(author.Name, StringComparison.InvariantCultureIgnoreCase));
                if (!string.IsNullOrEmpty(existingAuthor.Profile))
                    author.Profile = await Renderer.AskAsync($"Author Profile URL ({existingAuthor.Profile}): ") ?? existingAuthor.Profile;
                else
                    author.Profile = await Renderer.AskAsync("Author Profile URL ");
                script = new RegistryScript
                {
                    Name = name,
                    Author = author,
                    Description = await Renderer.AskAsync("Description: "),
                    Tags = (await Renderer.AskAsync("Tags (comma-separated list): ")).Split(',').Select(x => x.Trim()).Where(x => x != "").ToList(),
                    Homepage = await Renderer.AskAsync("Package Homepage URL: "),
                    Repository = await Renderer.AskAsync("Package Repository URL: ")
                };
            }

            var version = new RegistryScriptVersion
            {
                Version = packageVersion ?? await Renderer.AskAsync("Package Version (0.0.0): ")
            };

            await Controller.AddToRegistry(registry, script, version, input).ConfigureAwait(false);

            foreach (var file in version.Files)
            {
                file.Url = await Renderer.AskAsync($"{file.Filename} URL: ");
            }

            var serializer = new RegistrySerializer();
            if (registryJson != null)
            {
                Controller.SaveToFile(serializer.Serialize(registry), registryJson.FullName);
                Renderer.WriteLine($"JSON written to {registryJson.FullName}");
            }
            else
            {
                Renderer.WriteLine("JSON Template:");
                Renderer.WriteLine(serializer.Serialize(script));
            }
        }
    }
}
