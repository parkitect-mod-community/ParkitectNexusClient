// ParkitectNexusClient
// Copyright 2015 Parkitect, Tim Potze

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using Octokit;

namespace ParkitectNexus.Data
{
    /// <summary>
    ///     Represents the online parkitect asset storage.
    /// </summary>
    public class ParkitectOnlineAssetRepository
    {
        private readonly ParkitectNexusWebsite _parkitectNexusWebsite;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParkitectOnlineAssetRepository"/> class.
        /// </summary>
        /// <param name="parkitectNexusWebsite">The parkitect nexus website.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public ParkitectOnlineAssetRepository(ParkitectNexusWebsite parkitectNexusWebsite)
        {
            if (parkitectNexusWebsite == null) throw new ArgumentNullException(nameof(parkitectNexusWebsite));
            _parkitectNexusWebsite = parkitectNexusWebsite;
        }
        
        /// <summary>
        ///     Determines whether the specified input is valid file hash.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="assetType">Type of the asset.</param>
        /// <returns>
        ///     true if valid; false otherwise.
        /// </returns>
        public static bool IsValidFileHash(string input, ParkitectAssetType assetType)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            switch (assetType)
            {
                case ParkitectAssetType.Blueprint:
                case ParkitectAssetType.Savegame:
                    return input.Length == 10 && input.All(c => (c >= 'a' && c <= 'f') || (c >= '0' && c <= '9'));
                case ParkitectAssetType.Mod:
                    var p = input.Split('/');
                    return p.Length == 2 && !String.IsNullOrWhiteSpace(p[0]) && !String.IsNullOrWhiteSpace(p[1]);
                default:
                    return false;
            }
        }

        /// <summary>
        ///     Resolves the download URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns>Information about the download.</returns>
        public async Task<DownloadInfo> ResolveDownloadUrl(ParkitectNexusUrl url)
        {
            if (url == null) throw new ArgumentNullException(nameof(url));

            switch (url.AssetType)
            {
                case ParkitectAssetType.Blueprint:
                case ParkitectAssetType.Savegame:
                    return new DownloadInfo(_parkitectNexusWebsite.ResolveUrl($"download/{url.FileHash}"), null, null);
                case ParkitectAssetType.Mod:
                    var tag = await GetLatestModTag(url.FileHash);
                    if (tag == null)
                        throw new Exception("mod has not yet been released(tagged)");
                    return new DownloadInfo(tag.ZipballUrl, url.FileHash, tag.Name);
                default:
                    throw new Exception("unsupported mod type");
            }
        }

        /// <summary>
        ///     Downloads the file associated with the specified url.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns>An instance which performs the requested task.</returns>
        public async Task<ParkitectAsset> DownloadFile(ParkitectNexusUrl url)
        {
            if (url == null) throw new ArgumentNullException(nameof(url));

            // Create a download url based on the file hash.
            var downloadInfo = await ResolveDownloadUrl(url);

            // Create a web client which will download the file.
            using (var webClient = new ParkitectNexusWebClient())
            {
                // Receive the content of the file.
                using (var stream = await webClient.OpenReadTaskAsync(downloadInfo.Url))
                {
                    // Read content information from the headers.
                    var contentDispositionHeader = webClient.ResponseHeaders.Get("Content-Disposition");
                    var contentLengthHeader = webClient.ResponseHeaders.Get("Content-Length");
                    var contentTypeHeader = webClient.ResponseHeaders.Get("Content-Type");

                    // Ensure the required content headers exist.
                    if (String.IsNullOrWhiteSpace(contentDispositionHeader) ||
                        String.IsNullOrWhiteSpace(contentTypeHeader))
                        throw new Exception("invalid headers");

                    // Parse the content length header to an integer.
                    var contentLength = 0;
                    if (contentLengthHeader != null && !Int32.TryParse(contentLengthHeader, out contentLength))
                        throw new Exception("invalid headers");

                    // Get asset information for the asset type specified in the url.
                    var assetInfo = url.AssetType.GetCustomAttribute<ParkitectAssetInfoAttribute>();

                    // Ensure the type of the received content matches the expected content type.
                    if (assetInfo == null || assetInfo.ContentType != contentTypeHeader.Split(';').FirstOrDefault())
                        throw new Exception("invalid response type");

                    // Extract the filename of the asset from the content disposition header.
                    var fileNameMatch = Regex.Match(contentDispositionHeader, @"attachment; filename=(""?)(.*)\1");

                    if (fileNameMatch == null || !fileNameMatch.Success)
                        throw new Exception("invalid headers");

                    var fileName = fileNameMatch.Groups[2].Value;

                    // Copy the contents of the downloaded stream to a memory stream.
                    var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);

                    // Verify we received all content.
                    if (contentLengthHeader != null && memoryStream.Length != contentLength)
                        throw new Exception("unexpected end of stream");

                    // Create an instance of ParkitectAsset with the received content and data.
                    return new ParkitectAsset(fileName, downloadInfo, url.AssetType, memoryStream);
                }
            }
        }

        private async Task<RepositoryTag> GetLatestModTag(string mod)
        {
            if (mod == null) throw new ArgumentNullException(nameof(mod));
            var p = mod.Split('/');

            if (p.Length != 2 || String.IsNullOrWhiteSpace(p[0]) || String.IsNullOrWhiteSpace(p[1]))
                throw new ArgumentException(nameof(mod));

            var client = new GitHubClient(new ProductHeaderValue("parkitect-nexus-client"));
            var release = (await client.Release.GetAll(p[0], p[1])).FirstOrDefault(r => !r.Prerelease);

            return release == null
                ? null
                : (await client.Repository.GetAllTags(p[0], p[1])).FirstOrDefault(t => t.Name == release.TagName);
        }

    }
    /// <summary>
    ///     Represents the ParkitectNexus website.
    /// </summary>
    public class ParkitectNexusWebsite
    {
#if DEBUG
        private const string WebsiteUrl = "https://{0}staging.parkitectnexus.com/{1}";
#else
        private const string WebsiteUrl = "https://{0}parkitectnexus.com/{1}";
#endif

        /// <summary>
        ///     Launches the nexus.
        /// </summary>
        public void Launch()
        {
            Process.Start(ResolveUrl(null));
        }

        /// <summary>
        ///     Installs the parkitectnexus:// protocol.
        /// </summary>
        public void InstallProtocol()
        {
            try
            {
                var appPath = Assembly.GetEntryAssembly().Location;

                var parkitectNexus = Registry.CurrentUser?.CreateSubKey(@"Software\Classes\parkitectnexus");
                parkitectNexus?.SetValue("", "ParkitectNexus Client");
                parkitectNexus?.SetValue("URL Protocol", "");
                parkitectNexus?.CreateSubKey(@"DefaultIcon")?.SetValue("", $"{appPath},0");
                parkitectNexus?.CreateSubKey(@"shell\open\command")?.SetValue("", $"\"{appPath}\" --download \"%1\"");
            }
            catch (Exception)
            {
                // todo: Log the error or something. The app is useless without the url protocol.
            }
        }

        /// <summary>
        ///     Resolves the URL to the specified path and subdomain.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="subdomain">The sub domain.</param>
        /// <returns>The URL.</returns>
        public string ResolveUrl(string path, string subdomain)
        {
            return string.Format(WebsiteUrl, string.IsNullOrEmpty(subdomain) ? string.Empty : subdomain + ".", path);
        }

        /// <summary>
        ///     Resolves the URL to the specified path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The URL.</returns>
        public string ResolveUrl(string path)
        {
            return ResolveUrl(path, null);
        }
    }
}