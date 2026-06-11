using Minio;
using Minio.DataModel.Args;

namespace CityCare.Api.Services;

public sealed class MinioOptions
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string Bucket { get; set; } = "citycare-photos";
    public bool UseSSL { get; set; }
    public string PublicBaseUrl { get; set; } = "http://localhost:9000";
    public int MaxFileSizeMb { get; set; } = 5;
}

public sealed class PhotoStorageService
{
    private readonly IMinioClient _minio;
    private readonly MinioOptions _options;
    private readonly ILogger<PhotoStorageService> _logger;

    public PhotoStorageService(
        IMinioClient minio,
        MinioOptions options,
        ILogger<PhotoStorageService> logger)
    {
        _minio = minio;
        _options = options;
        _logger = logger;
    }

    public long MaxFileSizeBytes => (long)_options.MaxFileSizeMb * 1024 * 1024;

    public async Task UploadAsync(
        string objectKey,
        Stream data,
        long size,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);

        var putArgs = new PutObjectArgs()
            .WithBucket(_options.Bucket)
            .WithObject(objectKey)
            .WithStreamData(data)
            .WithObjectSize(size)
            .WithContentType(contentType);

        await _minio.PutObjectAsync(putArgs, cancellationToken);
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var removeArgs = new RemoveObjectArgs()
            .WithBucket(_options.Bucket)
            .WithObject(objectKey);

        await _minio.RemoveObjectAsync(removeArgs, cancellationToken);
    }

    public string BuildPublicUrl(string objectKey) =>
        $"{_options.PublicBaseUrl.TrimEnd('/')}/{_options.Bucket}/{objectKey}";

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        var exists = await _minio.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_options.Bucket), cancellationToken);

        if (exists)
            return;

        await _minio.MakeBucketAsync(
            new MakeBucketArgs().WithBucket(_options.Bucket), cancellationToken);

        // Politique de lecture publique pour que les URLs soient consultables directement
        var policy = $$"""
        {
          "Version": "2012-10-17",
          "Statement": [
            {
              "Effect": "Allow",
              "Principal": { "AWS": ["*"] },
              "Action": ["s3:GetObject"],
              "Resource": ["arn:aws:s3:::{{_options.Bucket}}/*"]
            }
          ]
        }
        """;

        await _minio.SetPolicyAsync(
            new SetPolicyArgs().WithBucket(_options.Bucket).WithPolicy(policy),
            cancellationToken);

        _logger.LogInformation("Bucket MinIO '{Bucket}' créé avec lecture publique.", _options.Bucket);
    }
}
