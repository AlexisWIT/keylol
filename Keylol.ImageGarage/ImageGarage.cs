﻿using System;
using System.Data.Entity.Infrastructure;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using ChannelAdam.ServiceModel;
using CsQuery;
using CsQuery.Output;
using Keylol.ImageGarage.ServiceReference;
using Keylol.Models.DAL;
using Keylol.Models.DTO;
using Keylol.ServiceBase;
using log4net;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Keylol.ImageGarage
{
    internal sealed class ImageGarage : KeylolService
    {
        private readonly ILog _logger;
        private readonly IModel _mqChannel;
        private readonly IServiceConsumer<IImageGarageCoordinator> _coordinator;

        public ImageGarage(ILogProvider logProvider, MqClientProvider mqClientProvider,
            IServiceConsumer<IImageGarageCoordinator> coordinator)
        {
            ServiceName = "Keylol.ImageGarage";

            _logger = logProvider.Logger;
            _mqChannel = mqClientProvider.CreateModel();
            _coordinator = coordinator;
            Config.HtmlEncoder = new HtmlEncoderMinimum();
        }

        protected override void OnStart(string[] args)
        {
            _mqChannel.QueueDeclare(MqClientProvider.ImageGarageRequestQueue, true, false, false, null);
            _mqChannel.BasicQos(0, 1, false);
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var test = await _coordinator.Operations.FindArticleAsync("1");
                    }
                    catch (FaultException ex)
                    {
                        _logger.Warn(ex.Message);
                    }
                    catch (Exception)
                    {
                        _logger.Warn("Unhandled exception");
                    }
                    Thread.Sleep(1000);
                }
            });
            return;
            var consumer = new EventingBasicConsumer(_mqChannel);
            consumer.Received += async (sender, eventArgs) =>
            {
                try
                {
                    using (var streamReader = new StreamReader(new MemoryStream(eventArgs.Body)))
                    {
                        var serializer = new JsonSerializer();
                        var requestDto =
                            serializer.Deserialize<ImageGarageRequestDto>(new JsonTextReader(streamReader));
                        var article = await _coordinator.Operations.FindArticleAsync(requestDto.ArticleId);
                        if (article == null)
                        {
                            _mqChannel.BasicNack(eventArgs.DeliveryTag, false, false);
                            using (NDC.Push("Consuming"))
                                _logger.Warn($"Article {requestDto.ArticleId} doesn't exist.");
                            return;
                        }
                        var dom = CQ.Create(article.Content);
                        article.ThumbnailImage = string.Empty;
                        var downloadCount = 0;
                        foreach (var img in dom["img"])
                        {
                            string url;
                            if (string.IsNullOrEmpty(img.Attributes["src"]))
                            {
                                url = img.Attributes["article-image-src"];
                            }
                            else
                            {
                                url = img.Attributes["src"];
                                try
                                {
                                    var request = WebRequest.CreateHttp(url);
                                    request.Referer = url;
                                    request.UserAgent =
                                        "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/48.0.2564.116 Safari/537.36";
                                    request.Accept = "image/webp,image/*,*/*;q=0.8";
                                    request.Headers["Accept-Language"] = "en-US,en;q=0.8,zh-CN;q=0.6,zh;q=0.4";
                                    using (var response = await request.GetResponseAsync())
                                    using (var ms =
                                        new MemoryStream(response.ContentLength > 0
                                            ? (int) response.ContentLength
                                            : 0))
                                    {
                                        do
                                        {
                                            var responseStream = response.GetResponseStream();
                                            if (responseStream == null) break;
                                            await responseStream.CopyToAsync(ms);
                                            var fileData = ms.ToArray();
                                            if (fileData.Length <= 0) break;
                                            var uri = new Uri(url);
                                            var extension = Path.GetExtension(uri.AbsolutePath);
                                            if (string.IsNullOrEmpty(extension)) break;
                                            var name = await UpyunProvider.UploadFile(fileData, extension);
                                            if (string.IsNullOrEmpty(name)) break;
                                            downloadCount++;
                                            url = $"keylol://{name}";
                                            img.Attributes["article-image-src"] = url;
                                            img.RemoveAttribute("src");
                                        } while (false);
                                    }
                                }
                                catch (WebException)
                                {
                                }
                                catch (UriFormatException)
                                {
                                }
                            }
                            if (string.IsNullOrEmpty(article.ThumbnailImage))
                                article.ThumbnailImage = url;
                        }
                        article.Content = dom.Render();
                        try
                        {
//                            await dbContext.SaveChangesAsync();
                            _mqChannel.BasicAck(eventArgs.DeliveryTag, false);
                            using (NDC.Push("Consuming"))
                                _logger.Info(
                                    $"Article {requestDto.ArticleId} ({article.Title}) finished, {downloadCount} images downloaded.");
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            _mqChannel.BasicNack(eventArgs.DeliveryTag, false, false);
                            using (NDC.Push("Consuming"))
                                _logger.Info($"Article {requestDto.ArticleId} update failed (concurrency conflict).");
                        }
                    }
                }
                catch (Exception e)
                {
                    _mqChannel.BasicNack(eventArgs.DeliveryTag, false, false);
                    using (NDC.Push("Consuming"))
                        _logger.Fatal("Unhandled callback exception.", e);
                }
            };
            _mqChannel.BasicConsume(MqClientProvider.ImageGarageRequestQueue, false, consumer);
        }

        protected override void OnStop()
        {
            _mqChannel.Close();
            base.OnStop();
        }
    }
}