using System.IO;
using WarHub.ArmouryModel.Source;
using WarHub.ArmouryModel.Workspaces.BattleScribe;

namespace WarHub.Rule2ProfileTool.Models
{
    public partial class DatafileInfo
    {
        public XmlDocument Document { get; }

        public string Name { get; }

        public string Revision { get; }

        public SourceNode Root { get; }
    }
}