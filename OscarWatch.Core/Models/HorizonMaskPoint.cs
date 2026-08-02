using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OscarWatch.Core.Models;

/// <summary>One skyline sample: obstruction elevation at a given azimuth.</summary>
public sealed class HorizonMaskPoint : INotifyPropertyChanged
{
    private double _azimuthDeg;
    private double _elevationDeg;

    public HorizonMaskPoint()
    {
    }

    public HorizonMaskPoint(double azimuthDeg, double elevationDeg)
    {
        _azimuthDeg = azimuthDeg;
        _elevationDeg = elevationDeg;
    }

    public double AzimuthDeg
    {
        get => _azimuthDeg;
        set
        {
            if (Math.Abs(_azimuthDeg - value) < 1e-9)
                return;
            _azimuthDeg = value;
            OnPropertyChanged();
        }
    }

    public double ElevationDeg
    {
        get => _elevationDeg;
        set
        {
            if (Math.Abs(_elevationDeg - value) < 1e-9)
                return;
            _elevationDeg = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
