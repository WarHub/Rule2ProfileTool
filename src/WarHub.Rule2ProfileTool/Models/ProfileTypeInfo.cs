using System.Collections.Generic;
using System.Linq;
using System.Text;
using WarHub.ArmouryModel.Source;

namespace WarHub.Rule2ProfileTool.Models
{
    public class ProfileTypeInfo
    {
        public ProfileTypeInfo(ProfileTypeNode node)
        {
            Node = node;
            var root = (CatalogueBaseNode)node.Ancestors().Last();
            SourceName = root.Name;
        }

        /// <summary>
        /// Gets the name of the datafile containing the profile type definition.
        /// </summary>
        public string SourceName { get; }

        public ProfileTypeNode Node { get; }
    }
}
