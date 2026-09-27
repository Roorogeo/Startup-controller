using StartupSelector.Models;

namespace StartupSelector.ViewModels
{
    public sealed class PresetViewModel : ObservableObject
    {
        private bool _isDefault;

        public PresetViewModel(Preset model, bool isDefault)
        {
            Model = model;
            _isDefault = isDefault;
        }

        public Preset Model { get; }

        public string Name
        {
            get => Model.Name;
            set
            {
                if (Model.Name != value)
                {
                    Model.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsDefault
        {
            get => _isDefault;
            set => SetProperty(ref _isDefault, value);
        }

        public bool Includes(string entryId) => Model.IncludeAllManaged || Model.CheckedIds.Contains(entryId);
    }
}
