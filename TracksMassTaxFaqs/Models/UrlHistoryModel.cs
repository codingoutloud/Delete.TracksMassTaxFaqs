using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TracksMassTaxFaqs.Models
{
   public class UrlHistoryModel
   {
      public string Url { get; set; }
      public DateTimeOffset? LastUpdatedAt { get; set; }
      public int VersionCount { get; set; }
   }
}
