using System.Web;
using System.Web.Mvc;

namespace TracksMassTaxFaqs
{
   public class FilterConfig
   {
      public static void RegisterGlobalFilters(GlobalFilterCollection filters)
      {
         filters.Add(new HandleErrorAttribute());
      }
   }
}