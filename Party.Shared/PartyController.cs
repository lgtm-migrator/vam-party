﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Party.Shared.Handlers;
using Party.Shared.Models;

namespace Party.Shared
{
    public interface IPartyController
    {
        Task<Registry> GetRegistryAsync(params string[] registries);
        Task<SavesMap> GetSavesAsync();
        Task<(RegistryScript script, RegistryScriptVersion version)> AddToRegistry(Registry registry, string name, string path);
        IEnumerable<SearchResult> Search(Registry registry, SavesMap saves, string query, bool showUsage);
        Task<InstalledPackageInfoResult> GetInstalledPackageInfoAsync(string name, RegistryScriptVersion version);
        Task<InstalledPackageInfoResult> InstallPackageAsync(InstalledPackageInfoResult info);
        RegistrySavesMatch[] MatchSavesToRegistry(SavesMap saves, Registry registry);
        string GetRelativePath(string fullPath);
        string GetRelativePath(string fullPath, string parentPath);
        void SaveToFile(string data, string path);
    }

    public class PartyController : IPartyController
    {
        private static string Version { get; } = typeof(PartyController).Assembly.GetName().Version.ToString();
        private readonly PartyConfiguration _config;
        private readonly HttpClient _http;
        private readonly IFileSystem _fs;

        public PartyController(PartyConfiguration config)
        {
            _config = config;
            _fs = new FileSystem();
            _http = new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Party", Version));
        }

        public Task<Registry> GetRegistryAsync(params string[] registries)
        {
            return new RegistryHandler(_http, _config.Registry.Urls).AcquireAsync(registries);
        }

        public Task<SavesMap> GetSavesAsync()
        {
            return new SavesResolverHandler(_fs, _config.VirtAMate.SavesDirectory, _config.Scanning.Ignore).AnalyzeSaves();
        }

        public Task<(RegistryScript script, RegistryScriptVersion version)> AddToRegistry(Registry registry, string name, string path)
        {
            return new AddToRegistryHandler(_config.VirtAMate.SavesDirectory, _fs).AddScriptVersionAsync(registry, name, path);
        }

        public IEnumerable<SearchResult> Search(Registry registry, SavesMap saves, string query, bool showUsage)
        {
            return new SearchHandler(_config).Search(registry, saves, query, showUsage);
        }

        public Task<InstalledPackageInfoResult> GetInstalledPackageInfoAsync(string name, RegistryScriptVersion version)
        {
            return new PackageStatusHandler(_config, _fs).GetInstalledPackageInfoAsync(name, version);
        }

        public Task<InstalledPackageInfoResult> InstallPackageAsync(InstalledPackageInfoResult info)
        {
            return new InstallPackageHandler(_fs, _http).InstallPackageAsync(info);
        }
        public RegistrySavesMatch[] MatchSavesToRegistry(SavesMap saves, Registry registry)
        {
            return new RegistrySavesMatchHandler().Match(saves, registry);
        }

        public string GetRelativePath(string fullPath)
        {
            return GetRelativePath(fullPath, _config.VirtAMate.SavesDirectory);
        }

        public string GetRelativePath(string fullPath, string parentPath)
        {
            if (!fullPath.StartsWith(parentPath))
            {
                throw new UnauthorizedAccessException($"Only paths within the saves directory are allowed: '{fullPath}'");
            }

            return fullPath.Substring(parentPath.Length).TrimStart(Path.DirectorySeparatorChar);
        }

        public void SaveToFile(string data, string path)
        {
            _fs.File.WriteAllText(path, data);
        }
    }
}
