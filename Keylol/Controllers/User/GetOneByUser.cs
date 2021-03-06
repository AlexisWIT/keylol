﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using System.Web.Security;
using Keylol.Identity;
using Keylol.Models;
using Keylol.Models.DTO;
using Keylol.Utilities;
using Microsoft.AspNet.Identity;
using SteamKit2;
using Swashbuckle.Swagger.Annotations;

namespace Keylol.Controllers.User
{
    public partial class UserController
    {
        /// <summary>
        ///     根据 Id、UserName 或者 IdCode 取得一名用户
        /// </summary>
        /// <param name="id">用户 ID</param>
        /// <param name="profilePointBackgroundImage">是否包含用户据点背景图片，默认 false</param>
        /// <param name="security">是否包含用户安全信息（Email、登录保护等），用户只能获取自己的安全信息（除非是运维职员），默认 false</param>
        /// <param name="steam">是否包含用户 Steam 信息，用户只能获取自己的 Steam 信息（除非是运维职员），默认 false</param>
        /// <param name="steamBot">是否包含用户所属 Steam 机器人（用户只能获取自己的机器人（除非是运维职员），默认 false</param>
        /// <param name="subscribeCount">是否包含用户订阅数量（用户只能获取自己的订阅信息（除非是运维职员），默认 false</param>
        /// <param name="stats">是否包含用户读者数和文章数，默认 false</param>
        /// <param name="subscribed">是否包含该用户有没有被当前用户订阅的信息，默认 false</param>
        /// <param name="moreOptions">是否包含更多杂项设置（例如通知偏好设置），默认 false</param>
        /// <param name="commentLike">是否包含用户有无新的评论和认可，用户只能获取自己的信息（除非是运维职员），默认 false</param>
        /// <param name="coupon">是否包含用户的文券数量，用户只能获取自己的信息（除非是运维职员），默认 false</param>
        /// <param name="reviewStats">是否包含用户评测文章数和简评数，默认 false</param>
        /// <param name="idType">ID 类型，默认 "Id"</param>
        [Route("{id}")]
        [AllowAnonymous]
        [HttpGet]
        [ResponseType(typeof(UserDto))]
        [SwaggerResponse(HttpStatusCode.NotFound, "指定用户不存在")]
        [SwaggerResponse(HttpStatusCode.Unauthorized, "尝试获取无权获取的属性")]
        public async Task<IHttpActionResult> GetOneByUser(string id,
            bool profilePointBackgroundImage = false,
            bool security = false,
            bool steam = false,
            bool steamBot = false,
            bool subscribeCount = false,
            bool stats = false,
            bool subscribed = false,
            bool moreOptions = false,
            bool commentLike = false,
            bool coupon = false,
            bool reviewStats = false,
            UserIdentityType idType = UserIdentityType.Id)
        {
            KeylolUser user;
            var visitorId = User.Identity.GetUserId();

            switch (idType)
            {
                case UserIdentityType.UserName:
                    user = await _userManager.FindByNameAsync(id);
                    break;

                case UserIdentityType.IdCode:
                    user = await _userManager.FindByIdCodeAsync(id);
                    break;

                case UserIdentityType.Id:
                    if (id == "current" && string.IsNullOrWhiteSpace(visitorId))
                        return Unauthorized();
                    user = await _userManager.FindByIdAsync(id == "current" ? visitorId : id);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(idType), idType, null);
            }

            if (user == null)
                return NotFound();

            if (user.Id == visitorId)
            {
                // 每日访问奖励
                if (DateTime.Now.Date > user.LastDailyRewardTime.Date)
                {
                    user.LastDailyRewardTime = DateTime.Now;
                    user.FreeLike = 5; // 免费认可重置
                    try
                    {
                        await _dbContext.SaveChangesAsync();
                        await _coupon.Update(user, CouponEvent.每日访问);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                    }
                }
            }

            var getSelf = visitorId == user.Id || User.IsInRole(KeylolRoles.Operator);

            var userDto = new UserDto(user);

