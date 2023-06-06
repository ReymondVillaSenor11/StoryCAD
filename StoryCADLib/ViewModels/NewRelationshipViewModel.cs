using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StoryCAD.Models;
using StoryCAD.Services.Logging;

namespace StoryCAD.ViewModels
{
    public class NewRelationshipViewModel : ObservableRecipient
    {
        # region private Fields
        
        private readonly LogService _logger;
        private readonly CharacterViewModel _charVM;
        private bool _changeable;
        private bool _changed;

        #endregion

        #region public Properties
        public StoryElement Member { get; set; }

        public ObservableCollection<StoryElement> ProspectivePartners { get; set; }

        private StoryElement _selectedPartner;
        public StoryElement SelectedPartner
        {
            get => _selectedPartner;
            set => SetProperty(ref _selectedPartner, value);
        }

        public ObservableCollection<string> RelationTypes { get; set; }

        private string _relationType;
        public string RelationType
        {
            get => _relationType;
            set => SetProperty(ref _relationType, value);
        }

        public ObservableCollection<string> InverseRelationTypes { get; set; }

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

        public NewRelationshipViewModel(CharacterViewModel charVM, LogService _logger)
        {
            _logger = _logger ?? throw new ArgumentNullException(nameof(_logger));
            _charVM = charVM ?? throw new ArgumentNullException(nameof(charVM));

            PropertyChanged += OnPropertyChanged;
            Member = null;
            ProspectivePartners = new ObservableCollection<StoryElement>();
            RelationTypes = new ObservableCollection<string>();
            InverseRelationTypes = new ObservableCollection<string>();
        }

        #endregion

        #region public Methods

        public void InitializeNewRelationshipVM()
        {
            _changeable = false;

            Member = _charVM.Model;
            RefreshProspectivePartners();
            RefreshRelationTypes();
            RefreshInverseRelationTypes();

            // Initialize the rest of the properties
            RelationType = "";
            InverseRelationship = false;
            InverseRelationType = "";
            SelectedPartner = null;

            _changeable = true;
        }

        #endregion

        #region private methods 

        /// <summary>
        /// Prospective partners are chars who are not in a relationship with the
        /// Member character (the one who is creating the relationship).
        /// </summary>
        private void RefreshProspectivePartners()
        {

            ProspectivePartners.Clear();
            StoryModel _storyModel = ShellViewModel.GetModel();

            foreach (StoryElement _character in _storyModel.StoryElements.Characters)
            {
                if (_character == Member)
                    continue;

                bool isAlreadyInRelationship = _charVM.CharacterRelationships.Any(_rel => _character.Equals(_rel.Partner));

                if (!isAlreadyInRelationship)
                {
                    ProspectivePartners.Add(_character);
                }
            }
        }

        private void RefreshRelationTypes()
        {
            PopulateRelationTypesCollection(RelationTypes);
        }

        private void RefreshInverseRelationTypes()
        {
            PopulateRelationTypesCollection(InverseRelationTypes);
        }

        private void PopulateRelationTypesCollection(ObservableCollection<string> collection)
        {
            collection.Clear();
            foreach (string _relationshipType in GlobalData.RelationTypes)
            {
                collection.Add(_relationshipType);
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            //TODO: forward _changed to CharacterViewModel
            if (_changeable)
            {
                _changed = true;
                //_logger.Log(LogLevel.Info, $"CharacterViewModel.OnPropertyChanged: {args.PropertyName} changed");
                //ShellViewModel.ShowChange();
            }
        }

        #endregion

    }
}
