using System.Diagnostics;
using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.System;

namespace Shelfy.Infrastructure.System;

/// <summary>
/// Win32 Item起動の実装
/// </summary>
public class Win32ItemLauncher : IItemLauncher
{
    public Task<bool> LaunchAsync(Item item)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = item.Target,
                UseShellExecute = true
            };

            Process.Start(psi);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> OpenParentFolderAsync(Item item)
    {
        try
        {
            if (item.Type == ItemType.Url)
            {
                return Task.FromResult(false);
            }

            var path = item.Target;
            var parentFolder = Path.GetDirectoryName(path);

            if (string.IsNullOrEmpty(parentFolder) || !Directory.Exists(parentFolder))
            {
                return Task.FromResult(false);
            }

            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            };

            Process.Start(psi);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
