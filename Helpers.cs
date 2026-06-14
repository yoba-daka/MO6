using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Examine;
using Examine.Search;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

//using NAudio.Wave;

namespace MosheSharon
{
    public static class Helpers
    {
        public static string[] hebrewDays = new string[] { "ראשון", "שני", "שלישי", "רביעי", "חמישי", "שישי", "שבת" };


        public static string GenerateUniqueCode(int length)
        {
            char[] chars = "ABCDEF0123456789".ToCharArray();
            byte[] data = new byte[length];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetNonZeroBytes(data);
            }

            StringBuilder result = new StringBuilder(length);
            foreach (byte b in data)
            {
                result.Append(chars[b % (chars.Length)]);
            }

            return result.ToString();
        }





    }

}