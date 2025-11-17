using Ryujinx.Ava.UI.Models;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class UserProfileViewModel : BaseModel, IDisposable
    {
        public UserProfileViewModel()
        {
            Profiles = new ObservableCollection<BaseModel>();
            LostProfiles = new ObservableCollection<UserProfile>();
            LostProfiles.CollectionChanged += LostProfilesChanged;
        }

        public ObservableCollection<BaseModel> Profiles { get; }

        public ObservableCollection<UserProfile> LostProfiles { get; }

        public bool IsEmpty => LostProfiles.Count == 0;

        public void Dispose()
        {
            LostProfiles.CollectionChanged -= LostProfilesChanged;
        }

        private void LostProfilesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsEmpty));
        }

        public void UpdateLostProfiles(ObservableCollection<UserProfile> newProfiles)
        {
            LostProfiles.Clear();
            
            foreach (var profile in newProfiles)
                LostProfiles.Add(profile);

            OnPropertyChanged(nameof(IsEmpty));
        }
    }
}
