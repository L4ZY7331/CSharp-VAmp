using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpVAmp.Services;
using CSharpVAmp.Utils;

namespace CSharpVAmp.ViewModels;

public partial class InstanceBoxViewModel : ObservableObject
{
    private readonly InstanceManager _instanceManager;
    private readonly int _boxIndex;

    [ObservableProperty]
    private Brush _backgroundColor = Brushes.Transparent;

    [ObservableProperty]
    private int? _instanceId;

    public InstanceBoxViewModel(int boxIndex, InstanceManager instanceManager)
    {
        _boxIndex = boxIndex;
        _instanceManager = instanceManager;
    }

    public void UpdateStatus(InstanceStatus status, int? instanceId)
    {
        InstanceId = instanceId;
        BackgroundColor = status switch
        {
            InstanceStatus.Inactive => Brushes.Transparent,
            InstanceStatus.Starting => Brushes.Gray,
            InstanceStatus.Initialized => Brushes.Yellow,
            InstanceStatus.Restarting => Brushes.Yellow,
            InstanceStatus.Buffering => Brushes.Yellow,
            InstanceStatus.Watching => new SolidColorBrush(Color.FromRgb(0x44, 0xD2, 0x09)),
            InstanceStatus.Shutdown => Brushes.Transparent,
            _ => Brushes.Transparent
        };
    }

    public void OnLeftClick()
    {
        if (InstanceId.HasValue)
        {
            _instanceManager.QueueCommand(InstanceId.Value, InstanceCommands.Refresh);
        }
    }

    public void OnRightClick()
    {
        if (InstanceId.HasValue)
        {
            _instanceManager.QueueCommand(InstanceId.Value, InstanceCommands.Exit);
        }
    }

    public void OnControlLeftClick()
    {
        if (InstanceId.HasValue)
        {
            _instanceManager.QueueCommand(InstanceId.Value, InstanceCommands.Screenshot);
        }
    }
}

