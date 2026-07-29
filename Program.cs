using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DAEnforcersSaveTool
{
    internal static class Program
    {
        private static readonly byte[] Key =
        {
            231,145, 28, 47,156,176,2,27,143,201,165,183,66,171,134,77,
            72,21,175,209,63,13,148,122,253,184,202,126,63,67,79,152
        };

        private static int Main(string[] args)
        {
            if (args.Length != 2 || (args[0] != "-dec" && args[0] != "-enc"))
            {
                PrintUsage();
                return 1;
            }

            string mode = args[0];
            string path = args[1];

            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"File not found: {path}");
                return 1;
            }

            try
            {
                if (mode == "-dec")
                {
                    string b64 = File.ReadAllText(path).Trim();
                    string decryptedWrapper = Decrypt(b64);

                    // Unwrap "data" payload to clean, formatted JSON for easy editing
                    string plainJson = UnwrapPayload(decryptedWrapper);

                    string outPath = GetDecryptedOutputPath(path);
                    File.WriteAllText(outPath, plainJson, new UTF8Encoding(false));
                    Console.WriteLine($"[Success] Decrypted -> {outPath}");
                }
                else
                {
                    string plainJson = File.ReadAllText(path, Encoding.UTF8).Trim('\uFEFF', ' ', '\r', '\n');

                    // Smart wrap: Prevents double-wrapping if the JSON already contains "hash" & "data"
                    string wrappedJson = WrapPayload(plainJson);

                    // Encrypt wrapper to Base64
                    string b64 = Encrypt(wrappedJson);

                    string outPath = GetEncryptedOutputPath(path);
                    File.WriteAllText(outPath, b64, new UTF8Encoding(false));
                    Console.WriteLine($"[Success] Encrypted -> {outPath}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                return 1;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("DAEnforcersSaveTool.exe (-dec/-enc) path-to-save-file");
            Console.WriteLine();
            Console.WriteLine("  -dec  Decrypt <file>.enc -> <file>.json (unwraps payload)");
            Console.WriteLine("  -enc  Encrypt <file>.json -> <file>.json.enc (computes SHA-256)");
        }

        private static string UnwrapPayload(string wrapperJson)
        {
            try
            {
                JToken token = JToken.Parse(wrapperJson);
                if (token is JObject obj && obj.ContainsKey("hash") && obj.ContainsKey("data"))
                {
                    Console.WriteLine("[Info] Detected outer wrapper envelope. Extracting inner payload...");
                    return obj["data"]!.ToString(Formatting.Indented);
                }
            }
            catch
            {
                // Fallback if raw JSON wasn't a wrapper
            }
            return wrapperJson;
        }

        private static string WrapPayload(string inputJson)
        {
            JToken inputToken = JToken.Parse(inputJson);
            JToken dataToken = inputToken;

            // Prevent double-wrapping if the user edited a file that still had the wrapper
            if (inputToken is JObject obj && obj.ContainsKey("hash") && obj.ContainsKey("data"))
            {
                Console.WriteLine("[Info] Input JSON already has wrapper envelope. Updating existing wrapper...");
                dataToken = obj["data"]!;
            }

            // 1. Canonicalize payload string (Formatting.None) exactly as DACOM.JsonDataWorker does
            string canonicalJson = dataToken.ToString(Formatting.None);

            // 2. Compute SHA-256 hash on canonicalized string
            string hash = ComputeSha256(canonicalJson);
            Console.WriteLine($"[Info] Calculated SHA-256: {hash}");

            // 3. Construct single-level wrapper
            JObject wrapper = new JObject
            {
                ["hash"] = hash,
                ["data"] = dataToken
            };

            // 4. Return minified wrapper string
            return wrapper.ToString(Formatting.None);
        }

        private static string ComputeSha256(string input)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = SHA256.HashData(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        private static string GetDecryptedOutputPath(string inputPath)
        {
            return inputPath.Substring(0, inputPath.Length - 4);
        }

        private static string GetEncryptedOutputPath(string inputPath)
        {
            return inputPath + ".enc";
        }

        private static string Encrypt(string plainText)
        {
            using Aes aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = Key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            byte[] iv = aes.IV;
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

            using ICryptoTransform encryptor = aes.CreateEncryptor();
            byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            byte[] result = new byte[iv.Length + cipherBytes.Length];
            Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, iv.Length, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

        private static string Decrypt(string base64)
        {
            byte[] data = Convert.FromBase64String(base64);

            using Aes aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = Key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            int ivLen = aes.BlockSize / 8; // 16 bytes
            if (data.Length < ivLen)
                throw new InvalidDataException("File too short to contain an IV.");

            byte[] iv = new byte[ivLen];
            byte[] cipherBytes = new byte[data.Length - ivLen];
            Buffer.BlockCopy(data, 0, iv, 0, ivLen);
            Buffer.BlockCopy(data, ivLen, cipherBytes, 0, cipherBytes.Length);

            aes.IV = iv;

            using ICryptoTransform decryptor = aes.CreateDecryptor();
            byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}