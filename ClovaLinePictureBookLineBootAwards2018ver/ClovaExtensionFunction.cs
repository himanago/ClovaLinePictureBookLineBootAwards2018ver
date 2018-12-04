
using CEK.CSharp;
using CEK.CSharp.Models;
using ClovaLinePictureBookLineBootAwards2018ver.Models.Json;
using ClovaLinePictureBookLineBootAwards2018ver.Services;
using Line.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClovaLinePictureBookLineBootAwards2018ver
{
    public static class ClovaExtensionFunction
    {
        [FunctionName("ClovaExtensionFunction")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log)
        {
            var response = new CEKResponse();
            try
            {
                // オーディオ系イベントではリクエスト形式が異なるのでJSONデータを直接確認する
                var reqJson = JObject.Parse(await req.ReadAsStringAsync());
                var reqObj = reqJson["request"];
                if (reqObj["type"].Value<string>() != "EventRequest")
                {
                    if (req.Headers.TryGetValue("SignatureCEK", out var signature))
                    {
                        var client = new ClovaClient();
                        var request = await client.GetRequest(signature, req.Body);

                        switch (request.Request.Type)
                        {
                            case RequestType.LaunchRequest:
                                // 起動リクエスト
                                // 再生途中のものがあるか確認
                                var stopCache = await PlayStatusService.FindCacheAsync(request.Session.User.UserId);
                                if (stopCache?.StopRequest != null && !string.IsNullOrEmpty(stopCache.StopRequest.BeforId) && !stopCache.StopRequest.IsFinished)
                                {
                                    response.AddText("再生途中の絵本があります。「続きから」と言うか、新しく絵本の名前を言ってください。");
                                    response.AddRepromptText("絵本の名前を言うか、「続きから」と言ってください。");
                                }
                                else
                                {
                                    response.AddText("絵本読み聞かせへようこそ！");
                                    response.AddText("何の絵本を読みますか？");
                                    response.AddSession("intent", "ChooseBookIntent");
                                    response.AddRepromptText("絵本の名前を言ってください。");
                                }
                                response.ShouldEndSession = false;
                                break;

                            case RequestType.SessionEndedRequest:
                                // 終了
                                response.AddText("読み聞かせを終了します。");
                                response.ShouldEndSession = true;
                                break;

                            case RequestType.IntentRequest:
                                await HandleIntentAsync(request, response, log);
                                break;
                        }
                    }
                }
                else
                {
                    // オーディオイベントの制御
                    // Clovaでのオーディオ再生が終わった際に呼び出される
                    if (reqObj["event"]["namespace"].Value<string>() == "AudioPlayer")
                    {
                        var userId = reqJson["session"]["user"]["userId"].Value<string>();
                        var beforeId = reqObj["event"]["payload"]["token"].Value<string>();
                        var eventName = reqObj["event"]["name"].Value<string>();
                        var cache = await PlayStatusService.FindCacheAsync(userId);
                        switch (eventName)
                        {
                            case "PlayFinished":
                                // LINE Botのメニューから停止要求がされていれば呼び出さない
                                if (cache?.StopRequest != null && cache.StopRequest.IsStopped)
                                {
                                    // DBに存在する停止レコードに、直近の再生済みオーディオのIDを更新する
                                    cache.StopRequest.BeforId = beforeId;
                                    cache.StopRequest.IsPaused = false;
                                    await PlayStatusService.SaveAsync(cache);
                                }
                                else if (cache?.StopRequest != null && cache.StopRequest.IsPaused)
                                {
                                    // 一時停止状態の場合は無音を流す（無限ループ）
                                    response.Response.Directives.Add(GetAudioResponseDirective(beforeId, "一時停止中", Consts.SilentAudioFileUrl));
                                    response.ShouldEndSession = true;
                                }
                                else
                                {
                                    // キャッシュ削除
                                    await PlayStatusService.DeleteAsync(cache);

                                    // 次のオーディオ再生（画像送付があればそれも）を実施
                                    await AddAudioPlayAndPushPictureAsync(response, userId, null, beforeId, log);
                                }
                                break;

                            case "PlayPaused":
                                // 再生中断された場合
                                cache.StopRequest.IsStopped = true;
                                await PlayStatusService.SaveAsync(cache);
                                break;

                            case "PlayStarted":
                                // 再生開始OK：DBに記録
                                // 再生開始時はStopped=falseで登録
                                if (cache?.StopRequest == null)
                                {
                                    await PlayStatusService.AddCacheAsync(userId, beforeId);
                                }
                                else
                                {
                                    cache.StopRequest.BeforId = beforeId;
                                    await PlayStatusService.SaveAsync(cache);
                                }
                                break;
                        }
                    }
                }

                // JSONの変換がうまくいかなかったので自前で変換して返す
                string json = JsonConvert.SerializeObject(response);

                return (ActionResult)new OkObjectResult(json);
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                log.LogError(ex.StackTrace);
                log.LogInformation(await req.ReadAsStringAsync());
                return (ActionResult)new ForbidResult();
            }
        }

        private static async Task HandleIntentAsync(CEKRequest request, CEKResponse response, ILogger log)
        {
            switch (request.Request.Intent.Name)
            {
                case "ChooseBookIntent":
                    // 絵本選択
                    string bookTitle = null;
                    var random = request.Request.Intent.Slots.ContainsKey("randomBook") ?
                        request.Request.Intent.Slots["randomBook"].Value : string.Empty;
                    if (random != string.Empty)
                    {
                        // 無料または購入済みの絵本
                        var books = await BookService.FindAllAvailableBooksAsync(request.Session.User.UserId);
                        bookTitle = books[new Random().Next() % books.Count].Title;
                    }
                    else
                    {
                        bookTitle = request.Request.Intent.Slots.ContainsKey("bookTitle") ?
                            request.Request.Intent.Slots["bookTitle"].Value : string.Empty;
                    }

                    log.LogInformation($"bookTitle:{bookTitle}");

                    // 再生途中の情報を消す
                    var oldPlayed = await PlayStatusService.FindCacheAsync(request.Session.User.UserId);
                    if (oldPlayed != null)
                    {
                        await PlayStatusService.DeleteAsync(oldPlayed);
                    }
                    await AddAudioPlayAndPushPictureAsync(response, request.Session.User.UserId, bookTitle, null, log);
                    break;

                case "ContinueIntent":
                    {
                        // 続きから
                        var userId = request.Session.User.UserId;
                        var cache = await PlayStatusService.FindCacheAsync(userId);
                        if (cache?.StopRequest != null && !string.IsNullOrEmpty(cache.StopRequest.BeforId))
                        {
                            await PlayStatusService.DeleteAsync(cache);
                            await AddAudioPlayAndPushPictureAsync(response, userId, null, cache.StopRequest.BeforId, log);
                        }
                        else
                        {
                            response.AddText("読みかけの絵本はありません。新しい絵本を読むので、絵本の名前を言ってください。");
                            response.AddSession("intent", "ChooseBookIntent");
                            response.ShouldEndSession = false;
                        }
                    }
                    break;

                case "ImpressionIntent":
                    {
                        // キャッシュで読み聞かせが完了になっていれば処理
                        var cache = await PlayStatusService.FindCacheAsync(request.Session.User.UserId);
                        // 感想のスロットを評価点数に変換
                        if (cache.StopRequest.IsFinished && TryParseImpressionSlot(request, out var rate))
                        {
                            await RateService.UpdateRateAndSendResultAsync(
                                request.Session.User.UserId, rate, cache.StopRequest.BeforId.Split('-')[0], null,
                                new LineMessagingClient(Consts.LineMessagingApiAccessToken));

                            // キャッシュ削除
                            await PlayStatusService.DeleteAsync(cache);

                            response.AddText("ありがとうございました！またお話が聞きたくなったら、いつでも呼んでくださいね！");
                            response.ShouldEndSession = true;
                        }
                        else
                        {
                            response.AddText("すみません、わかりません。評価はLINEからお願いします。新しい絵本を読む場合は、絵本の名前を言ってください。");
                            response.AddSession("intent", "ChooseBookIntent");
                            response.ShouldEndSession = false;
                        }
                    }
                    break;

                case "BookListIntent":
                    // 絵本リスト
                    response.AddText("LINEに一覧を送りました。一覧から読みたい絵本を選んでください。");
                    response.AddRepromptText("絵本の名前を言ってください。");
                    response.AddSession("intent", "ChooseBookIntent");
                    response.ShouldEndSession = false;

                    await PushListAsync(request.Session.User.UserId);
                    break;

                case "Clova.CancelIntent":
                    response.ShouldEndSession = true;
                    break;
                case "Clova.YesIntent":
                    break;
                case "Clova.NoIntent":
                    break;
                case "Clova.GuideIntent":
                    response.AddText("LINEのトーク画面を開きながら、「桃太郎を読んで」などと言ってみてください。");
                    response.AddText("一覧を見せてと言うと、絵本の一覧を確認できます。");
                    response.AddText("トーク画面の絵本メニューからは、リストの確認や絵本の追加購入ができます。");
                    response.ShouldEndSession = false;
                    break;
            }
        }

        private static async Task PushListAsync(string userId)
        {
            var lineMessagingClient = new LineMessagingClient(Consts.LineMessagingApiAccessToken);
            await lineMessagingClient.PushMessageAsync(userId, await LineMessageBuilder.BuildAvailableListMessageAsync(userId));
        }

        private static async Task AddAudioPlayAndPushPictureAsync(CEKResponse response, string userId, string title, string beforeId, ILogger log)
        {
            var lineMessagingClient = new LineMessagingClient(Consts.LineMessagingApiAccessToken);

            string bookKey = null;
            string page = null;
            if (beforeId != null)
            {
                var beforeIdSplit = beforeId.Split('-');
                bookKey = beforeIdSplit[0];
                page = beforeIdSplit[1];
            }

            // CosmosDBからデータを取得
            var book = await BookService.FindBookByTitleOrKeyAsync(title, bookKey);
            if (book == null)
            {
                response.AddText("すみません。絵本が見つかりませんでした。");
                response.ShouldEndSession = true;
                return;
            }

            int beforeIndex = page == null ? -1 : book.Speeches.IndexOf(book.Speeches.Where(p => p.Key == page).First());

            // 最終ページの再生後は終了
            if (book.Speeches.Count - 1 == beforeIndex)
            {
                // 完了キャッシュ
                await PlayStatusService.AddCacheAsync(userId, beforeId, true);

                // LINEへ評価登録用のメッセージを送信
                var flexMessage = FlexMessage.CreateBubbleMessage("rating");
                flexMessage.SetBubbleContainer(new BubbleContainer()
                    .SetHeader(
                        new BoxComponent(BoxLayout.Vertical) { Spacing = Spacing.Sm, Flex = 0 }.AddContents(new TextComponent("おもしろかったですか？")))
                    .SetHero(imageUrl: ImageUrlBuilder.GetCoverThumbnailUrl(book.Key))
                    .SetBody(
                        boxLayout: BoxLayout.Baseline,
                        flex: null,
                        spacing: null,
                        margin: null).AddBodyContents(new TextComponent($"絵本『{book.Title}』の感想をお願いします") { Size = ComponentSize.Sm })
                    .SetFooter(new BoxComponent(BoxLayout.Vertical) { Spacing = Spacing.Lg, Flex = 0 }
                        .AddContents(new ButtonComponent(new PostbackTemplateAction("５：とてもおもしろかった！", JsonConvert.SerializeObject(new Rating { Key = book.Key, Rate = 5 }), $"絵本『{book.Title}』は評価：5です！")))
                        .AddContents(new ButtonComponent(new PostbackTemplateAction("４：おもしろかった！", JsonConvert.SerializeObject(new Rating { Key = book.Key, Rate = 4 }), $"絵本『{book.Title}』は評価：4です！")))
                        .AddContents(new ButtonComponent(new PostbackTemplateAction("３：まあまあかな。", JsonConvert.SerializeObject(new Rating { Key = book.Key, Rate = 3 }), $"絵本『{book.Title}』は評価：3です。")))
                        .AddContents(new ButtonComponent(new PostbackTemplateAction("２：あんまり…", JsonConvert.SerializeObject(new Rating { Key = book.Key, Rate = 2 }), $"絵本『{book.Title}』は評価：2です。")))
                        .AddContents(new ButtonComponent(new PostbackTemplateAction("１：つまらない…", JsonConvert.SerializeObject(new Rating { Key = book.Key, Rate = 1 }), $"絵本『{book.Title}』は評価：1です。")))
                    )
                );
                await lineMessagingClient.PushMessageAsync(userId, new List<ISendMessage> { flexMessage });

                // Clovaからの評価受付
                response.AddText($"これで絵本はおわりです。");
                response.AddText($"この絵本の感想を教えてください。LINEで評価を選択するか、各評価のコメントを参考にひとことで言ってください。");
                response.AddSession("intent", "ImpressionIntent");
                response.ShouldEndSession = false;

                return;
            }

            var targetPage = book.Speeches[beforeIndex + 1];

            string emotionParam = Uri.EscapeUriString($"{{\"j\":\"{targetPage.Joy}\",\"s\":\"{targetPage.Sadness}\",\"a\":\"{targetPage.Anger}\"}}");
            string text = targetPage.Text;
            string speed = targetPage.Speed.ToString("0.00");
            string pitch = targetPage.Pitch.ToString("0.00");
            string range = targetPage.Range.ToString("0.00");
            string id = $"{book.Key}-{targetPage.Key}";

            string speakerName = targetPage.Speaker == "narrator"
                ? await CustomNarratorService.GetNarratorNameAsync(userId)
                : targetPage.Speaker;

            var url = $"https://webapi.aitalk.jp/webapi/v2/ttsget.php?username={Consts.AITalkApiUsername}&password={Consts.AITalkApiPassword}&text={text}&input_type=text&speaker_name={speakerName}&volume=2.00&speed={speed}&pitch={pitch}&range={range}&style={emotionParam}&ext=mp3";

            response.Response.Directives.Add(GetAudioResponseDirective(id, $"{title}-{targetPage.Key}", url));
            response.ShouldEndSession = true;

            var messages = new List<ISendMessage>();

            // 新規ページ（画像ありの本文データ）なら、画像をイメージマップでプッシュ送信
            if (targetPage.HasImage)
            {
                string imageBaseUrl = $"https://lineclovapicturebook.blob.core.windows.net/pictures/{book.Key}/{targetPage.Key}";
                messages.Add(new ImagemapMessage(imageBaseUrl, text,
                    new ImagemapSize(1040, 1040), new List<IImagemapAction> { new MessageImagemapAction(new ImagemapArea(0, 0, 1, 1), text) }));
            }

            // テキストモードにあわせてテキスト送信
            var cache = await TextModeService.FindCacheAsync(userId);
            switch (cache?.TextMode?.TextMode)
            {
                case TextMode.Kanji:
                    // 漢字
                    messages.Add(new TextMessage(text));
                    break;
                case TextMode.Kana:
                    // かな
                    messages.Add(new TextMessage(targetPage.KanaText));
                    break;
                case TextMode.None:
                default:
                    // なし
                    break;
            }

            if (messages.Count > 0)
            {
                await lineMessagingClient.PushMessageAsync(userId, messages);
            }
        }

        private static Directive GetAudioResponseDirective(string audioItemId, string title, string url)
        {
            return new Directive()
            {
                Header = new DirectiveHeader()
                {
                    Namespace = DirectiveHeaderNamespace.AudioPlayer,
                    Name = DirectiveHeaderName.Play
                },
                Payload = new Dictionary<string, object>
                {
                    {
                        "audioItem", new Dictionary<string, object>
                        {
                            { "audioItemId", audioItemId },
                            { "title", title },
                            { "artist", "絵本読み聞かせ" },
                            {
                                "stream", new Dictionary<string, object>
                                {
                                    { "beginAtInMilliseconds", 0 },
                                    {
                                        "progressReport", new Dictionary<string, object>
                                        {
                                            { "progressReportDelayInMilliseconds", null },
                                            { "progressReportIntervalInMilliseconds", null },
                                            { "progressReportPositionInMilliseconds", null }
                                        }
                                    },
                                    { "url", url },
                                    { "urlPlayable", true }
                                }
                            },
                        }
                    },
                    { "playBehavior", "REPLACE_ALL" }
                }
            };
        }

        private static bool TryParseImpressionSlot(CEKRequest request, out int rate)
        {
            rate = 0;
            if (request.Request.Intent.Slots.ContainsKey("imp_one"))
            {
                rate = 1;
            }
            else if (request.Request.Intent.Slots.ContainsKey("imp_two"))
            {
                rate = 2;
            }
            else if (request.Request.Intent.Slots.ContainsKey("imp_three"))
            {
                rate = 3;
            }
            else if (request.Request.Intent.Slots.ContainsKey("imp_four"))
            {
                rate = 4;
            }
            else if (request.Request.Intent.Slots.ContainsKey("imp_five"))
            {
                rate = 5;
            }
            return rate != 0;
        }
    }
}
