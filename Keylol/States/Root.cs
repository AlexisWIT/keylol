﻿using System;
using System.Threading.Tasks;
using Keylol.Identity;
using Keylol.Models.DAL;
using Keylol.Provider;
using Keylol.Provider.CachedDataProvider;
using Keylol.StateTreeManager;
using Microsoft.AspNet.Identity;

namespace Keylol.States
{
    /// <summary>
    /// State Tree Root
    /// </summary>
    public class Root
    {
        /// <summary>
        /// 定位器名称
        /// </summary>
        public static string LocatorName() => "state";

        /// <summary>
        /// 获取新的完整状态树
        /// </summary>
        /// <param name="state">当前 UI 状态</param>
        /// <param name="userManager"><see cref="KeylolUserManager"/></param>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="coupon"><see cref="CouponProvider"/></param>
        /// <param name="cachedData"><see cref="CachedDataProvider"/></param>
        /// <returns>完整状态树</returns>
        public static async Task<Root> Locate(string state, [Injected] KeylolUserManager userManager,
            [Injected] KeylolDbContext dbContext, [Injected] CouponProvider coupon,
            [Injected] CachedDataProvider cachedData)
        {
            var root = new Root();
            var currentUserId = StateTreeHelper.CurrentUser().Identity.GetUserId();
            if (await StateTreeHelper.CanAccessAsync<Root>(nameof(CurrentUser)))
            {
                var user = await userManager.FindByIdAsync(currentUserId);
                root.CurrentUser = await CurrentUser.CreateAsync(user, userManager, dbContext, coupon);
            }

            switch (state)
            {
                case "entrance.discovery":
                    root.DiscoveryPage =
                        await States.DiscoveryPage.DiscoveryPage.CreateAsync(currentUserId, dbContext, cachedData);
                    break;

                case "entrance.points":
                    root.PointsPage =
                        await States.PointsPage.PointsPage.CreateAsync(currentUserId, dbContext, cachedData);
                    break;

                default:
                    throw new NotSupportedException("Not supported state.");
            }
            return root;
        }

        /// <summary>
        /// 当前登录的用户
        /// </summary>
        [Authorize]
        public CurrentUser CurrentUser { get; set; }

        /// <summary>
        /// 入口 - 发现
        /// </summary>
        public DiscoveryPage.DiscoveryPage DiscoveryPage { get; set; }

        /// <summary>
        /// 入口 - 据点
        /// </summary>
        public PointsPage.PointsPage PointsPage { get; set; }
    }
}