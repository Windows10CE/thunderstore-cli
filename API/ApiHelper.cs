using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ThunderstoreCLI.API.Models;
using ThunderstoreCLI.Util;
using static Crayon.Output;

namespace ThunderstoreCLI.API
{
    public class ApiHelper
    {
        private Config.Config Config { get; }
        private RequestBuilder BaseRequestBuilder { get; }

        private AuthenticationHeaderValue AuthHeader
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Config.AuthConfig.DefaultToken))
                    throw new ArgumentException("An auth token is required for this command!");
                return new AuthenticationHeaderValue(Config.AuthConfig.Type.ToString(), Config.AuthConfig.DefaultToken);
            }
        }

        public ApiHelper(Config.Config config)
        {
            Config = config;
            BaseRequestBuilder = new RequestBuilder(config.PublishConfig.Repository);
        }

        public HttpRequestMessage SubmitPackage(string fileUuid)
        {
            return BaseRequestBuilder
                .StartNew()
                .WithEndpoint("api/experimental/submission/submit/")
                .WithMethod(HttpMethod.Post)
                .WithAuth(AuthHeader)
                .WithContent(new StringContent(SerializeUploadMeta(Config, fileUuid), Encoding.UTF8, "application/json"))
                .GetRequest();
        }

        public HttpRequestMessage StartUploadMedia(string filePath)
        {
            return BaseRequestBuilder
                .StartNew()
                .WithEndpoint("api/experimental/usermedia/initiate-upload/")
                .WithMethod(HttpMethod.Post)
                .WithAuth(AuthHeader)
                .WithContent(new StringContent(SerializeFileData(filePath), Encoding.UTF8, "application/json"))
                .GetRequest();
        }

        public HttpRequestMessage FinishUploadMedia(CompletedUpload finished, string uuid)
        {
            return BaseRequestBuilder
                .StartNew()
                .WithEndpoint($"api/experimental/usermedia/{uuid}/finish-upload/")
                .WithMethod(HttpMethod.Post)
                .WithAuth(AuthHeader)
                .WithContent(new StringContent(JsonSerializer.Serialize(finished), Encoding.UTF8, "application/json"))
                .GetRequest();
        }

        public async Task<string> FullUploadMediaAsync(string filePath, HttpClient client)
        {
            var start = await client.SendAsync(StartUploadMedia(filePath));
            if (start.StatusCode != HttpStatusCode.Created)
            {
                Console.WriteLine(Red("ERROR: Failed to start usermedia upload"));
                Console.WriteLine(Red("Details:"));
                Console.WriteLine($"Status code: {start.StatusCode:D} {start.StatusCode}");
                using var startReader = new StreamReader(await start.Content.ReadAsStreamAsync());
                Console.WriteLine(Dim(await startReader.ReadToEndAsync()));
                return null;
            }

            var uploadData = await JsonSerializer.DeserializeAsync<UploadInitiateData>(await start.Content.ReadAsStreamAsync());

            Console.WriteLine(Cyan($"Uploading {uploadData.Metadata.Filename} ({BytesToSize(uploadData.Metadata.Size)}) in {uploadData.UploadUrls.Length} chunks..."));
            Console.WriteLine();

            using var partClient = new HttpClient();

            // ReSharper disable once AccessToDisposedClosure
            var tasks = uploadData.UploadUrls.Select(x => UploadChunk(x, filePath, partClient)).ToArray();

            var allTracker = Task.WhenAll(tasks);
            
            ushort spinIndex = 0;
            string[] spinChars = { "|", "/", "-", "\\", "|", "/", "-", "\\" };
            while (true)
            {
                var completed = tasks.Count(static x => x.IsCompleted);
                    
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write(Green($"{completed}/{tasks.Length} chunks uploaded...{spinChars[spinIndex++ % spinChars.Length]}"));
                    
                if (allTracker.IsCompleted)
                {
                    Console.WriteLine();
                    break;
                }

                await Task.Delay(200);
            }

            var finished = await client.SendAsync(FinishUploadMedia(new CompletedUpload()
            {
                Parts = tasks.Select(static x => x.Result).ToArray()
            }, uploadData.Metadata.UUID));

            var usermedia = await JsonSerializer.DeserializeAsync<UploadInitiateData.UserMediaData>(await finished.Content.ReadAsStreamAsync());

            return usermedia.UUID;
        }

        public string FullUploadMedia(string filePath, HttpClient client = null) => FullUploadMediaAsync(filePath, client ?? new HttpClient()).Result;

        private async Task<CompletedPartData> UploadChunk(UploadInitiateData.UploadPartData part, string path, HttpClient client)
        {
            await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            stream.Seek(part.Offset, SeekOrigin.Begin);

            byte[] hash;
            using (var reader = new BinaryReader(stream, Encoding.Default, true))
            {
                using (var md5 = MD5.Create())
                {
                    md5.Initialize();
                    var length = part.Length;
                    while (length > md5.InputBlockSize)
                    {
                        length -= md5.InputBlockSize;
                        md5.TransformBlock(reader.ReadBytes(md5.InputBlockSize), 0, md5.InputBlockSize, null, 0);
                    }
                    md5.TransformFinalBlock(reader.ReadBytes(length), 0, length);
                    hash = md5.Hash;
                }
            }

            stream.Seek(part.Offset, SeekOrigin.Begin);

            var partRequest = new HttpRequestMessage(HttpMethod.Put, part.Url)
            {
                Content = new StreamContent(stream, part.Length)
            };

            partRequest.Content.Headers.ContentMD5 = hash;

            using var response = await client.SendAsync(partRequest);

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                Console.WriteLine(Red("Failed to upload file chunk"));
                Console.WriteLine(Red(await response.Content.ReadAsStringAsync()));
                throw;
            }

            return new CompletedPartData()
            {
                ETag = response.Headers.ETag.Tag,
                PartNumber = part.PartNumber
            };
        }

        private static string SerializeUploadMeta(Config.Config config, string fileUuid)
        {
            var meta = new PackageUploadMetadata()
            {
                AuthorName = config.PackageMeta.Namespace,
                Categories = config.PublishConfig.Categories,
                Communities = config.PublishConfig.Communities,
                HasNsfwContent = config.PackageMeta.ContainsNsfwContent == true,
                UploadUUID = fileUuid
            };
            return JsonSerializer.Serialize(meta);
        }

        private static string SerializeFileData(string filePath)
        {
            return JsonSerializer.Serialize(new FileData()
            {
                Filename = Path.GetFileName(filePath),
                Filesize = new FileInfo(filePath).Length
            });
        }

        private string BytesToSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            while (bytes >= 1024 && suffixIndex < suffixes.Length)
            {
                bytes /= 1024;
                suffixIndex++;
            }

            return bytes + suffixes[suffixIndex];
        }
    }
}
