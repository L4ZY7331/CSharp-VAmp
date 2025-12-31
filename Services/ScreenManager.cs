using System.Windows;
using CSharpVAmp.Models;
using Microsoft.Extensions.Logging;

namespace CSharpVAmp.Services;

public class ScreenManager
{
    private readonly List<ScreenLocation> _spawnLocations = new();
    private readonly object _lock = new();
    private readonly ILogger<ScreenManager> _logger;

    public int AvailableLocations => _spawnLocations.Count(loc => loc.Free);

    public ScreenManager(int windowWidth, int windowHeight, ILogger<ScreenManager> logger)
    {
        _logger = logger;
        
        int screenWidth = 1920;
        int screenHeight = 1080;
        
        try
        {
            screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            screenHeight = (int)SystemParameters.PrimaryScreenHeight;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get screen dimensions, using defaults");
        }

        const int windowWidthOffset = 100;
        const int windowHeightOffset = 50;

        var cols = screenWidth / (windowWidth - windowWidthOffset);
        var rows = screenHeight / (windowHeight - windowHeightOffset);

        var index = 0;
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                _spawnLocations.Add(new ScreenLocation
                {
                    Index = index++,
                    X = col * (windowWidth - windowWidthOffset),
                    Y = row * (windowHeight - windowHeightOffset),
                    Width = windowWidth,
                    Height = windowHeight,
                    Free = true
                });
            }
        }

        _logger.LogInformation("Generated {Count} screen locations", _spawnLocations.Count);
    }

    public ScreenLocation? GetFreeScreenLocation()
    {
        lock (_lock)
        {
            var freeLocation = _spawnLocations.FirstOrDefault(loc => loc.Free);
            if (freeLocation != null)
            {
                freeLocation.Free = false;
            }
            return freeLocation;
        }
    }

    public ScreenLocation GetDefaultLocation()
    {
        return _spawnLocations[0];
    }

    public void ReleaseLocation(ScreenLocation location)
    {
        lock (_lock)
        {
            location.Free = true;
        }
    }
}

