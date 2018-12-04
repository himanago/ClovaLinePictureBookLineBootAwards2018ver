
using ClovaLinePictureBookLineBootAwards2018ver.Services;
using Line.Messaging;
using Line.Pay;
using Line.Pay.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClovaLinePictureBookLineBootAwards2018ver
{
    public static class LinePayConfirmFunction
    {
        [FunctionName("LinePayConfirmFunction")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log)
        {
            var lineMessagingClient = new LineMessagingClient(Consts.LineMessagingApiAccessToken);

            var linePayClient = new LinePayClient(
                Consts.LinePayChannelId,
                Consts.LinePayChannelSecretKey,
                Consts.LinePayIsSandbox
            );

            var transactionId = Int64.Parse(req.GetQueryParameterDictionary()
                .FirstOrDefault(q => string.Compare(q.Key, "transactionId", true) == 0)
                .Value);

            var cache = await LinePayCacheService.FindCacheAsync(transactionId);
            var linePayCache = cache.LinePay;

            // ���ϊm�F
            var response = await linePayClient.ConfirmAsync(transactionId, new Confirm()
            {
                Amount = linePayCache.Amount,
                Currency = Currency.JPY
            });

            // �w������DB�ɍX�V
            await BookService.SaveBuyerAsync(cache);

            await lineMessagingClient.PushMessageAsync(cache.UserId, new List<ISendMessage>(){
                new TextMessage($"���肪�Ƃ��������܂��A{cache.LinePay.Title}�̌��ς��������܂����B")
            });

            return (ActionResult)new OkObjectResult("���ϊ���");
        }
    }
}
