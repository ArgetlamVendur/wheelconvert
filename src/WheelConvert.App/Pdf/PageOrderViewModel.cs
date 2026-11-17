using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace WheelConvert.App.Pdf;

/// <summary>One page tile. <see cref="PageNumber"/> is the 1-based page in the source PDF and never changes.</summary>
public sealed class PageItem(int pageNumber) : INotifyPropertyChanged
{
    private ImageSource? _thumbnail;
    private bool _previewFailed;

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

    /// <summary>True when rendering this page failed; the placeholder then reads "No preview" instead of "Loading…".</summary>
    public bool PreviewFailed
    {
        get => _previewFailed;
        set
        {
            _previewFailed = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PreviewFailed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Placeholder)));
        }
    }

    public string Placeholder => _previewFailed ? "No preview" : "Loading…";
}

/// <summary>Pure list logic for the reorder window: no I/O, no rendering. All indices are 0-based positions in the current list.</summary>
public sealed class PageOrderViewModel
{
    private readonly PageItem[] _allItems;

    public PageOrderViewModel(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);
        _allItems = [.. Enumerable.Range(1, pageCount).Select(n => new PageItem(n))];
        Pages = [];
        Reset();
    }

    public ObservableCollection<PageItem> Pages { get; }

    /// <summary>Every page of the PDF in source order, including removed ones. Created once, so thumbnails survive Remove and Reset.</summary>
    public IReadOnlyList<PageItem> AllItems => _allItems;

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

    /// <summary>Moves a specific page tile; false (no change) if it is no longer in the list or the target is out of range.</summary>
    public bool MoveItem(PageItem item, int to)
    {
        var from = Pages.IndexOf(item);
        return from >= 0 && Move(from, to);
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

    /// <summary>Restores all pages in original order (also brings back removed pages). The same <see cref="PageItem"/> instances return, so loaded thumbnails are kept.</summary>
    public void Reset()
    {
        Pages.Clear();
        foreach (var item in _allItems)
        {
            Pages.Add(item);
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
