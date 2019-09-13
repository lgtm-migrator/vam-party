﻿using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Party.Shared;
using Party.Shared.Models;
using Party.Shared.Models.Registries;
using Party.Shared.Utils;

namespace Party.CLI.Commands
{
    public abstract class CommandBase
    {
        private static PartyConfiguration GetConfig(PartyConfiguration config, DirectoryInfo vam)
        {
            if (vam != null)
            {
                config.VirtAMate.VirtAMateInstallFolder = vam.FullName;
            }
            return config;
        }

        protected IConsoleRenderer Renderer { get; }
        protected PartyConfiguration Config { get; }
        protected IPartyController Controller { get; }

        protected CommandBase(IConsoleRenderer renderer, PartyConfiguration config, IPartyController controller, CommonArguments args)
        {
            Renderer = renderer;
            Config = GetConfig(config, args.VaM);
            Controller = controller;
            Controller.ChecksEnabled = args.Force;
        }

        protected static void AddCommonOptions(Command command)
        {
            command.AddOption(new Option("--vam", "Specify the Virt-A-Mate install folder") { Argument = new Argument<DirectoryInfo>().ExistingOnly() });
            command.AddOption(new Option("--force", "Ignores most security checks and health checks"));
        }

        public abstract class CommonArguments
        {
            public DirectoryInfo VaM { get; set; }
            public bool Force { get; set; }
        }

        protected async Task<(SavesMap, Registry)> GetSavesAndRegistryAsync(string filter = null)
        {
            // NOTE: When specifying --noop to status, it puts --noop in a filter, and returns nothing. Try to avoid that, or at least specify why nothing has been returned?
            // TODO: This should be done in the Controller

            Renderer.WriteLine("Analyzing the saves folder and gettings the packages list from the registry, please wait...");

            var isFilterPackage = PackageFullName.TryParsePackage(filter, out var filterPackage);
            var filterPath = !isFilterPackage;

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var registryTask = Metrics.Measure(() => Controller.GetRegistryAsync());
            // TODO: If the item is a package (no extension), resolve it to a path (if the plugin was not downloaded, throw)
            // TODO: When the filter is a scene, mark every script that was not referenced by that scene as not safe for cleanup; also remove them for display
            var savesTask = Metrics.Measure(() => Controller.GetSavesAsync(filterPath ? Path.GetFullPath(filter) : null));

            await Task.WhenAll();

            var (registry, registryTiming) = await registryTask;
            var (saves, savesTiming) = await savesTask;

            stopwatch.Stop();

            Renderer.WriteLine($"Scanned {saves.Scenes?.Length ?? 0} scenes and {saves.Scripts?.Length ?? 0} scripts in {savesTiming.TotalSeconds:0.00}s, and downloaded registry in {registryTiming.TotalSeconds:0.00}s. Total wait time: {stopwatch.Elapsed.TotalSeconds:0.00}s");

            // TODO: Put in controller
            if (isFilterPackage)
            {
                // TODO: Filter by type
                var packageHashes = new HashSet<string>(registry.Packages.Get(filterPackage.Type).Where(s => filterPackage.Name.Equals(s.Name, StringComparison.InvariantCultureIgnoreCase)).SelectMany(s => s.Versions).SelectMany(v => v.Files).Select(f => f.Hash.Value).Distinct());
                saves.Scripts = saves.Scripts.Where(s =>
                {
                    if (s is ScriptList scriptList)
                        return new[] { scriptList.Hash }.Concat(scriptList.Scripts.Select(c => c.Hash)).All(h => packageHashes.Contains(h));
                    else
                        return packageHashes.Contains(s.Hash);
                }).ToArray();
            }

            return (saves, registry);
        }

        protected void PrintWarnings(bool details, SavesError[] logs)
        {
            if (logs == null || logs.Length == 0) return;

            var grouped = logs.GroupBy(l => l.Level).ToDictionary(g => g.Key, g => g.ToArray());
            grouped.TryGetValue(SavesErrorLevel.Error, out var errors);
            grouped.TryGetValue(SavesErrorLevel.Warning, out var warnings);

            if (details)
            {
                if (errors != null)
                {
                    using (Renderer.WithColor(ConsoleColor.Red))
                    {
                        Renderer.WriteLine("Errors:");
                        foreach (var error in errors)
                        {
                            Renderer.Error.WriteLine($"  {Controller.GetDisplayPath(error.File)}: {error.Error}");
                        }
                    }
                    Renderer.WriteLine();
                }

                if (warnings != null)
                {
                    using (Renderer.WithColor(ConsoleColor.Yellow))
                    {
                        Renderer.WriteLine("Warnings:");
                        foreach (var error in warnings)
                        {
                            Renderer.Error.WriteLine($"  {Controller.GetDisplayPath(error.File)}: {error.Error}");
                        }
                    }
                }
                Renderer.WriteLine();
            }
            else
            {
                using (Renderer.WithColor(ConsoleColor.Yellow))
                {
                    Renderer.Error.WriteLine($"There were {warnings?.Length ?? 0} warnings and {errors?.Length ?? 0} errors in the saves folder. Run with --warnings to print them.");
                }
            }
        }

        protected string Pluralize(int count, string singular, string plural)
        {
            if (count == 1)
            {
                return $"{count} {singular}";
            }
            else
            {
                return $"{count} {plural}";
            }
        }
    }
}
