using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VideoWebApp.Interface
{
    public interface IAzureService
    {

        string GenerateSasToken(string containerName, string blobName);
        Task<string> UploadFileToBlobAsync(string containerName, string filePath);
        string RetrieveFileFromStorage(string containerName, string FileName);
        Task DeleteFileFromStorage(string containerName, string FileName);
        Task<IEnumerable<string>> ListFilesInContainer(string containerName);
        Task<string> ConvertVideoFileAsync(string inputFilePath, string outputFilePath);
    }

}