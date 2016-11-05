﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using System.Xml.Linq;
using BuildFeed.Code;
using BuildFeed.Local;
using BuildFeed.Model;
using BuildFeed.Model.View;

namespace BuildFeed.Controllers
{
   public class SupportController : BaseController
   {
      private readonly BuildRepository _bModel;

      public SupportController()
      {
         _bModel = new BuildRepository();
      }

      [Route("login/")]
      public ActionResult Login() => View();

      [HttpPost]
      [Route("login/")]
      public ActionResult Login(LoginUser ru)
      {
         if (ModelState.IsValid)
         {
            bool isAuthenticated = Membership.ValidateUser(ru.UserName, ru.Password);

            if (isAuthenticated)
            {
               int expiryLength = ru.RememberMe
                  ? 129600
                  : 60;

               FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(ru.UserName, true, expiryLength);
               string encryptedTicket = FormsAuthentication.Encrypt(ticket);
               HttpCookie cookieTicket = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket)
               {
                  Expires = DateTime.Now.AddMinutes(expiryLength),
                  Path = FormsAuthentication.FormsCookiePath
               };
               Response.Cookies.Add(cookieTicket);

               string returnUrl = string.IsNullOrEmpty(Request.QueryString["ReturnUrl"])
                  ? "/"
                  : Request.QueryString["ReturnUrl"];

               return Redirect(returnUrl);
            }
         }

         ViewData["ErrorMessage"] = "The username and password are not valid.";
         return View(ru);
      }

      [Authorize]
      [Route("password/")]
      public ActionResult Password() => View();

      [HttpPost]
      [Authorize]
      [Route("password/")]
      public ActionResult Password(ChangePassword cp)
      {
         if (ModelState.IsValid)
         {
            MembershipUser user = Membership.GetUser();

            if (user != null)
            {
               bool success = user.ChangePassword(cp.OldPassword, cp.NewPassword);

               if (success)
               {
                  return Redirect("/");
               }
            }
         }

         ViewData["ErrorMessage"] = "There was an error changing your password.";
         return View(cp);
      }

      [Route("logout/")]
      public ActionResult Logout()
      {
         FormsAuthentication.SignOut();
         return Redirect("/");
      }

      [Route("register/")]
      public ActionResult Register() => View();

      [HttpPost]
      [Route("register/")]
      public ActionResult Register(RegistrationUser ru)
      {
         if (ModelState.IsValid)
         {
            MembershipCreateStatus status;
            Membership.CreateUser(ru.UserName, ru.Password, ru.EmailAddress, "THIS WILL BE IGNORED", "I WILL BE IGNORED", false, out status);

            switch (status)
            {
               case MembershipCreateStatus.Success:
                  return RedirectToAction("thanks_register");
               case MembershipCreateStatus.InvalidPassword:
                  ViewData["ErrorMessage"] = "The password is invalid.";
                  break;
               case MembershipCreateStatus.DuplicateEmail:
                  ViewData["ErrorMessage"] = "A user account with this email address already exists.";
                  break;
               case MembershipCreateStatus.DuplicateUserName:
                  ViewData["ErrorMessage"] = "A user account with this user name already exists.";
                  break;
               default:
                  ViewData["ErrorMessage"] = "Unspecified error.";
                  break;
            }
         }

         return View(ru);
      }

      [Route("register/thanks/")]
      public ActionResult thanks_register() => View();

      [Route("rss")]
      public async Task<ActionResult> Rss()
      {
         ViewBag.Labs = await _bModel.SelectAllLabs();
         return View();
      }

