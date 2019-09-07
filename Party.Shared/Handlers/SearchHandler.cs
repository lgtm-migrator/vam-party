﻿using System;
using System.Collections.Generic;
using System.Linq;
using Party.Shared.Models;
using Party.Shared.Resources;

namespace Party.Shared.Handlers
{
    public class SearchHandler
    {
        private readonly string[] _trustedDomains;

        public SearchHandler(string[] trustedDomains)
        {
            _trustedDomains = trustedDomains ?? throw new ArgumentNullException(nameof(trustedDomains));
        }

        public IEnumerable<SearchResult> Search(Registry registry, SavesMap saves, string query)
        {
            if (registry is null) throw new ArgumentNullException(nameof(registry));
            if (registry?.Scripts is null) throw new ArgumentException("registry does not have any scripts", nameof(registry));

            foreach (var package in registry.Scripts)
            {
                if (!string.IsNullOrEmpty(query))
                {
                    if (!MatchesQuery(package, query))
                    {
                        continue;
                    }
                }
                var trusted = package.Versions?
                    .SelectMany(v => v.Files)
                    .Where(f => f.Url != null && !f.Ignore)
                    .All(f => _trustedDomains.Any(t => f.Url.StartsWith(t)))
                    ?? false;
                Script[] scripts = null;
                Scene[] scenes = null;
                if (saves != null && package.Versions != null)
                {
                    // TODO: We should consider all files from a specific version of plugin together
                    var allFilesFromAllVersions = package.Versions
                        .SelectMany(v => v.Files ?? new SortedSet<RegistryFile>());
                    scripts = allFilesFromAllVersions
                        .Where(regFile => regFile.Hash?.Value != null)
                        .SelectMany(regFile => saves.Scripts.Where(localScript => localScript.Hash == regFile.Hash.Value))
                        .Distinct()
                        .ToArray();
                    scenes = scripts
                        .SelectMany(s => s.Scenes)
                        .Distinct()
                        .ToArray();
                }
                yield return new SearchResult
                {
                    Package = package,
                    Trusted = trusted,
                    Scripts = scripts,
                    Scenes = scenes
                };
            }
        }

        private bool MatchesQuery(RegistryScript package, string query)
        {
            if (package.Name?.Contains(query, StringComparison.InvariantCultureIgnoreCase) ?? false)
            {
                return true;
            }
            if (package.Author?.Contains(query, StringComparison.InvariantCultureIgnoreCase) ?? false)
            {
                return true;
            }
            if (package.Description?.Contains(query, StringComparison.InvariantCultureIgnoreCase) ?? false)
            {
                return true;
            }
            if (package.Tags?.Any(tag => tag.Contains(query, StringComparison.InvariantCultureIgnoreCase)) ?? false)
            {
                return true;
            }
            return false;
        }
    }
}
