using System;
using Microsoft.Build.Locator;

class Program
{
    static int Main()
    {
        try
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances();
            if (instances == null)
            {
                Console.WriteLine("No instances returned");
                return 0;
            }
            foreach (var inst in instances)
            {
                Console.WriteLine("--- INSTANCE START ---");
                Console.WriteLine($"Name: {inst.Name}");
                Console.WriteLine($"Version: {inst.Version}");
                Console.WriteLine($"DiscoveryType: {inst.DiscoveryType}");
                Console.WriteLine($"MSBuildPath: {inst.MSBuildPath}");
                Console.WriteLine("--- INSTANCE END ---");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.ToString());
            return 2;
        }
    }
}
