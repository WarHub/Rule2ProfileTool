using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using ReactiveUI;
using Serilog;
using WarHub.ArmouryModel.Source;
using WarHub.ArmouryModel.Source.BattleScribe;
using WarHub.ArmouryModel.Workspaces.BattleScribe;
using WarHub.Rule2ProfileTool.Models;

namespace WarHub.Rule2ProfileTool.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, INotifyDataErrorInfo
    {
        public MainWindowViewModel()
        {
            SelectFolder = ReactiveCommand.CreateFromTask(async ct =>
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "Select folder with datafiles"
                };
                var path = await dialog.ShowAsync();
                return path;
            });
            SelectFolder.Subscribe(path => FolderPath = path);

            var canLoadFolder = this.WhenAnyValue(x => x.FolderPath, path => !string.IsNullOrWhiteSpace(path));

            LoadFolder = ReactiveCommand.Create(LoadFolderImpl, canLoadFolder);
            LoadFolder.ThrownExceptions
                .Subscribe(exception =>
                {
                    if (exception is DirectoryNotFoundException dirNotFound)
                    {
                        FolderPathError = dirNotFound.Message;
                    }
                    Log.Warning(exception, "Error when loading folder");
                });
            LoadFolder.Subscribe(results =>
            {
                Datafiles.Clear();
                Datafiles.AddRange(results);
                FolderPathError = null;
            });

            this.WhenAnyValue(x => x.FolderPathError)
                .DistinctUntilChanged()
                .Subscribe(x => ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(FolderPath))));

            this.WhenAnyValue(x => x.FolderPath)
                .Throttle(TimeSpan.FromSeconds(0.5), RxApp.MainThreadScheduler)
                .Select(x => Unit.Default)
                .InvokeCommand(this, x => x.LoadFolder);

            DatafilesToLoad = new Subject<DatafileInfo>();

            Datafiles.ActOnEveryObject(item => DatafilesToLoad.OnNext(item), item => { });

            this.WhenAnyObservable(x => x.DatafilesToLoad)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(LoadFileRoot)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(tuple =>
                {
                    var (info, node) = tuple;
                    info.Name = $"{node.Name} (v{node.Revision.ToString()})";
                    info.Root = node;
                });

            DatafileConversionInfos = SelectedDatafiles
                .CreateDerivedCollection(
                    x => new DatafileConversionViewModel(x),
                    filter: null,
                    orderer: (left, right) => left.Info.Name.CompareTo(right.Info.Name));

            var canConvert =
                this.WhenAnyValue(
                    x => x.FolderPath,
                    x => x.FolderPathError,
                    (path,error) => !string.IsNullOrWhiteSpace(path) && error == null);

            Convert = ReactiveCommand.Create(() =>{}, canConvert);
        }

        private static (DatafileInfo info, CatalogueBaseNode node) LoadFileRoot(DatafileInfo info)
        {
            var root = (CatalogueBaseNode)info.Document.GetRoot();
            return (info, root);
        }

        private IReadOnlyCollection<DatafileInfo> LoadFolderImpl()
        {
            var path = FolderPath;
            var workspace = XmlWorkspace.CreateFromDirectory(path);
            var infos = workspace.Documents
                .Where(x => x.Kind == XmlDocumentKind.Gamesystem || x.Kind == XmlDocumentKind.Catalogue)
                .Select(xml => new DatafileInfo(xml))
                .ToList();
            return infos;
        }

        private Subject<DatafileInfo> DatafilesToLoad { get; }

        private ReactiveCommand<Unit, IReadOnlyCollection<DatafileInfo>> LoadFolder { get; }

        public ReactiveCommand<Unit, string> SelectFolder { get; }

        public ReactiveCommand<Unit, Unit> Convert { get; }

        public ReactiveList<DatafileInfo> Datafiles { get; } = new ReactiveList<DatafileInfo>();

        public ReactiveList<DatafileInfo> SelectedDatafiles { get; } = new ReactiveList<DatafileInfo>();

        public IReactiveDerivedList<DatafileConversionViewModel> DatafileConversionInfos { get; }

        private string _folderPath;
        private string _folderPathError;

        public string FolderPath
        {
            get => _folderPath;
            private set => this.RaiseAndSetIfChanged(ref _folderPath, value);
        }

        public string FolderPathError
        {
            get => _folderPathError;
            private set => this.RaiseAndSetIfChanged(ref _folderPathError, value);
        }

        public IEnumerable GetErrors(string propertyName)
        {
            switch (propertyName)
            {
                case nameof(FolderPath):
                    return new[] { _folderPathError };
                default:
                    return Enumerable.Empty<object>();
            }
        }

        public bool HasErrors { get; }
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;
    }
}
