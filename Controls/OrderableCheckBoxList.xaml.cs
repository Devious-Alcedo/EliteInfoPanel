using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EliteInfoPanel.Core;
using Serilog;

namespace EliteInfoPanel.Controls
{
    public partial class OrderableCheckBoxList : UserControl
    {
        private Point _startPoint;
        private bool _isDragging = false;
        private ListBoxItem _draggedItem = null;
        private int _draggedIndex = -1;
        private ScrollViewer _scrollViewer = null;

        public OrderableCheckBoxList()
        {
            InitializeComponent();

            // Handle drag-drop events
            ItemsListBox.PreviewMouseLeftButtonDown += ItemsListBox_PreviewMouseLeftButtonDown;
            ItemsListBox.PreviewMouseMove += ItemsListBox_PreviewMouseMove;
            ItemsListBox.PreviewMouseLeftButtonUp += ItemsListBox_PreviewMouseLeftButtonUp;
            ItemsListBox.DragEnter += ItemsListBox_DragEnter;
            ItemsListBox.DragOver += ItemsListBox_DragOver;
            ItemsListBox.Drop += ItemsListBox_Drop;
            ItemsListBox.DragLeave += ItemsListBox_DragLeave;

            // Register for changes in the items collection
            FlagItems = new ObservableCollection<FlagCheckBoxItem>();
            FlagItems.CollectionChanged += (s, e) => OnItemsChanged();

            // Find ScrollViewer after template is applied
            ItemsListBox.Loaded += (s, e) => {
                _scrollViewer = FindVisualChild<ScrollViewer>(ItemsListBox);
            };
        }

        // ObservableCollection for the flag items
        public ObservableCollection<FlagCheckBoxItem> FlagItems { get; set; }

        // Event to notify when items are reordered or selection changed
        public event EventHandler ItemsChanged;

        // The collection of Flag values in their current order
        public List<Flag> GetSelectedFlags()
        {
            return FlagItems
                .Where(item => item.IsChecked)
                .Select(item => item.Flag)
                .ToList();
        }

        // Initialize the list with flag values
        public void InitializeFlags(IEnumerable<Flag> allFlags, IEnumerable<Flag> selectedFlags)
        {
            FlagItems.Clear();

            // Filter out Flag.None
            var validFlags = allFlags.Where(f => f != Flag.None);
            var selectedFlagsList = selectedFlags?.ToList() ?? new List<Flag>();

            // First add all selected flags in their proper order
            foreach (var flag in selectedFlagsList)
            {
                if (validFlags.Contains(flag))
                {
                    FlagItems.Add(new FlagCheckBoxItem
                    {
                        Flag = flag,
                        DisplayText = GetDisplayTextForFlag(flag),
                        IsChecked = true
                    });
                }
            }

            // Then add any remaining flags that weren't in the selected list
            foreach (var flag in validFlags.Except(selectedFlagsList))
            {
                FlagItems.Add(new FlagCheckBoxItem
                {
                    Flag = flag,
                    DisplayText = GetDisplayTextForFlag(flag),
                    IsChecked = false
                });
            }

            ItemsListBox.ItemsSource = FlagItems;
        }

        private string GetDisplayTextForFlag(Flag flag)
        {
            return flag switch
            {
                Flag.FsdMassLocked => "Mass Locked",
                Flag.LandingGearDown => "Landing Gear Down",
                var f when f == Util.SyntheticFlags.HudInCombatMode => "HUD Combat Mode",
                var f when f == Util.SyntheticFlags.Docking => "Docking",
                _ => flag.ToString().Replace("_", " ")
            };
        }

        private void OnItemsChanged()
        {
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ItemsListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Log the hit test result
            Log.Debug("Mouse down at position: {Position}", e.GetPosition(ItemsListBox));

            // Find drag handle through hit testing
            FrameworkElement dragHandleElement = FindDragHandleElement(e.OriginalSource as DependencyObject);

            if (dragHandleElement != null)
            {
                Log.Debug("Drag handle found: {ElementName}", dragHandleElement.Name);

                // Store the mouse position for drag distance calculation
                _startPoint = e.GetPosition(null);

                // Find the parent ListBoxItem
                var listBoxItem = FindAncestor<ListBoxItem>(dragHandleElement);
                if (listBoxItem != null)
                {
                    // Remember which item is being dragged
                    _draggedItem = listBoxItem;
                    _draggedIndex = ItemsListBox.ItemContainerGenerator.IndexFromContainer(listBoxItem);

                    Log.Debug("Drag ready on item: {DisplayText} at index {Index}",
                        (listBoxItem.DataContext as FlagCheckBoxItem)?.DisplayText,
                        _draggedIndex);

                    // Select the item
                    listBoxItem.IsSelected = true;

                    // Mark the event as handled to prevent default selection behavior
                    e.Handled = true;
                }
            }
        }

