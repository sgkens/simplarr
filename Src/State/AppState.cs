// src/State/AppState.cs
using Simplerr.Config;
using Simplerr.Services.Docker.Models;
using Simplerr.Services.Wsl;

namespace Simplerr.State;

public class AppState
{
    private readonly object _lock = new();

    public WslStatus WslStatus { get; private set; } = WslStatus.Unknown;
    public List<ContainerInfo> Containers { get; private set; } = [];
    public Dictionary<string, ContainerStats> Stats { get; private set; } = [];
    public int SelectedIndex { get; private set; } = 0;
    public int ScrollOffset { get; private set; } = 0;
    public string? StatusMessage { get; private set; }
    public bool IsBusy { get; private set; } = false;
    public bool IsHelpOpen { get; private set; } = false;
    public ContainerInfo? RequestedLogContainer { get; set; }

    public ContainerInfo? SelectedContainer
    {
        get
        {
            lock (_lock)
            {
                if (Containers.Count == 0) return null;
                var idx = Math.Clamp(SelectedIndex, 0, Containers.Count - 1);
                return Containers[idx];
            }
        }
    }

    public void UpdateWslStatus(WslStatus status)
    {
        lock (_lock) WslStatus = status;
    }

    public void UpdateContainers(List<ContainerInfo> containers)
    {
        lock (_lock)
        {
            Containers = containers;
            SelectedIndex = Math.Clamp(SelectedIndex, 0, Math.Max(0, containers.Count - 1));
            
            if (ScrollOffset > Math.Max(0, containers.Count - 1))
                ScrollOffset = 0;
        }
    }

    public void UpdateStats(string containerId, ContainerStats stats)
    {
        lock (_lock) Stats[containerId] = stats;
    }

    public void MoveSelectionUp()
    {
        lock (_lock)
        {
            if (IsHelpOpen) return;
            if (SelectedIndex > 0)
            {
                SelectedIndex--;
            }
        }
    }

    public void MoveSelectionDown()
    {
        lock (_lock)
        {
            if (IsHelpOpen) return;
            if (SelectedIndex < Containers.Count - 1)
            {
                SelectedIndex++;
            }
        }
    }

    public void UpdateScroll(int pageSize)
    {
        lock (_lock)
        {
            if (Containers.Count <= pageSize)
            {
                ScrollOffset = 0;
                return;
            }

            if (SelectedIndex < ScrollOffset)
            {
                ScrollOffset = SelectedIndex;
            }
            else if (SelectedIndex >= ScrollOffset + pageSize)
            {
                ScrollOffset = SelectedIndex - pageSize + 1;
            }
        }
    }

    public void SetStatus(string? message)
    {
        lock (_lock) StatusMessage = message;
    }

    public void SetBusy(bool busy)
    {
        lock (_lock) IsBusy = busy;
    }

    public void ToggleHelp()
    {
        lock (_lock) IsHelpOpen = !IsHelpOpen;
    }
}
