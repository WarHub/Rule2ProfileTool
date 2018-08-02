using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using WarHub.Rule2ProfileTool.Services;

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

            LoadFolder = ReactiveCommand.Create(LoadFolderImpl);
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
                .Select(info => (info, LoadFileRoot(info)))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(tuple =>
                {
                    var (info, node) = tuple;
                    info.Name = $"{node.Name} (v{node.Revision.ToString()})";
                    info.Root = node;
                });

            RefreshRuleSelections = ReactiveCommand.Create(RefreshRuleSelectionsImpl);
            RefreshRuleSelections
                .Subscribe(results =>
                {
                    Rules.Clear();
                    Rules.AddRange(results);
                });

            MarkAllRules = ReactiveCommand.Create(MarkAllRulesImpl, Rules.IsEmptyChanged.Select(x => !x));
            
            Rules.ItemChanged
                .WithLatestFrom(MarkAllRules.IsExecuting, (changed, isExecuting) => isExecuting ? null : changed)
                .Where(x => x != null)
                .Subscribe(e =>
                {
                    if (!e.Sender.IsSelected)
                    {
                        AllRulesSelected = false;
                    }
                });

            SelectedDatafiles.Changed
                .Select(x => Unit.Default)
                .InvokeCommand(RefreshRuleSelections);

            RefreshProfileTypes = ReactiveCommand.Create(RefreshProfileTypesImpl);
            RefreshProfileTypes
                .Subscribe(results =>
                {
                    ProfileTypes.Clear();
                    ProfileTypes.AddRange(results);
                    SelectedProfileType = ProfileTypes.FirstOrDefault();
                });

            SelectedDatafiles.Changed
                .Select(x => Unit.Default)
                .InvokeCommand(RefreshProfileTypes);

            RefreshCharacteristicTypeInfos = ReactiveCommand.Create(RefreshCharacteristicTypeInfosImpl);
            RefreshCharacteristicTypeInfos
                .Subscribe(results =>
                {
                    CharacteristicInfos.Clear();
                    CharacteristicInfos.AddRange(results);
                    SelectedCharacteristicType = CharacteristicInfos.FirstOrDefault();
                });

            this.WhenAnyValue(x => x.SelectedProfileType)
                .Select(x => Unit.Default)
                .InvokeCommand(RefreshCharacteristicTypeInfos);

            DatafileConversionStatuses = SelectedDatafiles
                .CreateDerivedCollection(
                    x => new DatafileConversionStatus(x),
                    filter: null,
                    orderer: (left, right) => left.Info.Name.CompareTo(right.Info.Name));

            var canConvert =
                this.WhenAnyValue(
                    x => x.FolderPath,
                    x => x.FolderPathError,
                    x => x.SelectedDatafiles.IsEmpty,
                    x => x.SelectedProfileType,
                    x => x.SelectedCharacteristicType,
                    x => x.Rules.IsEmpty,
                    (path, error, emptyFiles, profileType, chType, emptyRules)
                    => !string.IsNullOrWhiteSpace(path) && error == null && !emptyFiles && profileType != null && chType != null && !emptyRules);

            Convert = ReactiveCommand.Create(ConvertImpl, canConvert);
            Convert.InvokeCommand(LoadFolder);
        }

        private void ConvertImpl()
        {
            var rules = Rules.Where(x => x.IsSelected).Select(x => x.Node).ToImmutableArray();
            var converter = new RuleConverter(SelectedProfileType.Node, SelectedCharacteristicType.Node, rules);
            foreach (var datafileStatus in DatafileConversionStatuses)
            {
                datafileStatus.ConversionProgressValue = 0.3;
                var catalogueBase = (CatalogueBaseNode)datafileStatus.Info.Document.GetRoot();
                var converted = converter.Convert(catalogueBase);
                datafileStatus.ConversionProgressValue = 0.9;
                using (var file = File.Open(datafileStatus.Info.Document.Filepath, FileMode.Create))
                {
                    GetSaveAction(converted)?.Invoke(file);
                }
                datafileStatus.ConversionProgressValue = 1;
            }

            Action<Stream> GetSaveAction(CatalogueBaseNode catalogueBase)
            {
                if (catalogueBase is CatalogueNode catalogue)
                {
                    return catalogue.Serialize;
                }
                if (catalogueBase is GamesystemNode gamesystem)
                {
                    return gamesystem.Serialize;
                }
                return null;
            }
        }

        private void MarkAllRulesImpl()
        {
            var selected = !AllRulesSelected;
            foreach (var rule in Rules)
            {
                rule.IsSelected = selected;
            }
            AllRulesSelected = selected;
        }

        private IEnumerable<RuleSelection> RefreshRuleSelectionsImpl()
        {
            var catalogues = SelectedDatafiles;
            var rules = catalogues
                .SelectMany(file =>
                {
                    var root = (CatalogueBaseNode)file.Document.GetRoot();
                    var ruleNodes = root
                    .DescendantsAndSelf(x =>
                            !x.IsKind(SourceKind.RepeatList) &&
                            !x.IsKind(SourceKind.ConstraintList) &&
                            !x.IsKind(SourceKind.ModifierList))
                    .Where(x => x.IsKind(SourceKind.Rule))
                    .OfType<RuleNode>()
                    .Select(node => new RuleSelection(node))
                    .ToList();
                    return ruleNodes;
                })
                .ToList();
            return rules;
        }

        private IEnumerable<ProfileTypeInfo> RefreshProfileTypesImpl()
        {
            var gsts = Datafiles.Where(x => x.Document.Kind == XmlDocumentKind.Gamesystem);
            var datafiles = gsts.Concat(SelectedDatafiles).Distinct();
            var profileTypes = datafiles
                .SelectMany(file =>
                {
                    var root = (CatalogueBaseNode)file.Document.GetRoot();
                    return root.ProfileTypes.Select(x => new ProfileTypeInfo(x));
                })
                .ToList();
            return profileTypes;
        }

        private IEnumerable<CharacteristicTypeInfo> RefreshCharacteristicTypeInfosImpl()
        {
            var profile = SelectedProfileType;
            return profile?.Node.CharacteristicTypes.Select(x => new CharacteristicTypeInfo(x)) ?? new CharacteristicTypeInfo[0];
        }

        private static CatalogueBaseNode LoadFileRoot(DatafileInfo info)
        {
            var root = (CatalogueBaseNode)info.Document.GetRoot();
            return root;
        }

        private IReadOnlyCollection<DatafileInfo> LoadFolderImpl()
        {
            var path = FolderPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return new DatafileInfo[0];
            }
            var workspace = XmlWorkspace.CreateFromDirectory(path);
            var infos = workspace.Documents
                .Where(x => x.Kind == XmlDocumentKind.Gamesystem || x.Kind == XmlDocumentKind.Catalogue)
                .Select(xml => new DatafileInfo(xml))
                .ToList();
            return infos;
        }

        private Subject<DatafileInfo> DatafilesToLoad { get; }

        private ReactiveCommand<Unit, IReadOnlyCollection<DatafileInfo>> LoadFolder { get; }

        private ReactiveCommand<Unit, IEnumerable<ProfileTypeInfo>> RefreshProfileTypes { get; }

        private ReactiveCommand<Unit, IEnumerable<CharacteristicTypeInfo>> RefreshCharacteristicTypeInfos { get; }

        private ReactiveCommand<Unit, IEnumerable<RuleSelection>> RefreshRuleSelections { get; }

        public ReactiveCommand<Unit, string> SelectFolder { get; }

        public ReactiveCommand<Unit, Unit> MarkAllRules { get; }

        public ReactiveCommand<Unit, Unit> Convert { get; }

        public ReactiveList<DatafileInfo> Datafiles { get; } = new ReactiveList<DatafileInfo>();

        public ReactiveList<DatafileInfo> SelectedDatafiles { get; } = new ReactiveList<DatafileInfo>();

        public IReactiveDerivedList<DatafileConversionStatus> DatafileConversionStatuses { get; }

        public ReactiveList<ProfileTypeInfo> ProfileTypes { get; } = new ReactiveList<ProfileTypeInfo>();

        public ReactiveList<CharacteristicTypeInfo> CharacteristicInfos { get; } = new ReactiveList<CharacteristicTypeInfo>();

        public ReactiveList<RuleSelection> Rules { get; } = new ReactiveList<RuleSelection>() { ChangeTrackingEnabled = true };

        private string _folderPath;
        private string _folderPathError;
        private CharacteristicTypeInfo _selectedCharacteristicType;
        private ProfileTypeInfo _selectedProfileType;
        private bool _allRulesSelected = false;

        public bool AllRulesSelected
        {
            get => _allRulesSelected;
            set => this.RaiseAndSetIfChanged(ref _allRulesSelected, value);
        }

        public string FolderPath
        {
            get => _folderPath;
            set => this.RaiseAndSetIfChanged(ref _folderPath, value);
        }

        public string FolderPathError
        {
            get => _folderPathError;
            private set => this.RaiseAndSetIfChanged(ref _folderPathError, value);
        }

        public ProfileTypeInfo SelectedProfileType
        {
            get => _selectedProfileType;
            set => this.RaiseAndSetIfChanged(ref _selectedProfileType, value);
        }

        public CharacteristicTypeInfo SelectedCharacteristicType
        {
            get => _selectedCharacteristicType;
            set => this.RaiseAndSetIfChanged(ref _selectedCharacteristicType, value);
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
