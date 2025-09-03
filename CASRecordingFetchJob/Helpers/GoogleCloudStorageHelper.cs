using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;

namespace CASRecordingFetchJob.Helpers
{
    public class GoogleCloudStorageHelper
    {
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;

        public GoogleCloudStorageHelper(IConfiguration configuration)
        {
            var googleAuthFilePath = configuration.GetValue<string>("GoogleAuthFilePath")
                                     ?? throw new ArgumentNullException("GoogleAuthFilePath not configured");
            _bucketName = configuration.GetValue<string>("GCSBucketName")
                          ?? throw new ArgumentNullException("GCSBucketName not configured");

            var credential = GoogleCredential.FromFile(googleAuthFilePath);
            _storageClient = StorageClient.Create(credential);
        }

        public async Task UploadRecordingAsync(string key, Stream stream, string? contentType = null, CancellationToken cancellationToken = default)
        {
            if (stream == null || !stream.CanRead)
                throw new ArgumentException("Invalid stream provided", nameof(stream));

            await _storageClient.UploadObjectAsync(
                bucket: _bucketName,
                objectName: key,
                contentType: contentType,
                source: stream,
                cancellationToken: cancellationToken
            );
        }
    }
}
