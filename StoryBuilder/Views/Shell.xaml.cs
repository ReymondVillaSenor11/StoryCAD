﻿using System;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using StoryBuilder.Models;
using StoryBuilder.Models.Tools;
using StoryBuilder.Services.Logging;
using StoryBuilder.Services.Messages;
using StoryBuilder.ViewModels;
using Windows.UI.ViewManagement;
using Microsoft.UI.Dispatching;
using StoryBuilder.Services;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using System.Diagnostics;

namespace StoryBuilder.Views;

public sealed partial class Shell
{
    public ShellViewModel ShellVm => Ioc.Default.GetService<ShellViewModel>();
    public UnifiedVM UnifiedVm => Ioc.Default.GetService<UnifiedVM>();
    public PreferencesModel Preferences = GlobalData.Preferences;

    private TreeViewNode dragTargetNode;
    private StoryNodeItem dragTargetStoryNode;
    private StoryNodeItem dragSourceStoryNode;
    private bool dragOperationValid;
    private LogService Logger;

    public Shell()
    {
        try
        {
            InitializeComponent();
            Logger = Ioc.Default.GetService<LogService>();
            DataContext = ShellVm;
            GlobalData.GlobalDispatcher = DispatcherQueue.GetForCurrentThread();
            Loaded += Shell_Loaded;
        }                         
        catch (Exception ex)
        {
            // A shell initialization error is fatal
            Logger.LogException(LogLevel.Error, ex, ex.Message);
            Logger.Flush();
            Application.Current.Exit();  // Win32
        }
        ShellVm.SplitViewFrame = SplitViewFrame;
    }

    private async void Shell_Loaded(object sender, RoutedEventArgs e)
    {
        // The Shell_Loaded event is processed in order to obtain and save the XamlRool  
        // and pass it on to ContentDialogs as a WinUI work-around. See
        // https://docs.microsoft.com/en-us/windows/winui/api/microsoft.ui.xaml.controls.contentdialog?view=winui-3.0-preview
        GlobalData.XamlRoot = Content.XamlRoot;
        ShellVm.ShowHomePage();
        ShellVm.ShowConnectionStatus();        
        if (GlobalData.ShowDotEnvWarning) { await ShellVm.ShowDotEnvWarningAsync(); }

        if (!await Ioc.Default.GetRequiredService<WebViewModel>().CheckWebviewState())
        {
            ShellVm._canExecuteCommands = false;
            await Ioc.Default.GetRequiredService<WebViewModel>().ShowWebviewDialog();
            ShellVm._canExecuteCommands = true;
        }
        if (GlobalData.LoadedWithVersionChange ) { await ShellVm.ShowChangelog(); }

        //If StoryBuilder was loaded from a .STBX File then instead of showing the Unified menu
        //We will instead load the file instead.
        if (GlobalData.FilePathToLaunch == null) { await ShellVm.OpenUnifiedMenu(); }
        else { await ShellVm.OpenFile(GlobalData.FilePathToLaunch);}
    }

    /// <summary>
    /// Makes the TreeView lose its selection when there is no corresponding main menu item.
    /// </summary>
    /// <remarks>But I don't know why...</remarks>
    private void SplitViewFrame_OnNavigated(object sender, NavigationEventArgs e)
    {
        NavigationTree.SelectionMode = TreeViewSelectionMode.None;
        NavigationTree.SelectionMode = TreeViewSelectionMode.Single;
    }

    /// <summary>
    /// Navigates to the specified source page type.
    /// </summary>
    public bool Navigate(Type sourcePageType, object parameter = null)
    {
        return SplitViewFrame.Navigate(sourcePageType, parameter);
    }
    private void TreeViewItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (ShellVm.RightClickedTreeviewItem != null) { ShellVm.RightClickedTreeviewItem.Background = null; } //Remove old right clicked nodes background

        TreeViewItem item = (TreeViewItem)sender;
        item.Background = new SolidColorBrush(new UISettings().GetColorValue(UIColorType.Accent));

