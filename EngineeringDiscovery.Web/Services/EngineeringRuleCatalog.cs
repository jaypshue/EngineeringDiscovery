using System.Collections.Generic;

namespace EngineeringDiscovery.Web.Services
{
    internal static class EngineeringRuleCatalog
    {
        // Return member-focused engineering rules. Keep deterministic order.
        public static IEnumerable<IEngineeringRule> MemberRules()
        {
            return new IEngineeringRule[]
            {
                new LongMethodRule(),
                new ExcessiveParameterRule(),
                new LargeConstructorRule(),
                new AsyncNamingRule(),
                new LargePublicSurfaceAreaRule()
            };
        }

    // Return type-focused engineering rules. Keep deterministic order.
    public static IEnumerable<IEngineeringRule> TypeRules()
    {
        return new IEngineeringRule[]
        {
            new LargeTypeRule(),
            new LargeInterfaceRule(),
            new DeepInheritanceRule(),
            new ExcessivePublicFieldsRule(),
            new MixedResponsibilityRule()
        };
    }
    }
}
