using System;
using System.Collections.Generic;
using System.Text;

namespace ClovaLinePictureBookLineBootAwards2018ver
{
    public class Consts_sample
    {
        /// <summary>Cosmon DBの接続文字列</summary>
        public static readonly string CosmosDbConnectionString = "(DBの接続文字列)";
        /// <summary>Cosmon DBのDB名</summary>
        public static readonly string CosmosDbDatabaseName = "(DB名)";
        /// <summary>Cosmon DBのBook格納コレクション名</summary>
        public static readonly string CosmosDbBookCollectionName = "(コレクション名)";
        /// <summary>Cosmon DBのTransaction格納コレクション名</summary>
        public static readonly string CosmosDbTransactionCollectionName = "(コレクション名)";
        /// <summary>Cosmon DBのCustomNarrator格納コレクション名</summary>
        public static readonly string CosmosDbCustomNarratorCollectionName = "(コレクション名)";

        /// <summary>Blob StorageのベースURL（絵本画像）</summary>
        public static readonly string BlobStoragePicturesBaseUrl = "https://xxxxxxxxx.blob.core.windows.net/pictures/";
        /// <summary>Blob StorageのベースURL（ナレーター画像）</summary>
        public static readonly string BlobStorageNarratorsBaseUrl = "https://xxxxxxxxx.blob.core.windows.net/narrators/";

        /// <summary>AITalk Web APIのユーザー名</summary>
        public static readonly string AITalkApiUsername = "(ユーザー名)";
        /// <summary>AITalk Web APIのパスワード</summary>
        public static readonly string AITalkApiPassword = "(パスワード)";

        /// <summary>Messaging APIのチャンネルシークレット</summary>
        public static readonly string LineMessagingApiChannelSecret = "(チャンネルシークレット)";
        /// <summary>Messaging APIのアクセストークン</summary>
        public static readonly string LineMessagingApiAccessToken = "(アクセストークン)";

        /// <summary>LINE PayのチャンネルID</summary>
        public static readonly string LinePayChannelId = "(チャンネルID)";
        /// <summary>LINE Payのシークレットキー</summary>
        public static readonly string LinePayChannelSecretKey = "(シークレットキー)";
        /// <summary>LINE Payがサンドボックスかどうか</summary>
        public static readonly bool LinePayIsSandbox = true;
        /// <summary>LINE Pay確認用関数のURL</summary>
        public static readonly string LinePayConfirmFunctionBaseUrl = "(URL)";

        /// <summary>無音音声ファイルのURL</summary>
        public static readonly string SilentAudioFileUrl = "(URL)";
    }
}