      [Route("sitemap/")]
#if !DEBUG
      [OutputCache(Duration = 3600, VaryByParam = "none", VaryByCustom = "userName;lang;theme")]
#endif
      public async Task<ActionResult> Sitemap()
      {
         List<Build> builds = await _bModel.SelectBuildsByOrder();
         var actions = new Dictionary<string, SitemapPagedAction[]>
         {
            {
               "Pages", new[]
               {
                  new SitemapPagedAction
                  {
                     UrlParams = new RouteValueDictionary(new
                     {
                        controller = "Front",
                        action = "Index",
                        page = 1
                     }),
                     Pages = (builds.Count + (FrontController.PAGE_SIZE - 1)) / FrontController.PAGE_SIZE
                  }
               }
            },
            {
               "Versions", (from b in builds
                            group b by new BuildVersion
                            {
                               Major = b.MajorVersion,
                               Minor = b.MinorVersion
                            }
                            into bv
                            orderby bv.Key.Major descending, bv.Key.Minor descending
                            select new SitemapPagedAction
                            {
                               Name = $"{InvariantTerms.ProductName} {bv.Key.Major}.{bv.Key.Minor}",
                               UrlParams = new RouteValueDictionary(new
                               {
                                  controller = "Front",
                                  action = "ViewVersion",
                                  major = bv.Key.Major,
                                  minor = bv.Key.Minor,
                                  page = 1
                               }),
                               Pages = (bv.Count() + (FrontController.PAGE_SIZE - 1)) / FrontController.PAGE_SIZE
                            }).ToArray()
            },
            {
               "Labs", (from b in builds
                        where !string.IsNullOrEmpty(b.Lab)
                        group b by b.Lab
                        into bv
                        orderby bv.Key
                        select new SitemapPagedAction
                        {
                           Name = bv.Key,
                           UrlParams = new RouteValueDictionary(new
                           {
                              controller = "Front",
                              action = "ViewLab",
                              lab = bv.Key,
                              page = 1
                           }),
                           Pages = (bv.Count() + (FrontController.PAGE_SIZE - 1)) / FrontController.PAGE_SIZE
                        }).ToArray()
            },
            {
               "Years", (from b in builds
                         where b.BuildTime.HasValue
                         group b by b.BuildTime.Value.Year
                         into bv
                         orderby bv.Key descending
                         select new SitemapPagedAction
                         {
                            Name = bv.Key.ToString(),
                            UrlParams = new RouteValueDictionary(new
                            {
                               controller = "Front",
                               action = "ViewYear",
                               year = bv.Key,
                               page = 1
                            }),
                            Pages = (bv.Count() + (FrontController.PAGE_SIZE - 1)) / FrontController.PAGE_SIZE
                         }).ToArray()
            },
            {
               "Sources", (from b in builds
                           group b by b.SourceType
                           into bv
                           orderby bv.Key
                           select new SitemapPagedAction
                           {
                              Name = MvcExtensions.GetDisplayTextForEnum(bv.Key),
                              UrlParams = new RouteValueDictionary(new
                              {
                                 controller = "Front",
                                 action = "ViewSource",
                                 source = bv.Key,
                                 page = 1
                              }),
                              Pages = (bv.Count() + (FrontController.PAGE_SIZE - 1)) / FrontController.PAGE_SIZE
                           }).ToArray()
            }
         };


         SitemapData model = new SitemapData
         {
            Builds = (from b in builds
                      group b by new
                      {
                         Major = b.MajorVersion,
                         Minor = b.MinorVersion,
                         Build = b.Number,
                         b.Revision
                      }
                      into bg
                      orderby bg.Key.Major descending, bg.Key.Minor descending, bg.Key.Build descending, bg.Key.Revision descending
                      select new SitemapDataBuildGroup
                      {
                         Id = new BuildGroup
                         {
                            Major = bg.Key.Major,
                            Minor = bg.Key.Minor,
                            Build = bg.Key.Build,
                            Revision = bg.Key.Revision
                         },
                         Builds = (from bgb in bg
                                   select new SitemapDataBuild
                                   {
                                      Id = bgb.Id,
                                      Name = bgb.FullBuildString
                                   }).ToArray()
                      }).ToArray(),
            Actions = actions,
            Labs = (from b in builds
                    group b by b.Lab
                    into lab
                    select lab.Key).ToArray()
         };

         return View(model);
      }

      [Route("xml-sitemap/")]
#if !DEBUG
      [OutputCache(Duration = 3600, VaryByParam = "none", VaryByCustom = "userName;lang;theme")]
#endif
      public async Task<ActionResult> XmlSitemap()
      {
         XNamespace xn = XNamespace.Get("http://www.sitemaps.org/schemas/sitemap/0.9");
         var xlist = new List<XElement>();

         // home page
         XElement home = new XElement(xn + "url");
         home.Add(new XElement(xn + "loc", Request.Url?.GetLeftPart(UriPartial.Authority) + "/"));
         home.Add(new XElement(xn + "changefreq", "daily"));
         xlist.Add(home);

         foreach (Build b in await _bModel.Select())
         {
            XElement url = new XElement(xn + "url");
            url.Add(new XElement(xn + "loc",
               Request.Url?.GetLeftPart(UriPartial.Authority) + Url.Action("ViewBuild",
                  "Front",
                  new
                  {
                     id = b.Id
                  })));
            if (b.Modified != DateTime.MinValue)
            {
               url.Add(new XElement(xn + "lastmod", b.Modified.ToString("yyyy-MM-dd")));
            }
            xlist.Add(url);
         }

         XDeclaration decl = new XDeclaration("1.0", "utf-8", "");
         XElement root = new XElement(xn + "urlset", xlist);

         XDocument xdoc = new XDocument(decl, root);

         Response.ContentType = "application/xml";
         xdoc.Save(Response.OutputStream);

         return new EmptyResult();
      }

      [Route("credits/")]
      public ActionResult Credits() => View();
   }
}