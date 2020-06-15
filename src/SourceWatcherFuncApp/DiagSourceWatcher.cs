using System.Threading.Tasks;
using System.Threading;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using SourceWatcherFuncApp.Services;
using SourceWatcherFuncApp.Utilities;
using Diagnostics.ModelsAndUtils.Models.Storage;
using System.Net;
using System.Collections.Generic;

namespace Diag.SourceWatcher
{
    public class DiagSourceWatcher
    {
        private IGithubService githubService;

        private IStorageService storageService;

        private IConfigurationRoot config;

        public DiagSourceWatcher(IConfigurationRoot configurationRoot, IGithubService GithubService,
                                 IStorageService StorageService)
        {
            githubService = GithubService;
            config = configurationRoot;
            storageService = StorageService;
        }

        [FunctionName("DiagSourceWatcher")]
        public async Task Run([TimerTrigger("0 0 0 * * *")]TimerInfo timerInfo, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function started at: {DateTime.Now}");

            log.LogInformation($"Loading directories from github branch  {config["Github:Branch"]}");

            ServicePointManager.DefaultConnectionLimit = 150;
            var githubDirectories = await githubService.DownloadGithubDirectories(config["Github:Branch"]);
            bool updateEntities = false;
            if (bool.TryParse(config["UpdateEntities"], out bool result))
            {
                updateEntities = result;
            }
            if(updateEntities)
            {

                foreach (var githubdir in githubDirectories)
                {
                    if (!githubdir.Type.Equals("dir", StringComparison.OrdinalIgnoreCase)) continue;

                    try
                    {
                        var contentList = await githubService.DownloadGithubDirectories(branchdownloadUrl: githubdir.Url);
                        var assemblyFile = contentList.Where(githubFile => githubFile.Name.EndsWith("dll")).FirstOrDefault();
                        var scriptFile = contentList.Where(githubfile => githubfile.Name.EndsWith(".csx")).FirstOrDefault();
                        var configFile = contentList.Where(githubFile => githubFile.Name.Equals("package.json", StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();

                        if (assemblyFile != null && scriptFile != null && configFile != null)
                        {

                            log.LogInformation($"Getting content for Assembly file : {assemblyFile.Path}");
                            var assemblyData = await githubService.GetFileContentStream(assemblyFile.Download_url);

                            //log.LogInformation("Reading detector metadata");
                            var configFileData = await githubService.GetFileContentByType<DiagEntity>(configFile.Download_url);

                            configFileData = EntityHelper.PrepareEntityForLoad(assemblyData, string.Empty, configFileData);
                            configFileData.GitHubSha = githubdir.Sha;
                            configFileData.GithubLastModified = await githubService.GetCommitDate(scriptFile.Path);

                            //First check if entity exists in blob or table
                            var existingDetectorEntity = await storageService.GetEntityFromTable(configFileData.PartitionKey, configFileData.RowKey);
                            var doesBlobExists = await storageService.CheckDetectorExists(githubdir.Name);
                            //If there is no entry in table or blob or github last modifed date has been changed, upload to blob
                            if (existingDetectorEntity == null || !doesBlobExists || existingDetectorEntity.GithubLastModified != configFileData.GithubLastModified)
                            {
                                var assemblyLastModified = await githubService.GetCommitDate(assemblyFile.Path);
                                storageService.LoadBlobToContainer(assemblyFile.Path, assemblyData);
                            }
                            await storageService.LoadDataToTable(configFileData);
                        }
                        else
                        {
                            log.LogInformation($"One or more files were not found in {githubdir.Name}, skipped storage entry");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.LogError($"Exception occured while processing {githubdir.Name}: {ex.ToString()} ");
                    }
                }
            }

            var existingRows = await storageService.GetAllEntities();
            var currentGitHubDirs = githubDirectories.Select(dir => dir.Name).ToList();
            var deletedIds = existingRows.Where(row => !currentGitHubDirs.Contains(row.RowKey)).ToList();
            var updatetasks = new List<Task>();
            deletedIds.ForEach(deletedRow =>
            {
                deletedRow.IsDisabled = true;
                log.LogInformation($"Marking {deletedRow.RowKey} as disabled");
                updatetasks.Add(storageService.LoadDataToTable(deletedRow));
            });
            await Task.WhenAll(updatetasks);
            log.LogInformation($"C# Timer trigger function finished at: {DateTime.Now}");
        }
    }
}
