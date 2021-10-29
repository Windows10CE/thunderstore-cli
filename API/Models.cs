using System;
using System.Text.Json.Serialization;

namespace ThunderstoreCLI.API.Models
{
    public class FileData
        {
            [JsonPropertyName("filename")]
            public string Filename { get; set; }
            
            [JsonPropertyName("file_size_bytes")]
            public long Filesize { get; set; }
        }

        public class CompletedUpload
        {
            [JsonPropertyName("parts")]
            public CompletedPartData[] Parts { get; set; }
        }
        public class CompletedPartData
        {
            [JsonPropertyName("ETag")]
            public string ETag { get; set; }
            
            [JsonPropertyName("PartNumber")]
            public int PartNumber { get; set; }
        }
        
        public class UploadInitiateData
        {
            public class UserMediaData
            {
                [JsonPropertyName("uuid")]
                public string UUID { get; set; }
                
                [JsonPropertyName("filename")]
                public string Filename { get; set; }
                
                [JsonPropertyName("size")]
                public long Size { get; set; }
                
                [JsonPropertyName("datetime_created")]
                public DateTime TimeCreated { get; set; }
                
                [JsonPropertyName("expiry")]
                public DateTime? ExpireTime { get; set; }
                
                [JsonPropertyName("status")]
                public string Status { get; set; }
            }
            public class UploadPartData
            {
                [JsonPropertyName("part_number")]
                public int PartNumber { get; set; }
                
                [JsonPropertyName("url")]
                public string Url { get; set; }
                
                [JsonPropertyName("offset")]
                public long Offset { get; set; }
                
                [JsonPropertyName("length")]
                public int Length { get; set; }
            }
            
            [JsonPropertyName("user_media")]
            public UserMediaData Metadata { get; set; }
            
            [JsonPropertyName("upload_urls")]
            public UploadPartData[] UploadUrls { get; set; }
        }

        public class PackageUploadMetadata
        {
            [JsonPropertyName("author_name")]
            public string AuthorName { get; set; }

            [JsonPropertyName("categories")]
            public string[] Categories { get; set; }

            [JsonPropertyName("communities")]
            public string[] Communities { get; set; }

            [JsonPropertyName("has_nsfw_content")]
            public bool HasNsfwContent { get; set; }
            
            [JsonPropertyName("upload_uuid")]
            public string UploadUUID { get; set; }
        }
}
