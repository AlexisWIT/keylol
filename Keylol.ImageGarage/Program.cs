﻿using ChannelAdam.ServiceModel;
using Keylol.ImageGarage.ServiceReference;
using Keylol.ServiceBase;
using SimpleInjector;

namespace Keylol.ImageGarage
{
    public static class Program
    {
        public static readonly Container Container = new Container();

        public static void Main(string[] args)
        {
            // 服务特定依赖注册点
            Container.RegisterSingleton(
                () => ServiceConsumerFactory.Create<IImageGarageCoordinator>(() => new ImageGarageCoordinatorClient
                {
                    ClientCredentials =
                    {
                        UserName =
                        {
                            UserName = "keylol-service-consumer",
                            Password = "neLFDyJB8Vj2Xtsn2KMTUEFw"
                        }
                    }
                }));
            KeylolService.Run<ImageGarage>(args, Container);
        }
    }
}