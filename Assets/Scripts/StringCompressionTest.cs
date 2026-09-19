using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Core.Utility;

/// <summary>
/// Test component for StringCompression utility.
/// Attach to a GameObject and enter Play Mode to run tests.
/// </summary>
public class StringCompressionTest : MonoBehaviour
    {
        [SerializeField]
        private string testUrl = "https://ttmuniverse.b-cdn.net/moon/init-data.br";

        [SerializeField, TextArea(5, 20)]
        private string testJson = @"{""player"":{""name"":""TestUser"",""level"":42,""inventory"":[{""id"":1,""name"":""Sword""},{""id"":2,""name"":""Shield""}]}}";

        private void Start()
        {
            RunCompressionTest();
            StartCoroutine(FetchAndDecodeBrotli());
        }

        [ContextMenu("Run Compression Test")]
        private void RunCompressionTest()
        {
            Debug.Log("=== String Compression Test ===");

            // Test Base64 string compression
            string compressed = StringCompression.Compress(testJson);
            string decompressed = StringCompression.Decompress(compressed);

            int originalSize = System.Text.Encoding.UTF8.GetByteCount(testJson);
            int compressedSize = System.Text.Encoding.UTF8.GetByteCount(compressed);

            Debug.Log($"Original ({originalSize} bytes):\n{testJson}");
            Debug.Log($"Compressed ({compressedSize} bytes):\n{compressed}");
            Debug.Log($"Decompressed:\n{decompressed}");
            Debug.Log($"Match: {testJson == decompressed}");
            Debug.Log($"Compression ratio: {(float)compressedSize / originalSize:P1}");

            // Test raw byte compression
            byte[] compressedBytes = StringCompression.CompressToBytes(testJson);
            string decompressedFromBytes = StringCompression.DecompressFromBytes(compressedBytes);

            Debug.Log($"Raw compressed size: {compressedBytes.Length} bytes");
            Debug.Log($"Byte compression match: {testJson == decompressedFromBytes}");
        }

        [ContextMenu("Fetch and Decode from URL")]
        private void FetchFromUrl()
        {
            StartCoroutine(FetchAndDecodeBrotli());
        }

        private IEnumerator FetchAndDecodeBrotli()
        {
            Debug.Log($"=== Fetching Brotli from URL ===");
            Debug.Log($"URL: {testUrl}");

            using var request = UnityWebRequest.Get(testUrl);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to fetch: {request.error}");
                yield break;
            }

            byte[] compressedBytes = request.downloadHandler.data;
            Debug.Log($"Downloaded {compressedBytes.Length} bytes");

            string json = StringCompression.DecompressFromBytes(compressedBytes);

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("Decompression returned null or empty");
                yield break;
            }

            Debug.Log($"Decompressed JSON ({json.Length} chars):");
            Debug.Log(json);

            // Save to local Assets/json folder with same name as source
            string sourceFileName = Path.GetFileNameWithoutExtension(testUrl) + ".json";
            string outputDir = Path.Combine(Application.dataPath, "json");
            Directory.CreateDirectory(outputDir);
            string outputPath = Path.Combine(outputDir, sourceFileName);
            File.WriteAllText(outputPath, json);
            Debug.Log($"Saved JSON to: {outputPath}");
        }
    }
