namespace CityCare.Api.Dtos.Incidents;

public class VoteResponse
{
    public Guid IncidentId { get; set; }
    public int VoteCount { get; set; }
    public bool HasVoted { get; set; }
}
