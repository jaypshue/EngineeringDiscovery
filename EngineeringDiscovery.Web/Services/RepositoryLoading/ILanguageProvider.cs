namespace EngineeringDiscovery.Web.Services.RepositoryLoading
{
    /// <summary>
    /// Specialization of IEngineeringSourceProvider for traditional programming languages.
    /// Language providers (C#, Java, TypeScript) should implement this interface.
    /// </summary>
    internal interface ILanguageProvider : IEngineeringSourceProvider
    {
    }
}
