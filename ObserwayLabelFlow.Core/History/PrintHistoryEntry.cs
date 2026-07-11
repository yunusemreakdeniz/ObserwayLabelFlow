using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ObserwayLabelFlow.Core.History;

public sealed class PrintHistoryEntry : INotifyPropertyChanged
{
    private bool _isSelected;

    public long Id { get; set; }

    public string TrackingNumber { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? PdfUrl { get; set; }

    public string? Notes { get; set; }

    public string? OrderNumber { get; set; }

    public string? CustomerName { get; set; }

    public string? OrderStatus { get; set; }

    public string? CarrierName { get; set; }

    public int ProductCount { get; set; }

    public string? PrinterName { get; set; }

    public int Copies { get; set; }

    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public string? PaperSize { get; set; }

    public string? PrintedBy { get; set; }

    /// <summary>Serialized <see cref="HistoryOrderSnapshot"/> for comprehensive export.</summary>
    public string? SnapshotJson { get; set; }

    /// <summary>UI-only selection flag; not persisted.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
