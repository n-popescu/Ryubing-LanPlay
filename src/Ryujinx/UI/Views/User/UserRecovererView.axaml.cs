using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.ViewModels;

namespace Ryujinx.Ava.UI.Views.User
{
    public partial class UserRecovererView : RyujinxControl<UserProfileViewModel>
    {
        private NavigationDialogHost _parent;

        public UserRecovererView()
        {
            InitializeComponent();
            AddHandler(Frame.NavigatedToEvent, (s, e) =>
            {
                NavigatedTo(e);
            }, RoutingStrategies.Direct);
        }

        private void NavigatedTo(NavigationEventArgs arg)
        {
            if (Program.PreviewerDetached)
            {
                switch (arg.NavigationMode)
                {
                    case NavigationMode.New:
                    case NavigationMode.Back:
                        NavigationDialogHost parent = (NavigationDialogHost)arg.Parameter;

                        _parent = parent;

                        ((ContentDialog)_parent.Parent).Title = $"{LocaleManager.Instance[LocaleKeys.UserProfileWindowTitle]} - {LocaleManager.Instance[LocaleKeys.UserProfilesRecoverLostAccounts]}";

                        break;
                }
            }
        }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            _parent?.GoBack();
        }
    }
}
