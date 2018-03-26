using WarHub.ArmouryModel.Source;

namespace WarHub.Rule2ProfileTool.Models
{
    public class ConverterMessage
    {
        public ConverterMessage(SourceNode node, string text)
        {
            Node = node;
            Text = text;
        }

        public SourceNode Node { get; }
        public string Text { get; }
    }
}
