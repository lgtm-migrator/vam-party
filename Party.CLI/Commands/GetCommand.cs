﻿using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Party.Shared;
using Party.Shared.Exceptions;
using Party.Shared.Models;

namespace Party.CLI.Commands
{
    public class GetCommand : CommandBase
    {
        public static Command CreateCommand(IConsoleRenderer renderer, PartyConfiguration config, IPartyController controller)
        {
            var command = new Command("get", "Downloads a package (script) into the saves folder");
            AddCommonOptions(command);
            command.AddArgument(new Argument<string>("package", null) { Arity = ArgumentArity.ExactlyOne });
            command.AddOption(new Option("--version", "Choose a specific version to install") { Argument = new Argument<string>() });
            command.AddOption(new Option("--noop", "Do not install, just check what it will do"));
            command.AddOption(new Option("--force", "Install even if hashes don't match, or files will be missing") { Argument = new Argument<bool>() });

            command.Handler = CommandHandler.Create<GetArguments>(async args =>
            {
                await new GetCommand(renderer, config, args.VaM, controller).ExecuteAsync(args);
            });
            return command;
        }

        public class GetArguments : CommonArguments
        {
            public string Package { get; set; }
            public string Version { get; set; }
            public bool Noop { get; set; }
            public bool Force { get; set; }
        }

        public GetCommand(IConsoleRenderer renderer, PartyConfiguration config, DirectoryInfo vam, IPartyController controller)
            : base(renderer, config, vam, controller)
        {
        }

        private async Task ExecuteAsync(GetArguments args)
        {
            Controller.HealthCheck();

            if (string.IsNullOrWhiteSpace(args.Package))
            {
                throw new UserInputException("You must specify a package");
            }

            var registry = await Controller.GetRegistryAsync().ConfigureAwait(false);

            var registryPackage = registry.Scripts.FirstOrDefault(s => s.Name.Equals(args.Package, StringComparison.InvariantCultureIgnoreCase));

            if (registryPackage == null)
            {
                throw new RegistryException($"Package not found: '{args.Package}'");
            }

            var registryPackageVersion = registryPackage.GetLatestVersion();
            if (!string.IsNullOrEmpty(args.Version))
            {
                registryPackageVersion = registryPackage.Versions.FirstOrDefault(p => p.Version.ToString().Equals(args.Version));
                if (registryPackageVersion == null)
                {
                    throw new RegistryException($"Package version not found: '{args.Package}' version '{args.Version}'");
                }
            }

            var notBundled = registryPackageVersion.Files.Where(f => f.Url == null && f.LocalPath != null).Select(f => (file: f, exists: Controller.Exists(f.LocalPath))).ToArray();
            if (!args.Force && notBundled.Any(file => !file.exists))
            {
                Renderer.WriteLine($"Some files are not available for download and must be downloaded at {registryPackage.Homepage ?? registryPackage.Repository ?? "(no link provided)"}");
                foreach (var file in notBundled)
                {
                    Renderer.Write($"  - {file.file.LocalPath}");
                    if (file.exists)
                        Renderer.Write($" [exists]", ConsoleColor.Green);
                    else
                        Renderer.Write($" [missing]", ConsoleColor.Red);
                    Renderer.WriteLine();
                }
                return;
            }

            var filesStatuses = await Controller.GetInstalledPackageInfoAsync(registryPackage.Name, registryPackageVersion);

            var distinctStatuses = filesStatuses.DistinctStatuses();

            ValidateStatuses(distinctStatuses);

            if (args.Noop)
            {
                Renderer.WriteLine($"Package {registryPackage.Name} v{registryPackageVersion.Version} by {registryPackage.Author ?? "?"}");
                Renderer.WriteLine($"Files will be downloaded in {filesStatuses.InstallFolder}:");
                foreach (var file in filesStatuses.Files)
                {
                    Renderer.WriteLine($"- Path: {Controller.GetRelativePath(file.Path, filesStatuses.InstallFolder)}");
                    Renderer.WriteLine($"  Hash: {file.RegistryFile.Hash.Value} ({file.RegistryFile.Hash.Type})");
                    Renderer.WriteLine($"  Url:  {file.RegistryFile.Url}");
                }
                return;
            }

            var installResult = await Controller.InstallPackageAsync(filesStatuses, args.Force);

            Renderer.WriteLine($"Installed package {registryPackage.Name} v{registryPackageVersion.Version} by {registryPackage.Author ?? "?"}");
            Renderer.WriteLine($"Files downloaded in {filesStatuses.InstallFolder}:");
            foreach (var file in installResult.Files)
            {
                Renderer.WriteLine($"- {Controller.GetRelativePath(file.Path, filesStatuses.InstallFolder)}");
            }
        }

        private void ValidateStatuses(InstalledPackageInfoResult.FileStatus[] distinctStatuses)
        {
            if (distinctStatuses.Length > 1)
            {
                throw new PackageInstallationException("The installed plugin has been either partially installed or was modified. Try deleting the installed package folder and try again.");
            }

            if (distinctStatuses.Length == 0)
            {
                throw new PackageInstallationException("No files were found in this package.");
            }

            switch (distinctStatuses.FirstOrDefault())
            {
                case InstalledPackageInfoResult.FileStatus.Installed:
                    throw new UserInputException("Plugin already installed");
                case InstalledPackageInfoResult.FileStatus.HashMismatch:
                    throw new PackageInstallationException("Installed plugin does not match the registry version. Did you modified it?");
                default:
                    return;
            }
        }
    }
}
