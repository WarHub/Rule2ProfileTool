using WarHub.ArmouryModel.Source;

namespace WarHub.Rule2ProfileTool.Models
{
    public class ConverterMessage
    {
        public ConverterMessage(RuleNode rule, string text)
        {
            Rule = rule;
            Text = text;
        }

        public RuleNode Rule { get; }
        public string Text { get; }
    }
}