            if (moreOptions)
            {
                userDto.SteamNotifyOnArticleReplied = user.SteamNotifyOnArticleReplied;
                userDto.SteamNotifyOnCommentReplied = user.SteamNotifyOnCommentReplied;
                userDto.SteamNotifyOnArticleLiked = user.SteamNotifyOnArticleLiked;
                userDto.SteamNotifyOnCommentLiked = user.SteamNotifyOnCommentLiked;
                userDto.AutoSubscribeEnabled = user.AutoSubscribeEnabled;
                userDto.AutoSubscribeDaySpan = user.AutoSubscribeDaySpan;
            }

            if (profilePointBackgroundImage)
                userDto.ProfilePointBackgroundImage = user.ProfilePoint.BackgroundImage;

            userDto.Roles = await _userManager.GetRolesAsync(user.Id);

            if (security)
            {
                if (!getSelf)
                    return Unauthorized();
                userDto.LockoutEnabled = user.LockoutEnabled;
                userDto.Email = user.Email;
            }

            if (steam)
            {
                if (!getSelf)
                    return Unauthorized();
                userDto.SteamId = await _userManager.GetSteamIdAsync(user.Id);
                userDto.SteamProfileName = user.SteamProfileName;
            }

            if (steamBot)
            {
                if (!getSelf)
                    return Unauthorized();
                if (user.SteamBotId != null)
                    userDto.SteamBot = new SteamBotDto(user.SteamBot)
                    {
                        Online = user.SteamBot.IsOnline()
                    };
            }

            if (coupon)
            {
                if (!getSelf)
                    return Unauthorized();
                userDto.Coupon = user.Coupon;
            }

            if (subscribeCount)
            {
                userDto.SubscribedPointCount =
                    await _dbContext.Users.Where(u => u.Id == user.Id).SelectMany(u => u.SubscribedPoints).CountAsync();
            }

            if (stats)
            {
                var statsResult = await _dbContext.Users.Where(u => u.Id == user.Id)
                    .Select(u =>
                        new
                        {
                            subscriberCount = u.ProfilePoint.Subscribers.Count,
                            articleCount = u.ProfilePoint.Articles.Count(a => a.Archived == ArchivedState.None)
                        })
                    .SingleOrDefaultAsync();
                userDto.SubscriberCount = statsResult.subscriberCount;
                userDto.ArticleCount = statsResult.articleCount;
            }

            if (reviewStats)
            {
                var reviewStatsResult = await _dbContext.Users.Where(u => u.Id == user.Id)
                    .Select(u => new
                    {
                        reviewCount = u.ProfilePoint.Articles.Count(a => a.Type == ArticleType.评),
                        shortReviewCount =
                            u.ProfilePoint.Articles.Count(a => a.Type == ArticleType.简评)
                    })
                    .SingleOrDefaultAsync();
                userDto.ReviewCount = reviewStatsResult.reviewCount;
                userDto.ShortReviewCount = reviewStatsResult.shortReviewCount;
            }

            if (subscribed)
            {
                userDto.Subscribed = await _dbContext.Users.Where(u => u.Id == visitorId)
                    .SelectMany(u => u.SubscribedPoints)
                    .Select(p => p.Id)
                    .ContainsAsync(user.Id);
            }

            if (commentLike)
            {
                if (!getSelf)
                    return Unauthorized();
                userDto.MessageCount = string.Join(",", new[]
                {
                    await _dbContext.Messages.Where(m => m.ReceiverId == user.Id && m.Unread &&
                                                         m.Type >= 0 && (int) m.Type <= 99)
                        .CountAsync(),
                    await _dbContext.Messages.Where(m => m.ReceiverId == user.Id && m.Unread &&
                                                         (int) m.Type >= 100 && (int) m.Type <= 199)
                        .CountAsync(),
                    await _dbContext.Messages.Where(m => m.ReceiverId == user.Id && m.Unread &&
                                                         (int) m.Type >= 200 && (int) m.Type <= 299)
                        .CountAsync()
                });
            }

            return Ok(userDto);
        }
    }
}