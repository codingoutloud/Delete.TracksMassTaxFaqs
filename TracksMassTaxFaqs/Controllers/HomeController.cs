using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Mvc;
using Microsoft.WindowsAzure.Storage.Blob;
using TracksMassTaxFaqs.Models;
using ValetKeyPattern.AzureStorage;

namespace TracksMassTaxFaqs.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/

        public ActionResult Index()
        {
           var client = new HttpClient();
           var result =
              client.GetAsync("http://www.mass.gov/dor/docs/dor/law-changes/faqss-computer-software-2013.pdf").Result;
           var contents = result.Content.ReadAsByteArrayAsync().Result;
           var thumbprint = GetThumbprint(contents);
           var blobUrl = ConfigurationManager.AppSettings["BlobValetKeyUrl"];
           var blobUri = BlobContainerValet.GetDestinationPathFromValetKey(blobUrl);

           WriteToBlobIfChanged(blobUri, thumbprint, contents); // TODO: issue if this gets too large

           DateTimeOffset? mostRecentlyUpdatedAt = default(DateTimeOffset);
           int verCount = GetSnapshotCount(ref mostRecentlyUpdatedAt);
           var urlHistoryModel = new UrlHistoryModel()
                                    {
                                       Url = blobUrl,
                                       LastUpdatedAt = mostRecentlyUpdatedAt,
                                       VersionCount = verCount
                                    };
           return View(urlHistoryModel);
        }


       static int GetSnapshotCount(ref DateTimeOffset? mostRecentlyUpdatedAt)
       {
          var valetKeyUrl = ConfigurationManager.AppSettings["BlobValetKeyUrl"];
          var destinationUrl = ConfigurationManager.AppSettings["BlobDestinationUrl"];
          var blob = BlobContainerValet.GetCloudBlockBlob(valetKeyUrl, new Uri(destinationUrl));

          var blobRequestOptions = new BlobRequestOptions();
          var snapCount = 0;

          mostRecentlyUpdatedAt = null;
          foreach (CloudBlockBlob b in blob.Container.ListBlobs(useFlatBlobListing: true, blobListingDetails: BlobListingDetails.Snapshots))
          {
             snapCount++;
             if (mostRecentlyUpdatedAt.HasValue)
             {
                if (mostRecentlyUpdatedAt.Value < b.SnapshotTime)
                   mostRecentlyUpdatedAt = b.SnapshotTime;
             }
             else
             {
                mostRecentlyUpdatedAt = b.SnapshotTime;
             }
          }

          return snapCount;
       }

        static string GetThumbprint(byte[] bytes)
        {
           var data = System.Security.Cryptography.SHA1.Create().ComputeHash(bytes);
           return Convert.ToBase64String(data);
        }

        static bool WriteToBlobIfChanged(Uri blobUri, string thumbprint, byte[] contents)
        {
           Console.Write("[thumbprint = {0} ... ", thumbprint);

           var valetKeyUrl = ConfigurationManager.AppSettings["BlobValetKeyUrl"];
           var destinationUrl = ConfigurationManager.AppSettings["BlobDestinationUrl"];


           var blob = BlobContainerValet.GetCloudBlockBlob(valetKeyUrl, new Uri(destinationUrl));

           try
           {
              blob.FetchAttributes();
           }
           catch (Microsoft.WindowsAzure.Storage.StorageException ex)
           {
              // if (Enum.GetName(typeof(RequestResult), ex.RequestInformation.HttpStatusCode) == HttpStatusCode.NotFound))
              if (ex.RequestInformation.HttpStatusCode == Convert.ToInt32(HttpStatusCode.NotFound))
              {
                 // write it the first time
                 WriteBlob(blobUri, thumbprint, contents);
                 Console.WriteLine(" (created)");
                 return true;
              }
              else
              {
                 throw;
              }
           }
           var oldthumbprint = blob.Metadata["thumbprint"];

           if (oldthumbprint != thumbprint)
           {
              Console.Write("Change detected: {0}... ", DateTime.Now.ToLongTimeString()); // TODO: DateTimeOffset

              var snapshot = blob.CreateSnapshot();
              WriteBlob(blobUri, thumbprint, contents);

              Console.WriteLine("updated.");
              return true;
           }
           else
           {
              Console.WriteLine("No change at {0}...", DateTime.Now.ToLongTimeString());
              return false;
           }
        }

        static void WriteBlob(Uri blobUri, string thumbprint, byte[] contents)
        {
           var valetKeyUrl = ConfigurationManager.AppSettings["BlobValetKeyUrl"];
           var destinationUrl = ConfigurationManager.AppSettings["BlobDestinationUrl"];
           var blob = ValetKeyPattern.AzureStorage.BlobContainerValet.GetCloudBlockBlob(valetKeyUrl, new Uri(destinationUrl));
           var memstream = new MemoryStream(contents);
           blob.UploadFromStream(memstream);
           blob.Metadata["thumbprint"] = thumbprint;
           blob.SetMetadata();
           blob.Properties.ContentType = ConfigurationManager.AppSettings["BlobDestinationMimeType"];
           blob.SetProperties();
        }
    }
}
