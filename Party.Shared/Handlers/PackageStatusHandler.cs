﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Party.Shared.Models;
using Party.Shared.Models.Registries;
using Party.Shared.Utils;

namespace Party.Shared.Handlers
{
    public class PackageStatusHandler
    {
        private readonly IFileSystem _fs;
        private readonly IFoldersHelper _folders;

        public PackageStatusHandler(IFileSystem fs, IFoldersHelper folders)
        {
            _fs = fs ?? throw new ArgumentNullException(nameof(fs));
            _folders = folders ?? throw new ArgumentNullException(nameof(folders));
        }

        public async Task<LocalPackageInfo> GetInstalledPackageInfoAsync(RegistryPackage package, RegistryPackageVersion version)
        {
            var basePath = _folders.GetDirectory(package.Type);
            var installPath = Path.Combine(basePath, package.Author ?? "Anonymous", package.Name, version.Version);
            var files = new List<InstalledFileInfo>();

            foreach (var file in version.Files.Where(f => !f.Ignore))
            {
                if (file.Filename != null)
                    files.Add(await GetPackageFileInfo(installPath, file).ConfigureAwait(false));
                else if (file.LocalPath != null)
                    files.Add(GetLocalFileInfo(file));
            }

            return new LocalPackageInfo
            {
                InstallFolder = basePath,
                Files = files.ToArray(),
                Corrupted = files.Any(f => f.Status == FileStatus.HashMismatch),
                Installed = files.All(f => f.Status == FileStatus.Installed),
                Installable = files.All(f => f.Status != FileStatus.NotInstallable && f.Status != FileStatus.HashMismatch)
            };
        }

        private InstalledFileInfo GetLocalFileInfo(RegistryFile file)
        {
            // TODO: Replace this with a path manager
            var filePath = Path.Combine(_folders.RelativeToVam(file.Filename));
            return new InstalledFileInfo
            {
                Path = filePath,
                RegistryFile = file,
                Status = _fs.File.Exists(filePath) ? FileStatus.Installed : FileStatus.NotInstallable
            };
        }

        private async Task<InstalledFileInfo> GetPackageFileInfo(string installPath, RegistryFile file)
        {
            var filePath = Path.Combine(installPath, file.Filename);
            var fileInfo = new InstalledFileInfo
            {
                Path = filePath,
                RegistryFile = file
            };

            if (_fs.File.Exists(filePath))
            {
                var hash = await Hashing.GetHashAsync(_fs, filePath);
                if (file.Hash.Type != Hashing.Type)
                {
                    throw new InvalidOperationException($"Unsupported hash type: {file.Hash.Type}");
                }
                if (hash == file.Hash.Value)
                {
                    fileInfo.Status = FileStatus.Installed;
                }
                else
                {
                    fileInfo.Status = FileStatus.HashMismatch;
                }
            }
            else if (file.Ignore)
            {
                fileInfo.Status = FileStatus.Ignored;
            }
            else
            {
                fileInfo.Status = FileStatus.NotInstalled;
            }
            return fileInfo;
        }
    }
}
