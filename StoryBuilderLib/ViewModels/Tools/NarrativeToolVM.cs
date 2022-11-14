﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using StoryBuilder.Models;
using StoryBuilder.Services.Logging;

namespace StoryBuilder.ViewModels.Tools;
public class NarrativeToolVM: ObservableRecipient
{
    private ShellViewModel _shellVM = Ioc.Default.GetRequiredService<ShellViewModel>();
    private LogService _logger = Ioc.Default.GetRequiredService<LogService>();
    public StoryNodeItem SelectedNode;
    public bool IsNarratorSelected = false;
    public RelayCommand CopyCommand { get; } 
    public RelayCommand DeleteCommand { get; } 
    public RelayCommand CopyAllUnusedCommand { get; }
    public RelayCommand CreateFlyout { get; }

    //Name of the section in the flyout
    private string _flyoutText;
    public string FlyoutText
    {
        get => _flyoutText;
        set => SetProperty(ref _flyoutText, value);
    }
    private string _message;
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public NarrativeToolVM()
    {
        CreateFlyout = new RelayCommand(MakeSection);
        CopyCommand = new RelayCommand(Copy);
        CopyAllUnusedCommand = new RelayCommand(CopyAllUnused);
        DeleteCommand = new RelayCommand(Delete);
    }

    /// <summary>
    /// Deletes a node from the tree.
    /// </summary>
    public void Delete()
    {
        try
        {
            if (SelectedNode != null)
            {
                if (SelectedNode.Type == StoryItemType.TrashCan || SelectedNode.IsRoot) { Message = "You can't delete this node!"; }

                if (IsNarratorSelected)
                {
                    SelectedNode.Delete(StoryViewType.NarratorView);
                    Message = $"Deleted {SelectedNode}";
                }
                else { Message = "You can't delete from here!"; }
            }
            else { _logger.Log(LogLevel.Warn, "Selected node was null, doing nothing"); }
        }
        catch (Exception _ex) { _logger.LogException(LogLevel.Error, _ex, "Error in NarrativeToolVM.Delete()"); }
    }
    

    /// <summary>
    /// Copies all scenes, if the node has children then it will copy all children that are scenes
    /// </summary>
    private void Copy()
    {
        try
        {
            _logger.Log(LogLevel.Info, "Starting to copy node between trees.");

            //Check if selection is null
            if (SelectedNode == null)
            {
                _logger.Log(LogLevel.Warn, "No node selected");
                return;
            }

            _logger.Log(LogLevel.Info, $"Node Selected is a {SelectedNode.Type}");
            if (SelectedNode.Type == StoryItemType.Scene)  //If its just a scene, add it immediately if not already in.
            {
                if (RecursiveCheck(_shellVM.StoryModel.NarratorView[0].Children).All(storyNodeItem => storyNodeItem.Uuid != SelectedNode.Uuid)) //checks node isn't in the narrator view
                {
                    _ = new StoryNodeItem((SceneModel)_shellVM.StoryModel.StoryElements.StoryElementGuids[SelectedNode.Uuid], _shellVM.StoryModel.NarratorView[0]);
                    _logger.Log(LogLevel.Info, $"Copied SelectedNode {SelectedNode.Name} ({SelectedNode.Uuid})");
                    Message = $"Copied {SelectedNode.Name}";
                }
                else
                {
                    _logger.Log(LogLevel.Warn, $"Node {SelectedNode.Name} ({SelectedNode.Uuid}) already exists in the NarratorView");
                    Message = "This scene already appears in the narrative view.";
                }
            }
            else if (SelectedNode.Type is StoryItemType.Folder or StoryItemType.Section) //If its a folder then recurse and add all unused scenes to the narrative view.
            {
                _logger.Log(LogLevel.Info, "Item is a folder/section, getting flattened list of all children.");
                foreach (StoryNodeItem _item in RecursiveCheck(SelectedNode.Children))
                {
                    if (_item.Type == StoryItemType.Scene && RecursiveCheck(_shellVM.StoryModel.NarratorView[0].Children).All(storyNodeItem => storyNodeItem.Uuid != _item.Uuid))
                    {
                        _ = new StoryNodeItem((SceneModel)_shellVM.StoryModel.StoryElements.StoryElementGuids[_item.Uuid], _shellVM.StoryModel.NarratorView[0]);
                        _logger.Log(LogLevel.Info, $"Copied item {SelectedNode.Name} ({SelectedNode.Uuid})");
                    }
                }

                Message = $"Copied {SelectedNode.Children} and child scenes.";
            }
            else
            {
                _logger.Log(LogLevel.Warn, $"Node {SelectedNode.Name} ({SelectedNode.Uuid}) wasn't copied, it was a {SelectedNode.Type}");
                Message = "You can't copy that."; 
            }
        }
        catch (Exception _ex) { _logger.LogException(LogLevel.Error, _ex, "Error in NarrativeTool.Copy()"); }
        _logger.Log(LogLevel.Info, "NarrativeTool.Copy() complete.");

    }


