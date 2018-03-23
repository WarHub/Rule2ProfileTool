using System.Linq;
using ReactiveUI;
using WarHub.ArmouryModel.Source;

namespace WarHub.Rule2ProfileTool.Models
{
    public class RuleSelection : ReactiveObject
    {
        private bool _isSelected;

        public RuleSelection(RuleNode node)
        {
            Node = node;
            var root = (CatalogueBaseNode) node.Ancestors().Last();
            SourceName = $"{root.Name} (v{root.Revision})";
        }
        public RuleNode Node { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }
        public string SourceName { get; }
    }
}