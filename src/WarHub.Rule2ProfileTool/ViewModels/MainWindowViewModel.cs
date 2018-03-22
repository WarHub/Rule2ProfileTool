﻿using System;
using System.Collections;
using System.Collections.Generic;
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
    public class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            SelectFolder = ReactiveCommand.CreateFromTask(async ct =>
            {
                var dialog = new OpenFolderDialog()
                {
                    Title = "Select folder with datafiles"
                };
                var path = await dialog.ShowAsync();
                return path;
            });
            SelectFolder.Subscribe(path => FolderPath = path);

            var canLoadFolder = this.WhenAnyValue(x => x.FolderPath, path => !string.IsNullOrWhiteSpace(path))
                .Do(x => Log.Debug("Can execute: {CanExecute}", x));

            LoadFolder = ReactiveCommand.Create(LoadFolderImpl, canLoadFolder);
            LoadFolder.Subscribe(results =>
            {
                Datafiles.Clear();
                Datafiles.AddRange(results);
            }, exception =>
            {
                Log.Warning(exception, "Error when loading folder");
            });

            this.WhenAnyValue(x => x.FolderPath)
                .Do(x => Log.Debug("Wybrana ścieżka1: {Path}", x))
                .Throttle(TimeSpan.FromSeconds(0.5), RxApp.MainThreadScheduler)
                .Do(x => Log.Debug("Wybrana ścieżka2: {Path}", x))
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

        public ReactiveList<DatafileInfo> Datafiles { get; } = new ReactiveList<DatafileInfo>();

        string _folderPath;
        private IList _selectedDatafiles;

        public string FolderPath
        {
            get => _folderPath;
            private set => this.RaiseAndSetIfChanged(ref _folderPath, value);
        }

        public IList SelectedDatafiles
        {
            get => _selectedDatafiles;
            set => this.RaiseAndSetIfChanged(ref _selectedDatafiles, value);
        }
    }
}
