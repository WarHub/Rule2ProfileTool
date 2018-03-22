using System.IO;
using ReactiveUI;
using WarHub.ArmouryModel.Source;
using WarHub.ArmouryModel.Workspaces.BattleScribe;

namespace WarHub.Rule2ProfileTool.Models
{
    public class DatafileInfo : ReactiveObject
    {
        private string _name;
        private SourceNode _root;

        public DatafileInfo(XmlDocument document)
        {
            Document = document;
            _name = document.Name;
        }

        public XmlDocument Document { get; }

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public SourceNode Root
        {
            get => _root;
            set => this.RaiseAndSetIfChanged(ref _root, value);
        }
    }
}