using Avalonia.Interactivity;
using Ryujinx.Ava.UI.ViewModels.Input;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class GyroCalibrationWindow : StyleableAppWindow
    {
        private readonly GyroCalibrationViewModel _viewModel;

        public GyroCalibrationWindow() : this(null) { }

        public GyroCalibrationWindow(GyroCalibrationViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;

            if (_viewModel != null)
            {
                _viewModel.Closed += Close;
            }

            InitializeComponent();
        }

        private void OnStart(object sender, RoutedEventArgs e)
        {
            _viewModel?.Start();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            _viewModel?.Cancel();
        }

        private void OnApplyDeadzone(object sender, RoutedEventArgs e)
        {
            _viewModel?.ApplyAndClose();
        }

        private void OnKeepDeadzone(object sender, RoutedEventArgs e)
        {
            _viewModel?.KeepAndClose();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.Closed -= Close;
                _viewModel.Dispose();
            }
            base.OnClosed(e);
        }
    }
}
