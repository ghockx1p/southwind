﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Southwind.Logic;
using Signum.Engine;
using Southwind.Web.Properties;
using Signum.Engine.Maps;
using Signum.Web;
using Signum.Web.PortableAreas;
using Signum.Engine.Authorization;
using Signum.Web.Auth;
using Signum.Web.AuthAdmin;
using Signum.Web.Operations;
using Signum.Entities.Authorization;
using System.Threading;
using Signum.Web.UserQueries;
using Signum.Web.Excel;
using Signum.Utilities;
using System.Globalization;
using Signum.Web.Chart;
using Signum.Web.Dashboard;
using Signum.Web.Exceptions;
using Signum.Web.Omnibox;
using Signum.Web.Files;
using Signum.Web.Disconnected;
using Signum.Web.Processes;
using Signum.Engine.Processes;
using Signum.Entities.Basics;
using Signum.Web.Notes;
using Signum.Web.Alerts;
using Signum.Web.Profiler;
using Signum.Web.Cache;
using Southwind.Entities;
using Signum.Web.Mailing;
using Signum.Web.Scheduler;
using Signum.Engine.Scheduler;
using Signum.Web.SMS;
using Signum.Web.Translation;
using Southwind.Web.BingTranslationService;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Signum.Engine.Basics;
using Signum.Web.Help;
using Signum.Web.Word;
using Signum.Web.Maps;
using Signum.Web.ViewLog;
using Signum.Web.DiffLog;
using Signum.Engine.Mailing;