        ShellVm.RightTappedNode = (StoryNodeItem)item.DataContext;
        ShellVm.RightClickedTreeviewItem = item; //We can't set the background through righttappednode so we set a reference to the node itself to reset the background later
        ShellVm.ShowFlyoutButtons();
    }

    /// <summary>
    /// Treat a treeview item as if it were a button
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void TreeViewItem_Invoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        ShellVm.TreeViewNodeClicked(args.InvokedItem);
        args.Handled = true;
    }

    private void AddButton_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        FlyoutShowOptions myOption = new();
        myOption.ShowMode = FlyoutShowMode.Transient;
        AddStoryElementCommandBarFlyout.ShowAt(NavigationTree, myOption);
    }

    /// <summary>
    /// This is called when the user clicks the save pen
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void SaveIconPressed(object sender, PointerRoutedEventArgs e) { await ShellVm.SaveFile(); }

    private void Search(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ShellVm.SearchNodes();
    }

    private void ClearNodes(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (ShellVm.DataSource == null || ShellVm.DataSource.Count == 0) { return; }
        foreach (StoryNodeItem node in ShellVm.DataSource[0]) { node.Background = null; }
    }

    private void TreeView_DragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs args)
    {
        Logger.Log(LogLevel.Trace, $"OnDragItemsStarting event");
        args.Data.RequestedOperation = DataPackageOperation.Move;
        dragOperationValid = true;
        // args.Items[0] is the TreeViewItem you're dragging.
        // With SelectionMode="Single" there will be only the one.
        Type type = args.Items[0].GetType();
        if (!type.Name.Equals("StoryNodeItem"))
        {
            Logger.Log(LogLevel.Warn, $"Invalid dragSource type: {type.Name}");
            args.Data.RequestedOperation = DataPackageOperation.None;
            dragOperationValid = false;
            ShellVm.ShowMessage(LogLevel.Warn, "Invalid drag source", false);
            return;
        }

        dragSourceStoryNode = args.Items[0] as StoryNodeItem;
        StoryNodeItem _parent = dragSourceStoryNode.Parent;
        if (_parent == null)
        {
            Logger.Log(LogLevel.Warn, $"dragSource is not below root");
            args.Data.RequestedOperation = DataPackageOperation.None;
            dragOperationValid = false;
            ShellVm.ShowMessage(LogLevel.Warn, "Invalid drag source", false);
            return;
        }
        while (!_parent.IsRoot) // find the root
        {
            _parent = _parent.Parent;
        }
        if (_parent.Type == StoryItemType.TrashCan)
        {
            Logger.Log(LogLevel.Warn, $"dragSource root is TrashCan");
            args.Data.RequestedOperation = DataPackageOperation.None;
            dragOperationValid = false;
            ShellVm.ShowMessage(LogLevel.Warn, "Invalid drag source", false);   Debug.WriteLine($"Exit TreeView_DragItemsStarting: RequestedOperation={args.Data.RequestedOperation}");
            return;
        }
        // Source node is valid for move
        Logger.Log(LogLevel.Trace, $"dragSource Name: {dragSourceStoryNode.Name}");
    }

    private void TreeViewItem_OnDragEnter(object sender, DragEventArgs args)
    {
        Logger.Log(LogLevel.Trace, $"OnDragEnter event");
        // sender is the node you're dragging over (the prospective target)
        Type type = sender.GetType();
        if (!type.Name.Equals("TreeViewItem"))
        {
            Logger.Log(LogLevel.Warn, $"Invalid dragTarget type: {type.Name}");
            args.Data.RequestedOperation = DataPackageOperation.None;
            ShellVm.ShowMessage(LogLevel.Warn, "Invalid drag target type", false);
            dragOperationValid = false;
            args.Handled = true;
            return;
        }
        var dragTargetItem = sender as TreeViewItem;
        dragTargetNode = NavigationTree.NodeFromContainer(dragTargetItem);
        Logger.Log(LogLevel.Trace, $"dragTarget Depth: {dragTargetNode.Depth}");

        var node = dragTargetNode;
        // A moved node is inserted above the target node, so you can't move to the root.
        if (node.Depth < 1)
        {
            Logger.Log(LogLevel.Warn, $"dragTarget is not below root");
            ShellVm.ShowMessage(LogLevel.Warn, "Drag target is not below root", false);
            dragOperationValid = false;
            args.Handled = true;
            return;
        }

        dragTargetStoryNode = dragTargetNode.Content as StoryNodeItem;
        Logger.Log(LogLevel.Trace, $"dragTarget Name: {dragTargetStoryNode.Name}");
        Logger.Log(LogLevel.Trace, $"dragTarget type: {dragTargetStoryNode.Type.ToString()}");

        // Insure that the target is not in the trashcan
        while (node.Depth != 0)
        {
            node = node.Parent;
        }
        var root = node.Content as StoryNodeItem;
        if (root.Type == StoryItemType.TrashCan)
        {
            Logger.Log(LogLevel.Warn, $"dragTarget root is TrashCan");
            ShellVm.ShowMessage(LogLevel.Warn, "Drag to Trashcan invalid", false);
            dragOperationValid = false;
            args.Handled = true;
            return;
        }
        // Move is valid, allow the drop operation.  
        //args.AcceptedOperation = DataPackageOperation.Move;
        //dragOperationValid = true;
    }

    private void TreeViewItem_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs args)
    {
        Logger.Log(LogLevel.Trace, $"OnDragItemsCompleted event");
        var sourceNode = dragSourceStoryNode;
        var targetNode = dragTargetStoryNode;

        if (dragOperationValid)
        {
            // Remove the source node from its original parent's children collection
            sourceNode.Parent.Children.Remove(sourceNode);

            // Add the source node to the target node's parent's children collection.
            // Insert() places the source immediately before the target.
            targetNode.Parent.Children.Insert(targetNode.Parent.Children.IndexOf(targetNode), sourceNode);

            // Update the source node's parent
            sourceNode.Parent = targetNode.Parent;

            // Refresh the UI and report the move
            ShellViewModel.ShowChange();
            ShellVm.ShowMessage(LogLevel.Info, "Drag and drop successful", true);
        }

        dragOperationValid = true;
        NavigationTree.CanDrag = true;
        NavigationTree.AllowDrop = true;
    }
}