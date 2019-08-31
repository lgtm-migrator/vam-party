﻿using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Party.Shared.Exceptions;
using Party.Shared.Models;
using Party.Shared.Utils;

namespace Party.Shared.Handlers
{
    public class AddUrlsToRegistryHandler
    {
        private readonly HttpClient _http;

        public AddUrlsToRegistryHandler(HttpClient _http)
        {
            this._http = _http ?? throw new ArgumentNullException(nameof(_http));
        }

        public async Task<(RegistryScript script, RegistryScriptVersion version)> AddScriptVersionAsync(Registry registry, string name, Uri url)
        {
            if (registry is null) throw new ArgumentNullException(nameof(registry));
            if (url is null) throw new ArgumentNullException(nameof(url));

            var script = registry.GetOrCreateScript(name);
            var version = script.CreateVersion();
            version.Files.Add(await GetFileFromUrl(url));

            return (script, version);
        }

        private async Task<RegistryFile> GetFileFromUrl(Uri url)
        {
            var filename = Path.GetFileName(url.LocalPath);
            if (string.IsNullOrWhiteSpace(filename)) throw new UserInputException($"Url '{url}' does not contain a filename.");
            filename = HttpUtility.UrlDecode(filename);
            if (!filename.EndsWith(".cs")) throw new UserInputException($"Url {url}' does not end with '.cs'");

            using var response = await _http.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var hash = Hashing.GetHash(lines);
            return new RegistryFile
            {
                Filename = filename,
                Url = url.ToString(),
                Hash = new RegistryFileHash
                {
                    Type = Hashing.Type,
                    Value = hash
                }
            };
        }
    }
}