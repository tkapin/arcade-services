// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

#nullable enable
namespace Microsoft.DotNet.Darc.Models.VirtualMonoRepo;

public interface ISourceManifest
{
    IReadOnlyCollection<ISourceComponent> Repositories { get; }
    IReadOnlyCollection<ISourceComponent> Submodules { get; }

    string ToJson();
    void RemoveSubmodule(SubmoduleRecord submodule);
    void UpdateSubmodule(SubmoduleRecord submodule);
    void UpdateVersion(string repository, string uri, string sha, string packageVersion);
}

/// <summary>
/// A model for source-manifest.json file which VMR uses to keep track of
/// synchronized sources
/// </summary>
public class SourceManifest : ISourceManifest
{
    private readonly SortedSet<RepositoryRecord> _repositories;
    private readonly SortedSet<SubmoduleRecord> _submodules;

    public IReadOnlyCollection<ISourceComponent> Repositories => _repositories;
    public IReadOnlyCollection<ISourceComponent> Submodules => _submodules;

    public SourceManifest(IEnumerable<RepositoryRecord> repositories, IEnumerable<SubmoduleRecord> submodules)
    {
        _repositories = new SortedSet<RepositoryRecord>(repositories);
        _submodules = new SortedSet<SubmoduleRecord>(submodules);
    }

    public void UpdateVersion(string repository, string uri, string sha, string packageVersion)
    {
        var repo = _repositories.FirstOrDefault(r => r.Path == repository);
        if (repo != null)
        {
            repo.CommitSha = sha;
            repo.RemoteUri = uri;
            repo.PackageVersion = packageVersion;
        }
        else
        {
            _repositories.Add(new RepositoryRecord(repository, uri, sha, packageVersion));
        }
    }

    public void RemoveSubmodule(SubmoduleRecord submodule)
    {
        var repo = _submodules.FirstOrDefault(r => r.Path == submodule.Path);
        if (repo != null)
        {
            _submodules.Remove(repo);
        }
    }

    public void UpdateSubmodule(SubmoduleRecord submodule)
    {
        var repo = _submodules.FirstOrDefault(r => r.Path == submodule.Path);
        if (repo != null)
        {
            repo.CommitSha = submodule.CommitSha;
            repo.RemoteUri = submodule.RemoteUri;
        }
        else
        {
            _submodules.Add(submodule);
        }
    }

    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        var data = new SourceManifestWrapper
        {
            Repositories = _repositories,
            Submodules = _submodules,
        };

        return JsonSerializer.Serialize(data, options);
    }

    public static SourceManifest FromJson(string path)
    {
        if (!File.Exists(path))
        {
            return new SourceManifest(Array.Empty<RepositoryRecord>(), Array.Empty<SubmoduleRecord>());
        }

        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
        var wrapper = JsonSerializer.Deserialize<SourceManifestWrapper>(stream, options)
            ?? throw new Exception($"Failed to deserialize {path}");

        return new SourceManifest(wrapper.Repositories, wrapper.Submodules);
    }
    
    /// <summary>
    /// We use this for JSON deserialization because we're on .NET 6.0 and the ctor deserialization doesn't work.
    /// </summary>
    private class SourceManifestWrapper
    {
        public ICollection<RepositoryRecord> Repositories { get; init; } = Array.Empty<RepositoryRecord>();
        public ICollection<SubmoduleRecord> Submodules { get; init; } = Array.Empty<SubmoduleRecord>();
    }
}