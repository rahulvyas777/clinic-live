namespace ClinicLive.Pocket.Shared.Services;

/// <summary>Where the clinic's server is. Each host decides; everything shared just asks.</summary>
public sealed record ClinicEndpoint(Uri Base)
{
    public Uri QueueHub => new(Base, "hubs/queue");
}
