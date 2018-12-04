
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
                // �I�[�f�B�I�n�C�x���g�ł̓��N�G�X�g�`�����قȂ�̂�JSON�f�[�^�𒼐ڊm�F����
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
                                // �N�����N�G�X�g
                                // �Đ��r���̂��̂����邩�m�F
                                var stopCache = await PlayStatusService.FindCacheAsync(request.Session.User.UserId);
                                if (stopCache?.StopRequest != null && !string.IsNullOrEmpty(stopCache.StopRequest.BeforId) && !stopCache.StopRequest.IsFinished)
                                {
                                    response.AddText("�Đ��r���̊G�{������܂��B�u��������v�ƌ������A�V�����G�{�̖��O�������Ă��������B");
                                    response.AddRepromptText("�G�{�̖��O���������A�u��������v�ƌ����Ă��������B");
                                }
                                else
                                {
                                    response.AddText("�G�{�ǂݕ������ւ悤�����I");
                                    response.AddText("���̊G�{��ǂ݂܂����H");
                                    response.AddSession("intent", "ChooseBookIntent");
                                    response.AddRepromptText("�G�{�̖��O�������Ă��������B");
                                }
                                response.ShouldEndSession = false;
                                break;

                            case RequestType.SessionEndedRequest:
                                // �I��
                                response.AddText("�ǂݕ��������I�����܂��B");
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
                    // �I�[�f�B�I�C�x���g�̐���
                    // Clova�ł̃I�[�f�B�I�Đ����I������ۂɌĂяo�����
                    if (reqObj["event"]["namespace"].Value<string>() == "AudioPlayer")
                    {
                        var userId = reqJson["session"]["user"]["userId"].Value<string>();
                        var beforeId = reqObj["event"]["payload"]["token"].Value<string>();
                        var eventName = reqObj["event"]["name"].Value<string>();
                        var cache = await PlayStatusService.FindCacheAsync(userId);
                        switch (eventName)
                        {
                            case "PlayFinished":
                                // LINE Bot�̃��j���[�����~�v��������Ă���ΌĂяo���Ȃ�
                                if (cache?.StopRequest != null && cache.StopRequest.IsStopped)
                                {
                                    // DB�ɑ��݂����~���R�[�h�ɁA���߂̍Đ��ς݃I�[�f�B�I��ID���X�V����
                                    cache.StopRequest.BeforId = beforeId;
                                    cache.StopRequest.IsPaused = false;
                                    await PlayStatusService.SaveAsync(cache);
                                }
                                else if (cache?.StopRequest != null && cache.StopRequest.IsPaused)
                                {
                                    // �ꎞ��~��Ԃ̏ꍇ�͖����𗬂��i�������[�v�j
                                    response.Response.Directives.Add(GetAudioResponseDirective(beforeId, "�ꎞ��~��", Consts.SilentAudioFileUrl));
                                    response.ShouldEndSession = true;
                                }
                                else
                                {
                                    // �L���b�V���폜
                                    await PlayStatusService.DeleteAsync(cache);

                                    // ���̃I�[�f�B�I�Đ��i�摜���t������΂�����j�����{
                                    await AddAudioPlayAndPushPictureAsync(response, userId, null, beforeId, log);
                                }
                                break;

                            case "PlayPaused":
                                // �Đ����f���ꂽ�ꍇ
                                cache.StopRequest.IsStopped = true;
                                await PlayStatusService.SaveAsync(cache);
                                break;

                            case "PlayStarted":
                                // �Đ��J�nOK�FDB�ɋL�^
                                // �Đ��J�n����Stopped=false�œo�^
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

                // JSON�̕ϊ������܂������Ȃ������̂Ŏ��O�ŕϊ����ĕԂ�
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
                    // �G�{�I��
                    string bookTitle = null;
                    var random = request.Request.Intent.Slots.ContainsKey("randomBook") ?
                        request.Request.Intent.Slots["randomBook"].Value : string.Empty;
                    if (random != string.Empty)
                    {
                        // �����܂��͍w���ς݂̊G�{
                        var books = await BookService.FindAllAvailableBooksAsync(request.Session.User.UserId);
                        bookTitle = books[new Random().Next() % books.Count].Title;
                    }
                    else
                    {
                        bookTitle = request.Request.Intent.Slots.ContainsKey("bookTitle") ?
                            request.Request.Intent.Slots["bookTitle"].Value : string.Empty;
                    }

                    log.LogInformation($"bookTitle:{bookTitle}");

                    // �Đ��r���̏�������
                    var oldPlayed = await PlayStatusService.FindCacheAsync(request.Session.User.UserId);
                    if (oldPlayed != null)
                    {
                        await PlayStatusService.DeleteAsync(oldPlayed);
                    }
                    await AddAudioPlayAndPushPictureAsync(response, request.Session.User.UserId, bookTitle, null, log);
                    break;

                case "ContinueIntent":
                    {
                        // ��������
                        var userId = request.Session.User.UserId;
                        var cache = await PlayStatusService.FindCacheAsync(userId);
                        if (cache?.StopRequest != null && !string.IsNullOrEmpty(cache.StopRequest.BeforId))
                        {
                            await PlayStatusService.DeleteAsync(cache);
                            await AddAudioPlayAndPushPictureAsync(response, userId, null, cache.StopRequest.BeforId, log);
                        }
                        else
                        {
                            response.AddText("�ǂ݂����̊G�{�͂���܂���B�V�����G�{��ǂނ̂ŁA�G�{�̖��O�������Ă��������B");
                            response.AddSession("intent", "ChooseBookIntent");
                            response.ShouldEndSession = false;
                        }
                    }
                    break;

                case "ImpressionIntent":
                    {
                        // �L���b�V���œǂݕ������������ɂȂ��Ă���Ώ���
                        var cache = await PlayStatusService.FindCacheAsync(request.Session.User.UserId);
                        // ���z�̃X���b�g��]���_���ɕϊ�
                        if (cache.StopRequest.IsFinished && TryParseImpressionSlot(request, out var rate))
                        {
                            await RateService.UpdateRateAndSendResultAsync(
                                request.Session.User.UserId, rate, cache.StopRequest.BeforId.Split('-')[0], null,
                                new LineMessagingClient(Consts.LineMessagingApiAccessToken));

                            // �L���b�V���폜
                            await PlayStatusService.DeleteAsync(cache);

                            response.AddText("���肪�Ƃ��������܂����I�܂����b�����������Ȃ�����A���ł��Ă�ł��������ˁI");
                            response.ShouldEndSession = true;
                        }
                        else
                        {
                            response.AddText("���݂܂���A�킩��܂���B�]����LINE���炨�肢���܂��B�V�����G�{��ǂޏꍇ�́A�G�{�̖��O�������Ă��������B");
                            response.AddSession("intent", "ChooseBookIntent");
                            response.ShouldEndSession = false;
                        }
                    }
                    break;

                case "BookListIntent":
                    // �G�{���X�g
                    response.AddText("LINE�Ɉꗗ�𑗂�܂����B�ꗗ����ǂ݂����G�{��I��ł��������B");
                    response.AddRepromptText("�G�{�̖��O�������Ă��������B");
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
                    response.AddText("LINE�̃g�[�N��ʂ��J���Ȃ���A�u�����Y��ǂ�Łv�Ȃǂƌ����Ă݂Ă��������B");
                    response.AddText("�ꗗ�������Ăƌ����ƁA�G�{�̈ꗗ���m�F�ł��܂��B");
                    response.AddText("�g�[�N��ʂ̊G�{���j���[����́A���X�g�̊m�F��G�{�̒ǉ��w�����ł��܂��B");
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

            // CosmosDB����f�[�^���擾
            var book = await BookService.FindBookByTitleOrKeyAsync(title, bookKey);
            if (book == null)
            {
                response.AddText("���݂܂���B�G�{��������܂���ł����B");
                response.ShouldEndSession = true;
                return;
            }

            int beforeIndex = page == null ? -1 : book.Speeches.IndexOf(book.Speeches.Where(p => p.Key == page).First());

            // �ŏI�y�[�W�̍Đ���͏I��
            if (book.Speeches.Count - 1 == beforeIndex)
            {
                // �����L���b�V��
                await PlayStatusService.AddCacheAsync(userId, beforeId, true);

                // LINE�֕]���o�^�p�̃��b�Z�[�W�𑗐M
                var flexMessage = FlexMessage.CreateBubbleMessage("rating");
                flexMessage.SetBubbleContainer(new BubbleContainer()
                    .SetHeader(
                        new BoxComponent(BoxLayout.Vertical) { Spacing = Spacing.Sm, Flex = 0 }.AddContents(new TextComponent("�������납�����ł����H")))
                    .SetHero(imageUrl: ImageUrlBuilder.GetCoverThumbnailUrl(book.Key))
                    .SetBody(
                        boxLayout: BoxLayout.Baseline,
                        flex: null,
                        spacing: null,
                        margin: null).AddBodyContents(new TextComponent($"�G�{�w{book.Title}�x�̊��z�����肢���܂�") { Size = ComponentSize.Sm })
                    .SetFooter(new BoxComponent(BoxLayout.Vertical) { Spacing = Spacing.Lg, Flex = 0 }
                        .AddContents(new ButtonComponent(new PostbackTemplateAction("�T�F�ƂĂ��������납�����I", JsonConvert.SerializeObject(new Rating { Key = book.Key, Rate = 5 }), $"�G�{�w{book.Title}�x�͕]���F5�ł��I")))
                        .AddContents(new ButtonComponent(new PostbackTemplateAction("�S�F�������납�����I", JsonConvert.SerializeObject(new Rating { Key = book.Key, Rate = 4 }), $"�G�{�w{book.Title}�x�͕]���F4�ł��I")))
                        .AddContents(new ButtonComponent(new PostbackTemplateAction("�R�F�܂��܂����ȁB", JsonConvert.SerializeObject(new Rating { Key = book.Key, Rate = 3 }), $"�G�{�w{book.Title}�x�͕]���F3�ł��B")))
                        .AddContents(new ButtonComponent(new PostbackTemplateAction("�Q�F����܂�c", JsonConvert.SerializeObject(new Rating { Key = book.Key, Rate = 2 }), $"�G�{�w{book.Title}�x�͕]���F2�ł��B")))
                        .AddContents(new ButtonComponent(new PostbackTemplateAction("�P�F�܂�Ȃ��c", JsonConvert.SerializeObject(new Rating { Key = book.Key, Rate = 1 }), $"�G�{�w{book.Title}�x�͕]���F1�ł��B")))
                    )
                );
                await lineMessagingClient.PushMessageAsync(userId, new List<ISendMessage> { flexMessage });

                // Clova����̕]����t
                response.AddText($"����ŊG�{�͂����ł��B");
                response.AddText($"���̊G�{�̊��z�������Ă��������BLINE�ŕ]����I�����邩�A�e�]���̃R�����g���Q�l�ɂЂƂ��ƂŌ����Ă��������B");
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

            // �V�K�y�[�W�i�摜����̖{���f�[�^�j�Ȃ�A�摜���C���[�W�}�b�v�Ńv�b�V�����M
            if (targetPage.HasImage)
            {
                string imageBaseUrl = $"https://lineclovapicturebook.blob.core.windows.net/pictures/{book.Key}/{targetPage.Key}";
                messages.Add(new ImagemapMessage(imageBaseUrl, text,
                    new ImagemapSize(1040, 1040), new List<IImagemapAction> { new MessageImagemapAction(new ImagemapArea(0, 0, 1, 1), text) }));
            }

            // �e�L�X�g���[�h�ɂ��킹�ăe�L�X�g���M
            var cache = await TextModeService.FindCacheAsync(userId);
            switch (cache?.TextMode?.TextMode)
            {
                case TextMode.Kanji:
                    // ����
                    messages.Add(new TextMessage(text));
                    break;
                case TextMode.Kana:
                    // ����
                    messages.Add(new TextMessage(targetPage.KanaText));
                    break;
                case TextMode.None:
                default:
                    // �Ȃ�
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
                            { "artist", "�G�{�ǂݕ�����" },
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
