using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WheelConvert.Core.Contracts;

namespace WheelConvert.App.Pdf;

/// <summary>
/// Thumbnail grid for reordering PDF pages. Reordering uses mouse capture (MouseDown, CaptureMouse, MouseMove, MouseUp),
/// not OLE drag-and-drop, so no DragDropEffects are involved (ADR-003).
/// </summary>
public partial class PageOrderWindow : Window
{
    private const int ThumbnailPixels = 320;

    private readonly PageOrderViewModel _viewModel;
    private readonly IPdfPageRenderer _renderer;
    private readonly ILog _log;
    private readonly string _pdfPath;
    private readonly CancellationToken _token;
    private Point _pressPoint;
    private PageItem? _dragItem;
    private bool _dragging;

    public PageOrderWindow(PageOrderViewModel viewModel, IPdfPageRenderer renderer, ILog log, string pdfPath)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _pdfPath = pdfPath;
        InitializeComponent();
        Title = $"Reorder pages — {Path.GetFileName(pdfPath)}";
        PageList.ItemsSource = _viewModel.Pages;
        _viewModel.Pages.CollectionChanged += (_, _) => SaveButton.IsEnabled = _viewModel.CanSave;
        Loaded += (_, _) =>
        {
            // The drop comes from Explorer; without this the window can open behind it.
            Activate();
            _ = LoadThumbnailsAsync();
        };
        // Held only by the Closed handler so the window itself owns nothing disposable.
        var cts = new CancellationTokenSource();
        _token = cts.Token;
        Closed += (_, _) =>
        {
            cts.Cancel();
            cts.Dispose();
        };
    }

    private async Task LoadThumbnailsAsync()
    {
        var token = _token;
        try
        {
            // One page at a time keeps memory and CPU modest. Rendering runs off the UI thread; decoding is
            // frozen and assigned back here on the UI thread. Walks every page, not only those currently listed,
            // so a page the user removed (and may bring back with Reset) still gets its thumbnail.
            foreach (var item in _viewModel.AllItems)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var png = await Task.Run(() => _renderer.RenderPagePngAsync(_pdfPath, item.PageNumber - 1, ThumbnailPixels, token), token)
                        .ConfigureAwait(true);
                    item.Thumbnail = Decode(png);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // One bad page must not stop the others.
                    _log.Warn($"Thumbnail for page {item.PageNumber} failed; showing a placeholder.", ex);
                    item.PreviewFailed = true;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Window closed while loading.
        }
    }

    private static BitmapImage Decode(byte[] png)
    {
        var image = new BitmapImage();
        using var stream = new MemoryStream(png);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private int SelectedIndex => PageList.SelectedItem is PageItem item ? _viewModel.Pages.IndexOf(item) : -1;

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // A page being held must not change under the drag; the drag ends on mouse-up.
        if (_dragItem is not null)
        {
            return;
        }

        var index = SelectedIndex;
        if (index < 0)
        {
            return;
        }

        if (e.Key == Key.Delete)
        {
            RemoveAt(index);
            e.Handled = true;
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && (e.Key == Key.Left || e.Key == Key.Right))
        {
            var target = e.Key == Key.Left ? index - 1 : index + 1;
            var item = _viewModel.Pages[index];
            if (_viewModel.Move(index, target))
            {
                PageList.SelectedItem = item;
            }

            e.Handled = true;
        }
    }

    private void RemoveAt(int index)
    {
        if (_viewModel.Remove(index) && _viewModel.Pages.Count > 0)
        {
            PageList.SelectedIndex = Math.Min(index, _viewModel.Pages.Count - 1);
        }
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is PageItem item)
        {
            RemoveAt(_viewModel.Pages.IndexOf(item));
        }

        e.Handled = true;
    }

    private void OnListMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null)
        {
            return; // Let the remove button handle its own click.
        }

        var index = IndexAt(e.GetPosition(PageList));
        _dragItem = index >= 0 ? _viewModel.Pages[index] : null;
        if (_dragItem is not null)
        {
            _pressPoint = e.GetPosition(PageList);
            _dragging = false;
            PageList.SelectedItem = _dragItem;
            PageList.CaptureMouse();
        }
    }

    private void OnListMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragItem is null || !PageList.IsMouseCaptured)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            // The mouse-up was missed; let go so the next click is not swallowed.
            PageList.ReleaseMouseCapture();
            return;
        }

        var position = e.GetPosition(PageList);
        if (!_dragging)
        {
            if (Math.Abs(position.X - _pressPoint.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(position.Y - _pressPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            _dragging = true;
        }

        var target = IndexAt(position);
        if (target >= 0 && _viewModel.Pages.IndexOf(_dragItem) != target)
        {
            if (_viewModel.MoveItem(_dragItem, target))
            {
                PageList.SelectedItem = _dragItem;
            }
            else
            {
                PageList.ReleaseMouseCapture(); // The held page is no longer in the list: end the drag.
            }
        }
    }

    private void OnListMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (PageList.IsMouseCaptured)
        {
            PageList.ReleaseMouseCapture();
        }
    }

    private void OnListLostCapture(object sender, MouseEventArgs e)
    {
        _dragItem = null;
        _dragging = false;
    }

    private int IndexAt(Point point)
    {
        if (PageList.InputHitTest(point) is DependencyObject hit
            && FindAncestor<ListBoxItem>(hit) is { } container
            && PageList.ItemContainerGenerator.ItemFromContainer(container) is PageItem item)
        {
            return _viewModel.Pages.IndexOf(item);
        }

        return -1;
    }

    private static T? FindAncestor<T>(DependencyObject start)
        where T : DependencyObject
    {
        // Run elements and similar are not Visuals; LogicalTreeHelper gets us back into the visual tree.
        for (DependencyObject? node = start; node is not null;
            node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
        {
            if (node is T match)
            {
                return match;
            }
        }

        return null;
    }

    private void OnReverse(object sender, RoutedEventArgs e) => _viewModel.Reverse();

    private void OnReset(object sender, RoutedEventArgs e) => _viewModel.Reset();

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Accept())
        {
            Close();
        }
    }
}
