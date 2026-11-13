using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace WheelConvert.App.Pdf;

/// <summary>One page tile. <see cref="PageNumber"/> is the 1-based page in the source PDF and never changes.</summary>
public sealed class PageItem(int pageNumber) : INotifyPropertyChanged
{
    private ImageSource? _thumbnail;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int PageNumber { get; } = pageNumber;

    /// <summary>Null until the renderer has produced the thumbnail (the window shows a placeholder).</summary>
    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set
        {
            _thumbnail = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
        }
    }
}

/// <summary>Pure list logic for the reorder window: no I/O, no rendering. All indices are 0-based positions in the current list.</summary>
public sealed class PageOrderViewModel
{
    private readonly int _pageCount;

    public PageOrderViewModel(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);
        _pageCount = pageCount;
        Pages = [];
        Reset();
    }

    public ObservableCollection<PageItem> Pages { get; }

    /// <summary>True once <see cref="Accept"/> succeeded; closing or cancelling leaves it false.</summary>
    public bool Accepted { get; private set; }

    /// <summary>Save is disabled when every page has been removed.</summary>
    public bool CanSave => Pages.Count > 0;

    /// <summary>1-based source page numbers in the current order.</summary>
    public IReadOnlyList<int> Result => [.. Pages.Select(p => p.PageNumber)];

    /// <summary>What the prompt returns: the order if the user saved, otherwise null (Cancel or window closed).</summary>
    public IReadOnlyList<int>? FinalResult => Accepted ? Result : null;

    public bool Move(int from, int to)
    {
        if (from < 0 || from >= Pages.Count || to < 0 || to >= Pages.Count)
        {
            return false;
        }

        if (from != to)
        {
            Pages.Move(from, to);
        }

        return true;
    }

    public bool Remove(int index)
    {
        if (index < 0 || index >= Pages.Count)
        {
            return false;
        }

        Pages.RemoveAt(index);
        return true;
    }

    public void Reverse()
    {
        var reversed = Pages.Reverse().ToList();
        for (var i = 0; i < reversed.Count; i++)
        {
            Pages.Move(Pages.IndexOf(reversed[i]), i);
        }
    }

    /// <summary>Restores all pages in original order (also brings back removed pages). Keeps already-loaded thumbnails.</summary>
    public void Reset()
    {
        var existing = Pages.ToDictionary(p => p.PageNumber);
        Pages.Clear();
        for (var n = 1; n <= _pageCount; n++)
        {
            Pages.Add(existing.TryGetValue(n, out var item) ? item : new PageItem(n));
        }
    }

    public bool Accept()
    {
        if (!CanSave)
        {
            return false;
        }

        Accepted = true;
        return true;
    }
}
