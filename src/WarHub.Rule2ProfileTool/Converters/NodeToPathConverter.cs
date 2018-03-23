using System;
using System.Globalization;
using System.Linq;
using Avalonia.Markup;
using WarHub.ArmouryModel.Source;

namespace WarHub.Rule2ProfileTool.Converters
{
    public class NodeToPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var node = (SourceNode)value;

            var pathNames = node.Ancestors()
                .Select(x => x is INameableNode named ? named.Name : x.GetType().Name);
            var path = string.Join(parameter is string separator ? separator : " -> ", pathNames);
            return path;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}