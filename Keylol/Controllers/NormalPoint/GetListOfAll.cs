﻿using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Keylol.Identity;
using Keylol.Models.DTO;

namespace Keylol.Controllers.NormalPoint
{
    public partial class NormalPointController
    {
        /// <summary>
        ///     获取所有据点列表
        /// </summary>
        /// <param name="skip">起始位置，默认 0</param>
        /// <param name="take">获取数量，最大 50，默认 20</param>
        [Authorize(Roles = KeylolRoles.Operator)]
        [Route("list")]
        [HttpGet]
        [ResponseType(typeof(List<NormalPointDto>))]
        public async Task<IHttpActionResult> GetListOfAll(int skip = 0, int take = 20)
        {
            if (take > 50) take = 50;
            var response = Request.CreateResponse(HttpStatusCode.OK,
                (await _dbContext.NormalPoints.OrderBy(p => p.CreateTime)
                    .Skip(() => skip).Take(() => take)
                    .Select(p => new
                    {
                        point = p,
                        articleCount = p.Articles.Count,
                        subscriberCount = p.Subscribers.Count
                    }).ToListAsync()).Select(entry => new NormalPointDto(entry.point, false, true)
                    {
                        ArticleCount = entry.articleCount,
                        SubscriberCount = entry.subscriberCount
                    }));
            response.Headers.Add("X-Total-Record-Count", (await _dbContext.NormalPoints.CountAsync()).ToString());
            return ResponseMessage(response);
        }
    }
}