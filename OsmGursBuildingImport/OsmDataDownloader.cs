﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.BZip2;

namespace OsmGursBuildingImport
{
    class OsmDataDownloader
    {
        private readonly string cacheFolder;
        private readonly GursData gursData;
        private HttpClient httpClient = new HttpClient();


        public OsmDataDownloader(string originalsCacheFolder, GursData gursData)
        {
            this.cacheFolder = originalsCacheFolder;
            this.gursData = gursData;
        }

        ConcurrentDictionary<string, SemaphoreSlim> semaphors = new();

        public async Task<string> GetOriginalXmlFileAsync(ProcessingArea area)
        {
            var xmlOutputFile = Path.Combine(cacheFolder, area.Name + ".original.xml");
            var semaphor = semaphors.GetOrAdd(xmlOutputFile, (k) => new SemaphoreSlim(1, 1));
            await semaphor.WaitAsync();
            try
            {
                if (!FileUpToDate(xmlOutputFile))
                {
                    var unclipped = Path.Combine(cacheFolder, "unclipped-" + area.Name + ".original.pbf");
                    await Process.Start("osmx", new[] { "extract", "/home/davidkarlas/slo.osmx", unclipped, "--region", area.pathToPoly }).WaitForExitAsync();
                    await Process.Start("osmconvert", new[] { unclipped, "--complete-ways", "--complete-multipolygons", "-B=" + area.pathToPoly, "-o=" + xmlOutputFile }).WaitForExitAsync();
                    File.Delete(unclipped);
                }
                return xmlOutputFile;
            }
            finally
            {
                semaphor.Release();
            }
        }

        private static bool FileUpToDate(string file)
        {
            return File.Exists(file) &&
                    File.GetLastWriteTimeUtc(file).AddMinutes(3) > DateTime.UtcNow;
        }
    }
}