        private void ItemsListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Reset drag state when mouse button is released
            _draggedItem = null;
            _draggedIndex = -1;
            _isDragging = false;

            // Hide the drop indicator
            HideDropIndicator();
        }

        private FrameworkElement FindDragHandleElement(DependencyObject element)
        {
            if (element == null)
                return null;

            // Check if this is the drag handle or its container
            if (element is FrameworkElement fe)
            {
                if (fe.Name == "DragHandle" || fe.Name == "DragHandleContainer")
                    return fe;
            }

            // Check parent
            var parent = VisualTreeHelper.GetParent(element);
            if (parent is FrameworkElement parentFe &&
                (parentFe.Name == "DragHandle" || parentFe.Name == "DragHandleContainer"))
                return parentFe;

            // Get parent of parent
            if (parent != null)
            {
                var grandParent = VisualTreeHelper.GetParent(parent);
                if (grandParent is FrameworkElement grandParentFe &&
                    (grandParentFe.Name == "DragHandle" || grandParentFe.Name == "DragHandleContainer"))
                    return grandParentFe;
            }

            return null;
        }

        private void ItemsListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null && !_isDragging)
            {
                Point currentPosition = e.GetPosition(null);
                Vector diff = _startPoint - currentPosition;

                // Check if mouse has moved far enough to start dragging
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Begin drag operation
                    _isDragging = true;
                    Log.Debug("Starting drag operation for item at index {Index}", _draggedIndex);

                    if (_draggedIndex >= 0 && _draggedIndex < FlagItems.Count)
                    {
                        var flagItem = FlagItems[_draggedIndex];
                        try
                        {
                            // Use DataObject to ensure our data is properly wrapped
                            DataObject dragData = new DataObject("FlagItem", flagItem);
                            DragDrop.DoDragDrop(_draggedItem, dragData, DragDropEffects.Move);

                            Log.Debug("Drag operation completed");
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error during drag operation");
                        }

                        // Reset drag state
                        _isDragging = false;
                        _draggedItem = null;
                        _draggedIndex = -1;

                        // Hide the drop indicator when drag operation completes
                        HideDropIndicator();
                    }
                }
            }
        }

        private void ItemsListBox_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("FlagItem"))
            {
                e.Effects = DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        private void ItemsListBox_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("FlagItem"))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            // Show the move cursor during drag
            e.Effects = DragDropEffects.Move;
            e.Handled = true;

            // Get mouse position relative to the ListBox
            Point dropPosition = e.GetPosition(ItemsListBox);

            // Get the item nearest to the cursor
            ListBoxItem targetItem = GetItemAtPosition(dropPosition);
            if (targetItem == null)
            {
                HideDropIndicator();
                return;
            }

            // Update drop indicator position
            UpdateDropIndicator(targetItem, dropPosition);

            // Auto-scroll if near the top or bottom of the list
            AutoScrollDuringDrag(dropPosition);
        }

        private void ItemsListBox_DragLeave(object sender, DragEventArgs e)
        {
            // Hide the drop indicator when dragging leaves the control
            HideDropIndicator();
        }

        private void ItemsListBox_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("FlagItem"))
            {
                return;
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;

            // Get the dragged item data
            var draggedItemData = e.Data.GetData("FlagItem");
            FlagCheckBoxItem draggedItem = null;

            if (draggedItemData is FlagCheckBoxItem typedItem)
            {
                draggedItem = typedItem;
            }
            else if (_draggedIndex >= 0 && _draggedIndex < FlagItems.Count)
            {
                // Fallback to using the stored index
                draggedItem = FlagItems[_draggedIndex];
            }

            if (draggedItem == null)
            {
                Log.Warning("Drop operation failed: Could not identify dragged item");
                HideDropIndicator();
                return;
            }

            // Find drop target position and item
            Point dropPosition = e.GetPosition(ItemsListBox);
            ListBoxItem targetListBoxItem = GetItemAtPosition(dropPosition);

            if (targetListBoxItem == null)
            {
                Log.Warning("Drop operation failed: No target item at drop position");
                HideDropIndicator();
                return;
            }

            var targetItem = targetListBoxItem.DataContext as FlagCheckBoxItem;

            if (targetItem == null || ReferenceEquals(draggedItem, targetItem))
            {
                HideDropIndicator();
                return; // Same item or invalid target
            }

            // Get the indices for move operation
            int oldIndex = FlagItems.IndexOf(draggedItem);
            int newIndex = FlagItems.IndexOf(targetItem);

            if (oldIndex < 0 || newIndex < 0)
            {
                Log.Warning("Drop operation failed: Invalid indices - Old: {OldIndex}, New: {NewIndex}",
                    oldIndex, newIndex);
                HideDropIndicator();
                return;
            }

            // Determine if dropping above or below the target item
            Rect targetBounds = VisualTreeHelper.GetDescendantBounds(targetListBoxItem);
            Point targetCenter = new Point(
                targetBounds.Left + targetBounds.Width / 2,
                targetBounds.Top + targetBounds.Height / 2);

            // Get position in ItemsListBox coordinates
            GeneralTransform transform = targetListBoxItem.TransformToAncestor(ItemsListBox);
            targetCenter = transform.Transform(targetCenter);

            // Adjust insertion index based on drop position
            if (dropPosition.Y > targetCenter.Y && newIndex < oldIndex)
            {
                // Dropping below an item that's before the dragged item
                newIndex++;
            }
            else if (dropPosition.Y <= targetCenter.Y && newIndex > oldIndex)
            {
                // Dropping above an item that's after the dragged item
                newIndex--;
            }

            Log.Debug("Moving item from position {OldIndex} to {NewIndex}", oldIndex, newIndex);

            // Perform the move
            FlagItems.RemoveAt(oldIndex);
            FlagItems.Insert(Math.Max(0, Math.Min(newIndex, FlagItems.Count)), draggedItem);

            // Update selection
            ItemsListBox.UpdateLayout();
            var newContainer = ItemsListBox.ItemContainerGenerator.ContainerFromItem(draggedItem) as ListBoxItem;
            if (newContainer != null)
            {
                newContainer.IsSelected = true;
            }

            // Hide the drop indicator
            HideDropIndicator();

            // Notify that items have changed
            OnItemsChanged();
        }

        private void UpdateDropIndicator(ListBoxItem targetItem, Point mousePosition)
        {
            if (targetItem == null)
            {
                HideDropIndicator();
                return;
            }

            // Get target item bounds
            Rect targetBounds = new Rect(
                targetItem.TranslatePoint(new Point(0, 0), ItemsListBox),
                new Size(targetItem.ActualWidth, targetItem.ActualHeight));

            // Determine whether to show indicator above or below the target item
            bool placeIndicatorAbove = mousePosition.Y < targetBounds.Top + (targetBounds.Height / 2);

            // Position the drop indicator
            double left = targetBounds.Left;
            double top = placeIndicatorAbove ? targetBounds.Top : targetBounds.Bottom;

            // Set canvas position
            Canvas.SetLeft(DropIndicator, left);
            Canvas.SetTop(DropIndicator, top - (DropIndicator.ActualHeight / 2));

            // Set the width to match the target item
            DropIndicator.Width = targetBounds.Width;

            // Make the indicator visible
            DropIndicator.Visibility = Visibility.Visible;
        }

        private void HideDropIndicator()
        {
            DropIndicator.Visibility = Visibility.Collapsed;
        }

        private void AutoScrollDuringDrag(Point mousePosition)
        {
            if (_scrollViewer == null)
                return;

            const double scrollThreshold = 30; // pixels from edge
            const double scrollAmount = 10; // Amount to scroll

            double viewportHeight = _scrollViewer.ViewportHeight;

            // Auto-scroll up when near the top
            if (mousePosition.Y < scrollThreshold && _scrollViewer.VerticalOffset > 0)
            {
                _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset - scrollAmount);
            }
            // Auto-scroll down when near the bottom
            else if (mousePosition.Y > viewportHeight - scrollThreshold &&
                     _scrollViewer.VerticalOffset < _scrollViewer.ScrollableHeight)
            {
                _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset + scrollAmount);
            }
        }

        private ListBoxItem GetItemAtPosition(Point position)
        {
            HitTestResult hitResult = VisualTreeHelper.HitTest(ItemsListBox, position);
            if (hitResult == null)
                return null;

            return FindAncestor<ListBoxItem>(hitResult.VisualHit);
        }

        private T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                {
                    return ancestor;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    return typedChild;
                }

                T result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            OnItemsChanged();
        }
    }

    // Data item for the flag checkbox list
    public class FlagCheckBoxItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isChecked;

        public Flag Flag { get; set; }
        public string DisplayText { get; set; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }
}