namespace Southwind.Web
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class MvcApplication : System.Web.HttpApplication
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
             Navigator.ViewRouteName,
             "View/{webTypeName}/{id}",
             new { controller = "Navigator", action = "View", webTypeName = "", id = "" }
            );

            routes.MapRoute(
             Navigator.CreateRouteName,
             "Create/{webTypeName}/{id}",
             new { controller = "Navigator", action = "Create", webTypeName = "" }
            );

            routes.MapRoute(
                Finder.FindRouteName,
                "Find/{webQueryName}",
                new { controller = "Finder", action = "Find", webQueryName = "" }
            );

            RouteTable.Routes.MapRoute(
                 "EmbeddedResources",
                 "{*file}",
                 new { controller = "Resources", action = "GetFile" },
                 new { file = new EmbeddedFileExist() }
            );

            routes.MapRoute(
                "Default", // Route name
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Home", action = "Index", id = UrlParameter.Optional } // Parameter defaults
            );
        }//RegisterRoutes

        protected void Application_Start()
        {
            Statics.SessionFactory = new ScopeSessionFactory(new AspNetSessionFactory());

            Starter.Start(UserConnections.Replace(Settings.Default.ConnectionString));

            using (AuthLogic.Disable())
                Schema.Current.Initialize();

            WebStart();

            ProcessRunnerLogic.StartRunningProcesses(5 * 1000);

            SchedulerLogic.StartScheduledTasks();

            AsyncEmailSenderLogic.StartRunningEmailSenderAsync(5 * 1000);

            RegisterRoutes(RouteTable.Routes);

            AuthLogic.UserLogingIn += user =>
            {
                AllowLogin required = ScopeSessionFactory.IsOverriden ? AllowLogin.WindowsOnly : AllowLogin.WebOnly;

                AllowLogin current = user.Mixin<UserEmployeeMixin>().AllowLogin; 

                if (current != AllowLogin.WindowsAndWeb && current != required)
                    throw new UnauthorizedAccessException("User {0} is {1}".FormatWith(user, current.NiceToString()));
            }; //UserLogingIn
        }

        private void WebStart()
        {
            Navigator.Start(new NavigationManager("haradwaithwinds"));
            Finder.Start(new FinderManager());
            Constructor.Start(new ConstructorManager(), new ClientConstructorManager());

            OperationClient.Start(new OperationManager(), true);

            AuthClient.Start(
                types: true, 
                property: true, 
                queries: true, 
                resetPassword: true, 
                passwordExpiration: false, 
                singleSignOnMessage: false);

            Navigator.EntitySettings<UserEntity>().CreateViewOverrides()
                .AfterLine((UserEntity u) => u.Role, (html, tc) => html.ValueLine(tc, u => u.Mixin<UserEmployeeMixin>().AllowLogin))
                .AfterLine((UserEntity u) => u.Role, (html, tc) => html.EntityLine(tc, u => u.Mixin<UserEmployeeMixin>().Employee));

            AuthAdminClient.Start(
                types: true, 
                properties: true,
                queries: true, 
                operations: true,
                permissions: true);

            MailingClient.Start(
                smtpConfig: true,
                newsletter: false, 
                pop3Config: false,
                emailReport: false,
                quickLinkFrom: null);

            SMSClient.Start();

            SessionLogClient.Start();
            ExceptionClient.Start();
            UserQueriesClient.Start();
            FilesClient.Start(
                file: true,
                embeddedFile: true,
                filePath: false,
                embeddedFilePath: true);

            MapClient.Start();
            ChartClient.Start();
            ExcelClient.Start(
                toExcelPlain:  true, 
                excelReport: false,
                excelAttachment: false);

            WordClient.Start();
            DashboardClient.Start();

            DisconnectedClient.Start();
            ProcessClient.Start(
                packages: true,
                packageOperations: true);

            TranslationClient.Start(new AlreadyTranslatedTranslator(new BingTranslator()),
                translatorUser: true,
                translationReplacement: false,
                instanceTranslator: true);

            SchedulerClient.Start(simpleTask: true);

            NoteClient.Start(typeof(UserEntity), /*Note*/typeof(OrderEntity));
            AlertClient.Start(typeof(UserEntity), /*Alert*/typeof(OrderEntity));
            LinksClient.Start(widget: true, contextualItems: true);

            ViewLogClient.Start();
            DiffLogClient.Start();

            HelpClient.Start("Images", "http://localhost:7654/"); 

            SouthwindClient.Start();

            CacheClient.Start();

            ProfilerClient.Start();

            ScriptHtmlHelper.Manager.MainAssembly = typeof(SouthwindClient).Assembly;
            SignumControllerFactory.MainAssembly = typeof(SouthwindClient).Assembly;
            SignumControllerFactory.EveryController().AddFilters(ctx => 
                ctx.FilterInfo.AuthorizationFilters.OfType<AuthenticationRequiredAttribute>().Any() ? null : new AuthenticationRequiredAttribute());
            SignumControllerFactory.EveryController().AddFilters(new SignumExceptionHandlerAttribute());
            SignumControllerFactory.EveryController().AddFilters(new ProfilerFilterAttribute());
            SignumControllerFactory.RegisterAvoidValidate();

            Navigator.Initialize();

            OmniboxClient.Start();
            OmniboxClient.Register(new SpecialOmniboxProvider());
            OmniboxClient.Register(new EntityOmniboxProvider());
            OmniboxClient.Register(new DynamicQueryOmniboxProvider());
            OmniboxClient.Register(new UserQueryOmniboxProvider());
            OmniboxClient.Register(new ChartOmniboxProvider());
            OmniboxClient.Register(new UserChartOmniboxProvider());
            OmniboxClient.Register(new DashboardOmniboxProvider());
            OmniboxClient.Register(new HelpOmniboxProvider());
            OmniboxClient.Register(new MapOmniboxProvider());
        } //WebStart

        protected void Application_Error(Object sender, EventArgs e)
        {
            SignumExceptionHandlerAttribute.HandlerApplication_Error(Request, HttpContext.Current, true);
        }

        protected void Session_Start(object sender, EventArgs e)
        {
            UserTicketClient.LoginFromCookie();
        }

        protected void Session_End(object sender, EventArgs e)
        {
            SessionLogClient.LogSessionEnd((UserEntity)Session[UserHolder.UserSessionKey], TimeSpan.FromMinutes(Session.Timeout));
        }

        protected void Application_ReleaseRequestState(object sender, EventArgs e)
        {
            Statics.CleanThreadContextAndAssert();
            Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture = DefaultCulture;
        }

        protected void Application_AcquireRequestState(object sender, EventArgs e)
        {
            Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture = GetCulture(this.Request);
        }

        static CultureInfo DefaultCulture = CultureInfo.GetCultureInfo("en-US"); 

        public static CultureInfo GetCulture(HttpRequest request)
        {
            // 1 user preference
            if (UserEntity.Current?.CultureInfo != null)
                return UserEntity.Current.CultureInfo.ToCultureInfo();

            // 2 cookie (temporary)
            if (request.Cookies["language"] != null)
                return new CultureInfo(request.Cookies["language"].Value);

            //3 requestCulture or default
            CultureInfo ciRequest = TranslationClient.GetCultureRequest(request);
            if (ciRequest != null)
                return ciRequest;

            return DefaultCulture; //Translation
        }
    }

    public class BingTranslator : ITranslator
    {
        public List<string> TranslateBatch(List<string> list, string from, string to)
        {
            string token = AdmAuthentication.GetAccessToken("ClientId", "Secret"); //find one in https://datamarket.azure.com/developer/applications/register
           
            LanguageServiceClient client = new LanguageServiceClient();
            using (OperationContextScope scope = new OperationContextScope(client.InnerChannel))
            {
                System.ServiceModel.OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name] = new HttpRequestMessageProperty
                {
                    Method = "POST",
                    Headers = { { "Authorization", "Bearer " + token } }
                };

                return list.GroupsOf(a => a.Length, 10000).SelectMany(gr =>
                {
                    TranslateArrayResponse[] result = client.TranslateArray("", gr.ToArray(), from, to, new TranslateOptions());

                    return result.Select(a => a.TranslatedText).ToList();

                }).ToList();
            }
        }

        public bool AutoSelect()
        {
            return true;
        }
    } // BingTranslator
}
