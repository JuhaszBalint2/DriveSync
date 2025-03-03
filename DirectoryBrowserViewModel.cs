using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DriveSync.Infrastructure.Services;
using DriveSync.WPF.Localization;
using Microsoft.Extensions.Logging;

namespace DriveSync.WPF.ViewModels
{
    public partial class DirectoryBrowserViewModel : ObservableObject
    {
        private readonly IRcloneService _rcloneService;
        private readonly ILogger<DirectoryBrowserViewModel> _logger;
        private readonly string _remoteName;
        private readonly bool _isSourceRemote;
        private DirectoryItem _selectedItem;

        public DirectoryItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    HasSelection = value != null;
                }
            }
        }

        private ObservableCollection<DirectoryItem> _items;
        public ObservableCollection<DirectoryItem> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        private string _dialogTitle;
        public string DialogTitle
        {
            get => _dialogTitle;
            set => SetProperty(ref _dialogTitle, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _hasSelection;
        public bool HasSelection
        {
            get => _hasSelection;
            set => SetProperty(ref _hasSelection, value);
        }

        public bool IsSourceRemote => _isSourceRemote;
        public string RemoteName => _remoteName;

        public string SelectedPath => SelectedItem?.Name;

        public DirectoryBrowserViewModel(
            IRcloneService rcloneService,
            ILogger<DirectoryBrowserViewModel> logger,
            string remoteName,
            bool isSourceRemote = true)
        {
            _rcloneService = rcloneService;
            _logger = logger;
            _remoteName = remoteName;
            _isSourceRemote = isSourceRemote;

            DialogTitle = string.Format(
                LocalizationManager.Instance["SelectDirectoryFromRemote"],
                remoteName
            );
            Items = new ObservableCollection<DirectoryItem>();
            LoadDirectoriesAsync().ConfigureAwait(false);

            LocalizationManager.Instance.PropertyChanged += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "CurrentLanguage")
                {
                    DialogTitle = string.Format(
                        LocalizationManager.Instance["SelectDirectoryFromRemote"],
                        remoteName
                    );
                }
            };
        }

        private async Task LoadDirectoriesAsync()
        {
            try
            {
                IsLoading = true;
                var directories = await _rcloneService.ListDirectories(_remoteName);
                Items.Clear();
                foreach (var dir in directories)
                {
                    Items.Add(new DirectoryItem(dir));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load directories for {_remoteName}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class DirectoryItem
    {
        public string Name { get; }

        public DirectoryItem(string name)
        {
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}