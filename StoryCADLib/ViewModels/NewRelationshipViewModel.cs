﻿using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StoryCAD.Models;

namespace StoryCAD.ViewModels;

public class NewRelationshipViewModel : ObservableRecipient
{
    #region public Properties
    public StoryElement Member { get; set; }

    public ObservableCollection<StoryElement> ProspectivePartners;
    
    private StoryElement _selectedPartner;
    public StoryElement SelectedPartner 
    {
        get => _selectedPartner;
        set => SetProperty(ref _selectedPartner, value);
    }

    private ObservableCollection<string> _RelationTypes;
    /// <summary>
    /// Presets of possible relationships, i.e mother, farther, etc.
    /// </summary>
    public ObservableCollection<string> RelationTypes
    {
        get => _RelationTypes;
        set => SetProperty(ref _RelationTypes, value);
    }

    private string _relationType;
    public string RelationType 
    { 
        get => _relationType; 
        set => SetProperty(ref _relationType, value);
    }
    private string _inverseRelationType;
    public string InverseRelationType
    {
        get => _inverseRelationType;
        set => SetProperty(ref _inverseRelationType, value);
    }
    private bool _inverseRelationship;
    public bool InverseRelationship
    {
        get => _inverseRelationship;
        set => SetProperty(ref _inverseRelationship, value);
    }

    #endregion

    #region Constructor

    public NewRelationshipViewModel(StoryElement member)
    {
        Member = member;
        ProspectivePartners = new ObservableCollection<StoryElement>();
        RelationTypes = new ObservableCollection<string>();
    }

    #endregion

}