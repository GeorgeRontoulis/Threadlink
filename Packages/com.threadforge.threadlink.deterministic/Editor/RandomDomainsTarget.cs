namespace Threadforge.Threadlink.Editor
{
    /// <summary>StatelessRNG random domains, generated as <c>RandomDomains.Name</c> from ID Domain assets.</summary>
    /// <remarks>A new entry starts with the value <c>StatelessRNG.Domain.Named(name)</c> gives, so replacing a named
    /// domain with its generated one keeps every stream identical.</remarks>
    public sealed class RandomDomainsTarget : IIdDomainTarget
    {
        /// <inheritdoc />
        public string Id => "StatelessRNG.Domains";

        /// <inheritdoc />
        public string DisplayName => "Random domains (StatelessRNG)";

        /// <inheritdoc />
        public string ClassName => "RandomDomains";

        /// <inheritdoc />
        public string Summary => "StatelessRNG random domains. Keep values stable once saves or replays depend on them.";

        /// <inheritdoc />
        public string RequiredAssembly => "Threadforge.Threadlink.Deterministic";

        /// <inheritdoc />
        public int InitialValue(string name) => unchecked((int)StatelessRNG.Domain.Named(name).Id);

        /// <inheritdoc />
        public GeneratedIds.Member Member(IdDomain domain, IdDomain.Entry entry) =>
            new GeneratedIds.Member
            {
                Name = entry.Name,
                Key = entry.Value.ToString(),
                Type = "global::Threadforge.Threadlink.StatelessRNG.Domain",
                Value = "new global::Threadforge.Threadlink.StatelessRNG.Domain(" + unchecked((uint)entry.Value) + "u)",
                Summary = string.IsNullOrEmpty(entry.Summary) ? "Random domain " + entry.Name + " (" + domain.name + ")." : entry.Summary,
            };
    }
}
