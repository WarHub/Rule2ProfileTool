using WarHub.ArmouryModel.Source;

namespace WarHub.Rule2ProfileTool.Models
{
    public class CharacteristicTypeInfo
    {
        public CharacteristicTypeInfo(CharacteristicTypeNode node)
        {
            Node = node;
        }

        public CharacteristicTypeNode Node { get; }
    }
}