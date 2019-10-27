﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Torch.API.WebAPI;
using Torch.Collections;
using Torch.Server.Annotations;

namespace Torch.Server.Views
{
    /// <summary>
    /// Interaction logic for PluginBrowser.xaml
    /// </summary>
    public partial class PluginBrowser : Window, INotifyPropertyChanged
    {
        public MtObservableList<PluginItem> Plugins { get; set; } = new MtObservableList<PluginItem>();
        public PluginItem CurrentItem { get; set; }

        private string _description = "Loading data from server, please wait..";
        public string CurrentDescription
        {
            get { return _description; }
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public PluginBrowser()
        {
            InitializeComponent();

            Task.Run(async () =>
                     {
                         var res = await PluginQuery.Instance.QueryAll();
                         if (res == null)
                             return;
                         foreach (var item in res.Plugins)
                             Plugins.Add(item);
                         PluginsList.Dispatcher.Invoke(() => PluginsList.SelectedIndex = 0);
                     });

            MarkdownFlow.CommandBindings.Add(new CommandBinding(NavigationCommands.GoToPage, (sender, e) => OpenUri((string)e.Parameter)));
        }

        public static bool IsValidUri(string uri)
        {
            if (!Uri.IsWellFormedUriString(uri, UriKind.Absolute))
                return false;
            Uri tmp;
            if (!Uri.TryCreate(uri, UriKind.Absolute, out tmp))
                return false;
            return tmp.Scheme == Uri.UriSchemeHttp || tmp.Scheme == Uri.UriSchemeHttps;
        }

        public static bool OpenUri(string uri)
        {
            if (!IsValidUri(uri))
                return false;
            System.Diagnostics.Process.Start(uri);
            return true;
        }

        private void PluginsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CurrentItem = (PluginItem)PluginsList.SelectedItem;
            CurrentDescription = CurrentItem.Description;
            DownloadButton.IsEnabled = !string.IsNullOrEmpty(CurrentItem.LatestVersion);
        }

        private void DownloadButton_OnClick(object sender, RoutedEventArgs e)
        {
            var item = CurrentItem;
            TorchBase.Instance.Config.Plugins.Add(new Guid(item.ID));
            TorchBase.Instance.Config.Save();
            Task.Run(async () =>
                     {
                         var result = await PluginQuery.Instance.DownloadPlugin(item.ID);
                         MessageBox.Show(result ? "Plugin downloaded successfully! Please restart the server to load changes."
                                                : "Plugin failed to download! See log for details.", 
                                                "Plugin Downloader",
                                                MessageBoxButton.OK);
                     });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
