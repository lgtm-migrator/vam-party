﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Party.Shared.Models;
using Party.Shared.Models.Local;
using Party.Shared.Serializers;

namespace Party.Shared.Handlers
{
    public class UpgradeSceneHandler
    {
        private readonly ISceneSerializer _serializer;
        private readonly IFoldersHelper _folders;

        public UpgradeSceneHandler(ISceneSerializer serializer, IFoldersHelper folders)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _folders = folders ?? throw new ArgumentNullException(nameof(folders));
        }

        public async Task<int> UpgradeSceneAsync(LocalSceneFile scene, LocalScriptFile local, LocalPackageInfo after)
        {
            var changes = DetermineChanges(local, after);
            if (changes.Count == 0) return 0;

            return await ApplyChanges(scene, changes).ConfigureAwait(false);
        }

        private Dictionary<string, string> DetermineChanges(LocalScriptFile local, LocalPackageInfo after)
        {
            if (local is LocalScriptListFile)
                throw new NotImplementedException(".cslist is not yet supported for upgrades");

            var changes = new Dictionary<string, string>();
            if (after.Files.Length == 1)
                AddChanges(changes, local.FullPath, after.Files[0].FullPath);
            else
                throw new NotImplementedException("No automatic strategy implement for this upgrade type");

            return changes;
        }

        private async Task<int> ApplyChanges(LocalSceneFile scene, Dictionary<string, string> changes)
        {
            var counter = 0;
            var json = await _serializer.DeserializeAsync(scene.FullPath).ConfigureAwait(false);
            foreach (var plugins in json.Atoms.SelectMany(a => a.Plugins).GroupBy(p => p.Path))
            {
                if (changes.TryGetValue(plugins.Key, out var after))
                {
                    foreach (var plugin in plugins)
                    {
                        plugin.Path = after;
                        counter++;
                    }
                }
            }
            if (counter > 0)
                await _serializer.SerializeAsync(json, scene.FullPath).ConfigureAwait(false);
            return counter;
        }

        private void AddChanges(IDictionary<string, string> changes, string before, string after)
        {
            after = _folders.ToRelative(after).Replace("\\", "/");
            changes.Add(_folders.ToRelative(before).Replace("\\", "/"), after);
            changes.Add(Path.GetFileName(before), after);
        }
    }
}