﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
using System.Web.Http;
using Southwind.Logic;
using Signum.Engine;
using Southwind.React.Properties;
using Signum.React;
using Signum.Utilities;
using Signum.Engine.Maps;
using Signum.Engine.Authorization;
using Signum.React.Facades;
using Signum.Entities;
using Signum.Entities.Authorization;
using Southwind.Entities;
using Signum.React.Auth;
using System.Web.Http.Dispatcher;
using Signum.React.ApiControllers;
using System.Reflection;
using Signum.React.Authorization;
using Signum.Entities.Omnibox;
using Signum.Entities.Chart;
using Signum.Engine.Chart;
using Signum.Entities.Dashboard;
using Signum.Engine.Dashboard;
using Signum.Entities.UserQueries;
using Signum.Engine.UserQueries;
using Signum.Entities.Help;
using Signum.React.Omnibox;
using Signum.Entities.Map;
using Signum.Engine.Operations;
using Signum.React.UserQueries;
using System.Globalization;
using System.Threading;
using Signum.Engine.Basics;
using Signum.React.Translation;
using Signum.React.Chart;
using Signum.React.Dashboard;
using Signum.React.Map;
using Signum.React.Cache;
using Signum.React.Scheduler;
using Signum.React.Processes;

namespace Southwind.React
{
    public class Global : HttpApplication
    {
        public static void RegisterMvcRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "Default",
                url: "{*catchall}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional },
                constraints: new { catchall = @"^(?!api).*" }
            );
        }

        void Application_Start(object sender, EventArgs e)
        {
            Starter.Start(UserConnections.Replace(Settings.Default.ConnectionString));

            using (AuthLogic.Disable())
                Schema.Current.Initialize();

            // Code that runs on application startup
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebStart);
            RegisterMvcRoutes(RouteTable.Routes);            

            Statics.SessionFactory = new ScopeSessionFactory(new WebApiSesionFactory());
        }


        public static void WebStart(HttpConfiguration config)
        {
            SignumServer.Start(config, typeof(Global).Assembly);
            AuthServer.Start(config, queries: true, types: true);
            CacheServer.Start(config);
            DisconnectedServer.Start(config);
            DashboardServer.Start(config);
            ChartServer.Start(config);
            SchedulerServer.Start(config);
            ProcessServer.Start(config);
            MapServer.Start(config);

            OmniboxServer.Start(config,
                new EntityOmniboxResultGenenerator(),
                new DynamicQueryOmniboxResultGenerator(),
                new ChartOmniboxResultGenerator(),
                new DashboardOmniboxResultGenerator(DashboardLogic.Autocomplete),
                new UserQueryOmniboxResultGenerator(UserQueryLogic.Autocomplete),
                new UserChartOmniboxResultGenerator(UserChartLogic.Autocomplete),
                new MapOmniboxResultGenerator(type => OperationLogic.TypeOperations(type).Any()),
                new ReactSpecialOmniboxGenerator()
                //new HelpModuleOmniboxResultGenerator(),
                );            
        }

        protected void Application_PostAuthorizeRequest()
        {
            HttpContext.Current.SetSessionStateBehavior(SessionStateBehavior.Required);
        }

        protected void Session_Start(object sender, EventArgs e)
        {
            UserTicketServer.LoginFromCookie();
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
            CultureInfo ciRequest = TranslationServer.GetCultureRequest(request);
            if (ciRequest != null)
                return ciRequest;

            return DefaultCulture; //Translation
        }
    }
}