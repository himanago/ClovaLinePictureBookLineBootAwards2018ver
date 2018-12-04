using System;
using System.Collections.Generic;
using System.Text;

namespace ClovaLinePictureBookLineBootAwards2018ver
{
    /// <summary>
    /// 絵本再生時のテキスト送信モードの列挙体です。
    /// </summary>
    public enum TextMode
    {
        /// <summary>
        /// テキストなし
        /// </summary>
        None,
        /// <summary>
        /// ひらがな/カタカナのみ
        /// </summary>
        Kana,
        /// <summary>
        /// 漢字あり
        /// </summary>
        Kanji
    }

    public static class TextModeExt
    {
        public static string DisplayName(this TextMode textMode)
        {
            string[] names = { "テキストなし", "ひらがな/カタカナのみ", "漢字あり" };
            return names[(int)textMode];
        }
    }
}
