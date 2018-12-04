using System;
using System.Collections.Generic;
using System.Text;

namespace ClovaLinePictureBookLineBootAwards2018ver
{
    public class ImageUrlBuilder
    {
        public static string GetCoverThumbnailUrl(string bookKey)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            return $"{Consts.BlobStoragePicturesBaseUrl}{bookKey}/cover/300?timestamp={timestamp}";
        }

        public static string GetNarratorImageUrl(string narratorKey)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            return $"{Consts.BlobStorageNarratorsBaseUrl}{narratorKey}.png?timestamp={timestamp}";
        }
    }
}
