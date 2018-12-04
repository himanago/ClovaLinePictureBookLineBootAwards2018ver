
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Line.Messaging;
using Line.Messaging.Webhooks;
using ClovaLinePictureBookLineBootAwards2018ver.Services;
using System.Collections.Generic;
using ClovaLinePictureBookLineBootAwards2018ver.Models.Json;
using Line.Pay;
using Line.Pay.Models;
using System.Net.Http;
using System.Linq;

namespace ClovaLinePictureBookLineBootAwards2018ver
{
    public static class LineReplyFunction
    {
        static LineMessagingClient lineMessagingClient;
        static LineReplyFunction()
        {
            lineMessagingClient = new LineMessagingClient(Consts.LineMessagingApiAccessToken);
        }

        [FunctionName("LineReplyFunction")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, ILogger log)
        {
            try
            {
                var events = await req.GetWebhookEventsAsync(Consts.LineMessagingApiChannelSecret);
                var ev = events.FirstOrDefault();
                switch (ev?.Type)
                {
                    case WebhookEventType.Postback:
                        await HandleWebhookEventAsync(ev);
                        break;
                    case WebhookEventType.Message:
                    default:
                        // なにもしない
                        break;
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                log.LogError(ex.StackTrace);
            }

            return (ActionResult)new OkObjectResult("返信OK");
        }

        private static async Task HandleWebhookEventAsync(WebhookEvent ev)
        {
            string data = (ev as PostbackEvent)?.Postback?.Data;
            if (!string.IsNullOrEmpty(data))
            {
                var postbackEvent = ev as PostbackEvent;
                var userId = postbackEvent.Source.UserId;

                switch (postbackEvent?.Postback?.Data)
                {
                    case "action=list":
                        // 一覧表示
                        await lineMessagingClient.ReplyMessageAsync(postbackEvent.ReplyToken,
                            await LineMessageBuilder.BuildAvailableListMessageAsync(userId));
                        break;

                    case "action=playpause":
                        // 一時停止＆再開
                        var cachePause = await PlayStatusService.FindCacheAsync(userId);
                        cachePause.StopRequest.IsPaused = !cachePause.StopRequest.IsPaused;
                        await PlayStatusService.SaveAsync(cachePause);
                        break;

                    case "action=stop":
                        // 停止要求
                        var cache = await PlayStatusService.FindCacheAsync(userId);
                        cache.StopRequest.IsStopped = true;
                        await PlayStatusService.SaveAsync(cache);
                        break;

                    case "action=buy":
                        // 購入対象一覧
                        await lineMessagingClient.ReplyMessageAsync(postbackEvent.ReplyToken,
                            LineMessageBuilder.BuildPremiumListMessage(await BookService.FindAllUnavailableBooksAsync(userId)));
                        break;

                    case "action=narratorChange":
                        // ナレーター変更リクエスト：変更用メッセージを返す
                        await lineMessagingClient.ReplyMessageAsync(postbackEvent.ReplyToken,
                            LineMessageBuilder.BuildNarratorListMessage(await CustomNarratorService.GetNarratorsAsync(), userId));
                        break;

                    case "action=textModeChange":
                        // テキストモード変更リクエスト
                        await lineMessagingClient.ReplyMessageAsync(postbackEvent.ReplyToken,
                            new List<ISendMessage>
                            {
                                new TextMessage("読み上げ時にトーク画面で受信するテキストについて設定します。", new QuickReply()
                                {
                                    Items =
                                    {
                                        new QuickReplyButtonObject(new PostbackTemplateAction(TextMode.None.DisplayName(), "textMode=0")),
                                        new QuickReplyButtonObject(new PostbackTemplateAction(TextMode.Kana.DisplayName(), "textMode=1")),
                                        new QuickReplyButtonObject(new PostbackTemplateAction(TextMode.Kanji.DisplayName(), "textMode=2"))
                                    }
                                })
                            });
                        break;

                    case string s when s.StartsWith("buyBook="):
                        // 購入処理
                        await BuyNewBookAsync(postbackEvent, s.Replace("buyBook=", string.Empty));
                        break;

                    case string s when s.StartsWith("changeNarrator="):
                        // ナレーター変更
                        await CustomNarratorService.UpdateCustomNarratorAsync(postbackEvent.Source.UserId, s.Replace("changeNarrator=", string.Empty));
                        await lineMessagingClient.ReplyMessageAsync(postbackEvent.ReplyToken, new[] { "ナレーターを変更しました！" });
                        break;

                    case string s when s.StartsWith("textMode="):
                        // テキストモード変更
                        if (Int32.TryParse(s.Replace("textMode=", string.Empty), out var num))
                        {
                            var newMode = (TextMode)num;
                            var textModeCache = await TextModeService.FindCacheAsync(userId);

                            if (textModeCache == null)
                            {
                                await TextModeService.AddCacheAsync(userId, newMode);
                            }
                            else
                            {
                                textModeCache.TextMode.TextMode = newMode;
                                await TextModeService.SaveAsync(textModeCache);
                            }
                            await lineMessagingClient.ReplyMessageAsync(postbackEvent.ReplyToken, new List<ISendMessage>
                            {
                                new TextMessage($"テキストモードを{newMode.DisplayName()}に変更しました！")
                            });
                        }
                        else
                        {
                            await lineMessagingClient.ReplyMessageAsync(postbackEvent.ReplyToken, new List<ISendMessage>
                            {
                                new TextMessage("テキストモード変更に失敗しました。")
                            });
                        }
                        break;

                    default:
                        // 評価更新
                        await UpdateRate(postbackEvent);
                        break;

                }
            }
        }

        private static async Task BuyNewBookAsync(PostbackEvent postbackEvent, string bookKey)
        {
            var linePayClient = new LinePayClient(
                Consts.LinePayChannelId,
                Consts.LinePayChannelSecretKey,
                Consts.LinePayIsSandbox
            );

            // 書籍データ
            var book = await BookService.FindBookByTitleOrKeyAsync(null, bookKey);
            var userId = postbackEvent.Source.UserId;

            var reserve = new Reserve()
            {
                ProductName = book.Title,
                Amount = book.Price,
                Currency = Currency.JPY,
                OrderId = Guid.NewGuid().ToString(),
                ConfirmUrl = Consts.LinePayConfirmFunctionBaseUrl + $"&userId={userId}",
                ConfirmUrlType = ConfirmUrlType.SERVER
            };

            var response = await linePayClient.ReserveAsync(reserve);

            reserve.Mid = userId;

            // 決済情報をキャッシュ
            await LinePayCacheService.AddCacheAsync(response.Info.TransactionId, postbackEvent.Source.UserId, book.Title, book.Price);

            await lineMessagingClient.ReplyMessageAsync(postbackEvent.ReplyToken, new List<ISendMessage>{
                new TemplateMessage(
                    $"絵本『{book.Title}』を購入します。",
                    new ButtonsTemplate(
                        text: $"絵本『{book.Title}』を購入します。よろしければ、下記のボタンで決済に進んでください",
                        actions: new List<ITemplateAction>
                        {
                            new UriTemplateAction("LINE Pay で決済", response.Info.PaymentUrl.Web)
                        }
                    )
                )
            });
        }

        private static async Task UpdateRate(PostbackEvent postbackEvent)
        {
            var postbackObj = JsonConvert.DeserializeObject<Rating>(postbackEvent?.Postback?.Data);
            var userId = postbackEvent.Source.UserId;
            var rate = postbackObj.Rate;
            var bookKey = postbackObj.Key;
            var replyToken = postbackEvent.ReplyToken;

            // レートを更新
            await RateService.UpdateRateAndSendResultAsync(userId, rate, bookKey, replyToken, lineMessagingClient);
        }
    }
}
