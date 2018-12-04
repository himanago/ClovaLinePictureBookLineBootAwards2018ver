using ClovaLinePictureBookLineBootAwards2018ver.Models.MongoDB;
using ClovaLinePictureBookLineBootAwards2018ver.Services;
using Line.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClovaLinePictureBookLineBootAwards2018ver
{
    /// <summary>
    /// LINE Botから送信するメッセージを組み立てるstaticクラスです。
    /// </summary>
    public static class LineMessageBuilder
    {
        public static async Task<List<ISendMessage>> BuildAvailableListMessageAsync(string userId)
        {
            var books = await BookService.FindAllAvailableBooksAsync(userId);
            return new List<ISendMessage>
            {
                new TemplateMessage("一覧", new CarouselTemplate(books.Select(
                    b => new CarouselColumn($"{b.Summary}[評価平均{b.Ratings.Average(r => r.Rating).ToString("0.0")}点]",
                        thumbnailImageUrl: ImageUrlBuilder.GetCoverThumbnailUrl(b.Key), title: b.Title, actions: new List<ITemplateAction>
                        {
                            new MessageTemplateAction("読んでもらう", $"Clovaに「{b.Title}を読んで」と言ってください！")
                        })).ToList(), imageSize: ImageSizeType.Contain))
            };
        }

        public static List<ISendMessage> BuildPremiumListMessage(List<Book> books)
        {
            if (books.Count() == 0)
            {
                return new List<ISendMessage> { new TextMessage("現在追加購入できる絵本はありません。") };
            }

            return new List<ISendMessage>
            {
                new TemplateMessage("一覧", new CarouselTemplate(books.Select(
                    b => new CarouselColumn($"{b.Summary}[評価平均{b.Ratings.Average(r => r.Rating).ToString("0.0")}点]",
                        thumbnailImageUrl: ImageUrlBuilder.GetCoverThumbnailUrl(b.Key), title: $"{b.Title}(￥{b.Price})", actions: new List<ITemplateAction>
                        {
                            new PostbackTemplateAction("購入する", $"buyBook={b.Key}")
                        })).ToList(), imageSize: ImageSizeType.Contain))
            };
        }

        public static List<ISendMessage> BuildNarratorListMessage(List<CustomNarrator> narrators, string userId)
        {
            return new List<ISendMessage>
            {
                new TemplateMessage("ナレーター一覧", new CarouselTemplate(narrators.Select(
                    n => new CarouselColumn(n.Users.Contains(userId) ? "現在設定中" : $"{n.Name}さんに変更する",
                        thumbnailImageUrl: ImageUrlBuilder.GetNarratorImageUrl(n.Key), title: n.Name,
                        actions: new List<ITemplateAction>
                        {
                            n.Users.Contains(userId)
                                ? new PostbackTemplateAction("変更しない", "do-nothing")
                                : new PostbackTemplateAction("変更する", $"changeNarrator={n.Key}")
                        })).ToList(), imageSize: ImageSizeType.Contain))
            };
        }
    }
}
