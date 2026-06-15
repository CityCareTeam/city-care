namespace CityCare.Core.Entities;

public class IncidentPhoto
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }
    public Incident Incident { get; set; } = null!;

    public Guid UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;

    // Clé de l'objet dans le bucket MinIO (ex: incidents/{incidentId}/{guid}.jpg)
    public string ObjectKey { get; set; } = null!;

    // Nom de fichier original fourni par le client
    public string FileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }

    public DateTime CreatedAt { get; set; }
}
