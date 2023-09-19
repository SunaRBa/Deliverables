using System;
using System.Windows;

namespace Debuger
{
    public partial class DebuggerIDE : Window
    {
        private DebugggerIDEViewModel _viewModel;

        public DebuggerIDE()
        {
            InitializeComponent();
            _viewModel = new DebugggerIDEViewModel(this, OutputTextBox);
            DataContext = _viewModel;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _viewModel.Dispose();
        }
    }
}