using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Bookstore.Domain;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading.Tasks;

namespace Bookstore.Data.FileServices
{
    public class S3FileService : IFileService
    {
        private readonly TransferUtility _transferUtility;
        private readonly IConfiguration _configuration;

        public S3FileService(IAmazonS3 s3Client, IConfiguration configuration)
        {
            _transferUtility = new TransferUtility(s3Client);
            _configuration = configuration;
        }

        public async Task DeleteAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            var bucketName = _configuration["Files/BucketName"];
            var request = new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = Path.GetFileName(filePath)
            };

            await _transferUtility.S3Client.DeleteObjectAsync(request);
        }

        public async Task<string> SaveAsync(Stream contents, string filename)
        {
            if (contents == null) return null;

            var bucketName = _configuration["Files/BucketName"];
            var cloudFrontDomain = _configuration["Files/CloudFrontDomain"];
            var uniqueFilename = $"{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}{Path.GetExtension(filename)}";

            var request = new TransferUtilityUploadRequest
            {
                BucketName = bucketName,
                InputStream = contents,
                Key = uniqueFilename
            };

            await _transferUtility.UploadAsync(request);

            return $"{cloudFrontDomain}/{uniqueFilename}";
        }
    }
}
