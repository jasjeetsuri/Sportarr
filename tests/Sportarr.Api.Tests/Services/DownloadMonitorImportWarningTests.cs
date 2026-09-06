using System.Net;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Sportarr.Api.Data;
using Sportarr.Api.Models;
using Sportarr.Api.Services;
using Sportarr.Api.Services.Interfaces;

namespace Sportarr.Api.Tests.Services;

public class DownloadMonitorImportWarningTests
{
    [Theory]
    [InlineData("Completed", true, true)]
    [InlineData("Paused", true, true)]
    [InlineData("Downloading", true, true)]
    [InlineData("Queued", true, true)]
    [InlineData("Completed", false, true)]
    [InlineData("Completed", true, false)]
    public async Task ClientPoll_PreservesImportWarningAcrossPollsAndReload(
        string clientStatus, bool completedHandling, bool monitored)
    {
        var options = new DbContextOptionsBuilder<SportarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var services = new ServiceCollection().BuildServiceProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigService(new ConfigurationBuilder().Build(),
            NullLogger<ConfigService>.Instance);
        using var handler = new CompletedDownloadHandler(clientStatus);
        using var http = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(instance => instance.CreateClient(It.IsAny<string>())).Returns(http);
        var clientService = new DownloadClientService(factory.Object, NullLoggerFactory.Instance,
            NullLogger<DownloadClientService>.Instance, cache, config,
            Mock.Of<IRemotePathMappingService>());

        using (var db = new SportarrDbContext(options))
        {
            db.DownloadQueue.Add(new DownloadQueueItem
            {
                Title = "Formula.1.2026.Italy.Qualifying.1080p.WEB",
                DownloadId = "test-download",
                Status = DownloadStatus.ImportWarning,
                ErrorMessage = "Not an upgrade",
                ImportRetryCount = 0,
                Added = DateTime.UtcNow.AddHours(-1),
                OutputPath = "/downloads/original-path",
                Event = new Event { Title = "Italian Grand Prix", Sport = "Motorsport", Monitored = monitored },
                DownloadClient = new DownloadClient
                {
                    Name = "Fake SABnzbd", Type = DownloadClientType.Sabnzbd,
                    Host = "localhost", Port = 8080, Category = "sportarr"
                }
            });
            await db.SaveChangesAsync();
        }

        for (var poll = 0; poll < 3; poll++)
        {
            using var db = new SportarrDbContext(options);
            using var monitor = new EnhancedDownloadMonitorService(services,
                NullLogger<EnhancedDownloadMonitorService>.Instance);
            var download = await db.DownloadQueue.Include(item => item.DownloadClient)
                .Include(item => item.Event).SingleAsync();
            var process = typeof(EnhancedDownloadMonitorService).GetMethod("ProcessDownloadAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            await (Task)process.Invoke(monitor, new object?[]
            {
                download, clientService, null, db, completedHandling, false, false, 0, CancellationToken.None
            })!;
            await db.SaveChangesAsync();

            download.Status.Should().Be(DownloadStatus.ImportWarning);
            download.ErrorMessage.Should().Be("Not an upgrade");
            download.ImportRetryCount.Should().Be(0);
            download.ImportedAt.Should().BeNull();
            download.Progress.Should().Be(clientStatus == "Completed" ? 100 : 99);
            download.OutputPath.Should().Be(clientStatus == "Completed"
                ? "/downloads/test-download" : "/downloads/original-path");
            (await db.Blocklist.CountAsync()).Should().Be(0);
        }

        handler.RequestCount.Should().BeGreaterThanOrEqualTo(3);

        handler.Missing = true;
        using (var db = new SportarrDbContext(options))
        {
            using var monitor = new EnhancedDownloadMonitorService(services,
                NullLogger<EnhancedDownloadMonitorService>.Instance);
            var download = await db.DownloadQueue.Include(item => item.DownloadClient).SingleAsync();
            var process = typeof(EnhancedDownloadMonitorService).GetMethod("ProcessDownloadAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task)process.Invoke(monitor, new object?[]
            {
                download, clientService, null, db, completedHandling, false, false, 0, CancellationToken.None
            })!;

            download.MissingFromClientCount.Should().Be(1);
            download.Status.Should().Be(DownloadStatus.ImportWarning);
            (await db.DownloadQueue.CountAsync()).Should().Be(1);
        }
        handler.DeleteRequested.Should().BeFalse();
    }

    private sealed class CompletedDownloadHandler(string clientStatus) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public bool Missing { get; set; }
        public bool DeleteRequested { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
                        DeleteRequested |= request.RequestUri!.Query.Contains("delete");
            var json = request.RequestUri!.Query.Contains("mode=queue")
                ? """{"queue":{"slots":[]}}"""
                : Missing ? """{"history":{"slots":[]}}""" : JsonSerializer.Serialize(new
                {
                    history = new
                    {
                        slots = new[]
                        {
                            new
                            {
                                nzo_id = "test-download", name = "Formula.1.2026.Italy.Qualifying.1080p.WEB",
                                status = clientStatus, category = "sportarr", bytes = 1000,
                                storage = "/downloads/test-download"
                            }
                        }
                    }
                });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }
}