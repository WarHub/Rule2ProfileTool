using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using WarHub.ArmouryModel.Source;
using WarHub.Rule2ProfileTool.Models;

namespace WarHub.Rule2ProfileTool.Services
{
    public class RuleConverter
    {
        public RuleConverter(ProfileTypeNode profileType, CharacteristicTypeNode characteristicType, ImmutableArray<RuleNode> rules)
        {
            ProfileType = profileType;
            CharacteristicType = characteristicType;
            Rules = rules;
            Profiles = Rules.ToImmutableDictionary(x => x.Id, ConvertRuleToProfile);
        }

        public ProfileTypeNode ProfileType { get; }
        public CharacteristicTypeNode CharacteristicType { get; }
        public ImmutableArray<RuleNode> Rules { get; }

        /// <summary>
        /// Gets dictionary of profiles converted from rules, keyed by ID of profile/rule.
        /// </summary>
        public ImmutableDictionary<string, ProfileNode> Profiles { get; }

        public IReadOnlyReactiveList<ConverterMessage> Messages => MessagesCore;

        private ReactiveList<ConverterMessage> MessagesCore { get; } = new ReactiveList<ConverterMessage>();

        public CatalogueBaseNode Convert(CatalogueBaseNode node)
        {
            var (profiles, rules) = ConvertRulesAndProfiles(node.Profiles, node.Rules);
            var (sharedProfiles, sharedRules) = ConvertRulesAndProfiles(node.SharedProfiles, node.SharedRules);
            var infoLinks = ConvertLinks(node.InfoLinks).ToArray();
            var selectionEntries = node.SelectionEntries.Select(ConvertSelectionEntryBase).ToArray();
            var sharedSelectionEntries = node.SharedSelectionEntries.Select(ConvertSelectionEntryBase).ToArray();
            var sharedSelectionEntryGroups = node.SharedSelectionEntryGroups.Select(ConvertSelectionEntryBase).ToArray();
            var newNode = node
                .WithProfiles(profiles)
                .WithRules(rules)
                .WithSharedProfiles(sharedProfiles)
                .WithSharedRules(sharedRules)
                .WithSharedSelectionEntries(sharedSelectionEntries)
                .WithSharedSelectionEntryGroups(sharedSelectionEntryGroups)
                .WithSelectionEntries(selectionEntries);
            return newNode;
        }

        private T ConvertSelectionEntryBase<T>(T node) where T : SelectionEntryBaseNode
        {
            var (profiles, rules) = ConvertRulesAndProfiles(node.Profiles, node.Rules);
            var infoLinks = ConvertLinks(node.InfoLinks).ToArray();
            var selectionEntries = node.SelectionEntries.Select(ConvertSelectionEntryBase).ToArray();
            var selectionEntryGroups = node.SelectionEntryGroups.Select(ConvertSelectionEntryBase).ToArray();
            return (T)node
                .WithRules(rules)
                .WithProfiles(profiles)
                .WithInfoLinks(infoLinks)
                .WithSelectionEntries(selectionEntries)
                .WithSelectionEntryGroups(selectionEntryGroups);
        }

        private IEnumerable<InfoLinkNode> ConvertLinks(IEnumerable<InfoLinkNode> links)
        {
            return links.Select(ConvertLink);
        }

        private InfoLinkNode ConvertLink(InfoLinkNode link)
        {
            var newLink = Profiles.ContainsKey(link.TargetId) ? link.WithType(InfoLinkKind.Profile) : link;
            if (link.Modifiers.Any())
            {
                MessagesCore.Add(new ConverterMessage(link, "The rule link has modifiers, all of which were copied without conversion and may cause problems."));
            }
            return newLink;
        }

        private (ProfileNode[], RuleNode[]) ConvertRulesAndProfiles(NodeList<ProfileNode> nodeProfiles, NodeList<RuleNode> nodeRules)
        {
            var rules = nodeRules.Except(Rules).ToArray();
            var profiles = nodeProfiles.AddRange(nodeRules.Intersect(Rules).Select(x => Profiles[x.Id])).ToArray();
            return (profiles, rules);
        }

        public ProfileNode ConvertRuleToProfile(RuleNode rule)
        {
            var profileBuilder = new ProfileCore.Builder()
            {
                Id = rule.Id,
                Name = rule.Name,
                ProfileTypeId = ProfileType.Id,
                ProfileTypeName = ProfileType.Name,
                Book = rule.Book,
                Page = rule.Page,
                IsHidden = rule.IsHidden
            };
            var profileNode = profileBuilder.ToImmutable().ToNode()
                .WithInfoLinks(rule.InfoLinks)
                .WithProfiles(rule.Profiles)
                .WithRules(rule.Rules)
                .WithModifiers(rule.Modifiers)
                .AddCharacteristics(CreateCharacteristics());
            if (rule.Modifiers.Any())
            {
                MessagesCore.Add(new ConverterMessage(rule, "The rule has modifiers, all of which were copied without conversion and may cause problems."));
            }
            return profileNode;

            IEnumerable<CharacteristicNode> CreateCharacteristics()
            {
                return ProfileType.CharacteristicTypes
                    .Select(x => new CharacteristicCore.Builder
                    {
                        CharacteristicTypeId = x.Id,
                        Name = x.Name,
                        Value = x == CharacteristicType ? rule.Description : null
                    })
                    .Select(x => x.ToImmutable().ToNode());
            }
        }
    }
}