    private List<StoryNodeItem> RecursiveCheck(ObservableCollection<StoryNodeItem> list)
    {
        _logger.Log(LogLevel.Info, "New instance of Recursive check starting.");
        List<StoryNodeItem> _newList = new();
        try
        {
            foreach (StoryNodeItem _variable in list)
            {
                _newList.Add(_variable);
                _newList.AddRange(RecursiveCheck(_variable.Children));
            }
        }
        catch (Exception _exception) { _logger.LogException(LogLevel.Error, _exception, "Error in recursive check"); }
        
        return _newList;
    }

    /// <summary>
    /// This copies all unused scenes.
    /// </summary>
    private void CopyAllUnused()
    {
        //Recursively goes through the children of NarratorView View.
        try { foreach (StoryNodeItem _item in _shellVM.StoryModel.ExplorerView[0].Children) { RecurseCopyUnused(_item); } }
        catch (Exception _e) { _logger.LogException(LogLevel.Error, _e, "Error in recursive check"); }
    }

    /// <summary>
    /// Creates new section
    /// </summary>
    private void MakeSection()
    {
        if (_shellVM.DataSource == null || _shellVM.DataSource.Count < 0)
        {
            _logger.Log(LogLevel.Warn, "DataSource is empty or null, not adding section");
            return;
        }
        _ = new StoryNodeItem(new SectionModel(FlyoutText, _shellVM.StoryModel), _shellVM.StoryModel.NarratorView[0]);
    }

    /// <summary>
    /// This recursively copies any unused scene in the ExplorerView view.
    /// </summary>
    /// <param name="item">The parent item </param>
    private void RecurseCopyUnused(StoryNodeItem item)
    {
        _logger.Log(LogLevel.Trace, $"Recursing through {item.Name} ({item.Uuid})");
        try
        {
            if (item.Type == StoryItemType.Scene) //Check if scene/folder/section, if not then just continue.
            {
                //This calls recursive check, which returns flattens the entire the tree and .Any() checks if the UUID is in anywhere in the model.
                if (RecursiveCheck(_shellVM.StoryModel.NarratorView[0].Children).All(storyNodeItem => storyNodeItem.Uuid != item.Uuid)) 
                {
                    //Since the node isn't in the node, then we add it here.
                    _logger.Log(LogLevel.Trace, $"{item.Name} ({item.Uuid}) not found in Narrative view, adding it to the tree");
                    _ = new StoryNodeItem((SceneModel)_shellVM.StoryModel.StoryElements.StoryElementGuids[item.Uuid], _shellVM.StoryModel.NarratorView[0]);
                }
            }

            foreach (StoryNodeItem _child in item.Children) { RecurseCopyUnused(_child); }  
        }
        catch (Exception _ex)
        {
            _logger.LogException(LogLevel.Error, _ex, "Error in NarrativeTool.CopyAllUnused()");
            Message = "Error copying nodes.";
        }
    }
}
